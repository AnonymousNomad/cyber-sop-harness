using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.Json;
using CyberSopHarness.Core;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h")
            {
                PrintHelp();
                return 0;
            }
            var dataDir = ResolveDataDir(args);
            var selectionStore = new ModelProviderSelectionStore(Path.Combine(dataDir, "provider-selection.json"));
            var endpointStore = new ExternalEndpointStore(Path.Combine(dataDir, "external-endpoint.json"));
            PersistentSecretStore CreateSecrets()
            {
                var protector = CreateProtector();
                return new PersistentSecretStore(Path.Combine(dataDir, "secrets"), protector, "cyber-sop-harness");
            }

            ISecretProtector CreateProtector()
            {
                if (OperatingSystem.IsWindows()) return new WindowsDpapiSecretProtector("cyber-sop-harness");
                return new PassphraseSecretProtector("cyber-sop-harness", ReadCustodyPassphrase);
            }

            switch (args[0])
            {
                case "setup":
                    return await SetupAsync(CreateSecrets(), selectionStore, args, dataDir, endpointStore);
                case "status":
                    return await StatusAsync(selectionStore, endpointStore);
                case "secret":
                    return await SecretAsync(CreateSecrets(), args);
                case "run":
                    return await RunAsync(CreateSecrets(), selectionStore, args, dataDir);
                case "endpoint":
                    return await EndpointAsync(endpointStore, args);
                case "desk":
                    return await DeskAsync(selectionStore, endpointStore, args, dataDir, CreateProtector);
                default:
                    Console.Error.WriteLine("unknown command: " + args[0]);
                    PrintHelp();
                    return 2;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or IOException or PlatformNotSupportedException or TimeoutException)
        {
            Console.Error.WriteLine("ERROR: " + exception.Message);
            return 1;
        }
    }

    private static string ResolveDataDir(string[] args)
    {
        var index = Array.IndexOf(args, "--data-dir");
        if (index >= 0 && index + 1 < args.Length) return Path.GetFullPath(args[index + 1]);
        return Path.Combine(Directory.GetCurrentDirectory(), "data");
    }

    private static string ReadCustodyPassphrase()
    {
        Console.Error.Write("Custody passphrase: ");
        var passphrase = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(passphrase) || passphrase.Length < 12) throw new InvalidOperationException("custody passphrase must contain at least 12 characters");
        return passphrase;
    }

    private static async Task<int> SetupAsync(PersistentSecretStore secrets, ModelProviderSelectionStore selectionStore, string[] args, string dataDir, ExternalEndpointStore endpointStore)
    {
        var disclosures = await BuildDisclosuresAsync(dataDir, secrets, endpointStore);
        foreach (var choice in disclosures)
        {
            Console.WriteLine(ProviderDisclosureRenderer.Render(choice));
            Console.WriteLine();
        }
        var provider = ArgumentValue(args, "--provider");
        if (provider is null)
        {
            Console.Write("Provider id: ");
            provider = Console.ReadLine()?.Trim();
        }
        if (string.IsNullOrWhiteSpace(provider)) throw new InvalidOperationException("no provider selected");
        var ackValue = ArgumentValue(args, "--ack-egress") ?? (disclosures.Any(item => item.EgressStatus == ProviderEgressStatus.External && item.ProviderId == provider) ? null : "no");
        if (ackValue is null)
        {
            Console.Write("This choice enables external egress. Acknowledge? (yes/no): ");
            ackValue = Console.ReadLine()?.Trim().ToLowerInvariant();
        }
        var acknowledged = string.Equals(ackValue, "yes", StringComparison.OrdinalIgnoreCase);
        var previous = await selectionStore.LoadAsync(CancellationToken.None);
        var wizard = new ModelProviderWizard(selectionStore, secrets, disclosures);
        var selectionEvent = await wizard.ConfirmAsync(provider, acknowledged, previous?.SelectionId, CancellationToken.None);
        Console.WriteLine("SELECTION_EVENT: " + selectionEvent.SelectionId + " " + selectionEvent.ProviderRef + " " + selectionEvent.EgressStatus + " previous=" + (selectionEvent.PreviousSelectionId ?? "none"));
        return 0;
    }

    private static async Task<int> DeskAsync(
        ModelProviderSelectionStore selectionStore,
        ExternalEndpointStore endpointStore,
        string[] args,
        string dataDir,
        Func<ISecretProtector> createProtector)
    {
        var selection = await selectionStore.LoadAsync(CancellationToken.None);
        var endpoint = await endpointStore.LoadAsync(CancellationToken.None);
        var providerModel = selection?.ProviderRef ?? "none";
        var engagementLabel = ArgumentValue(args, "--engagement") ?? "unassigned";
        var scopeRef = ArgumentValue(args, "--scope") ?? "none";
        var riskClass = ArgumentValue(args, "--risk") ?? "R0";
        var initialState = new CommandDeskState(
            Environment.UserName,
            "csh",
            engagementLabel,
            scopeRef,
            riskClass,
            providerModel,
            0,
            "not-measured",
            false,
            DateTimeOffset.UtcNow);
        var renderOptions = CommandDeskRenderOptions.FromEnvironment(args, !Console.IsOutputRedirected);
        var renderer = new CommandDeskRenderer(renderOptions);
        var registry = CommandDeskVerbRegistry.Default;
        var modelsDirectory = Path.GetFullPath(ArgumentValue(args, "--models-dir") ?? Path.Combine(dataDir, "..", "models"));
        var manifests = await StagedModelCatalog.LoadAsync(modelsDirectory, CancellationToken.None);
        var modelControl = new CommandDeskModelControl(manifests, selectionStore);
        LocalModelRuntime? deskRuntime = null;
        var engagementPath = ArgumentValue(args, "--engagement-manifest");
        var engagementManifest = engagementPath is null
            ? null
            : await EngagementManifestFile.LoadAsync(Path.GetFullPath(engagementPath), CancellationToken.None);
        var historyDirectory = args.Contains("--no-history", StringComparer.OrdinalIgnoreCase) ? null : Path.Combine(dataDir, "command-history");
        var replOptions = new CommandDeskReplOptions();

        async Task<CommandDeskResult> SubmitProposalAsync(string path)
        {
            if (engagementManifest is null) return CommandDeskResult.UsageError("proposal submission requires --engagement-manifest");
            var keyPath = ArgumentValue(args, "--owner-public-key");
            if (keyPath is null) return CommandDeskResult.UsageError("proposal submission requires --owner-public-key");
            var ownerKeyPem = await File.ReadAllTextAsync(keyPath, CancellationToken.None);
            var trustValidation = EngagementManifestFile.Validate(engagementManifest, ownerKeyPem);
            if (!trustValidation.IsValid) return CommandDeskResult.Failure("engagement authorization is invalid", trustValidation.Errors.ToArray());
            var proposalText = await File.ReadAllTextAsync(path, CancellationToken.None);
            if (!ActionProposalParser.TryParse(proposalText, out var action, out var parseReason) || action is null)
                return CommandDeskResult.Failure("model proposal was rejected", parseReason ?? "invalid action request");
            var actionValidation = ActionRequestValidator.Validate(action);
            if (!actionValidation.IsValid) return CommandDeskResult.Failure("proposal failed action validation", actionValidation.Errors.ToArray());
            if (action.CapabilityRef != HttpHeaderInspectTool.CapabilityRef)
                return CommandDeskResult.Failure("proposal dispatch is unavailable for this capability", $"registered_dispatch={HttpHeaderInspectTool.CapabilityRef}");

            var capabilities = CreateDeskCapabilities();
            var policy = new PolicyEngine(capabilities, CreateOwnerTrustStore(engagementManifest, ownerKeyPem)).Evaluate(action, engagementManifest, null);
            if (policy.Decision != PolicyDecision.Allow)
                return CommandDeskResult.Failure("proposal blocked by policy", policy.Reason, $"action_hash={policy.ActionHash}");
            if (!Uri.TryCreate(action.TargetRef, UriKind.Absolute, out var targetUri) || targetUri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(targetUri.UserInfo))
                return CommandDeskResult.Failure("proposal target is invalid for contained HTTP inspection", action.TargetRef);

            var provider = new ProviderDescriptor(
                "desk-proposal-file",
                "operator-approved-action",
                "1.0",
                Canonicalization.Sha256Hex("desk-proposal-file:v1"),
                "local-only",
                "none",
                "typed");
            var proposal = new ProviderProposal(provider, action, Canonicalization.Sha256Hex(Canonicalization.ActionPayload(action)), TimeSpan.Zero, 0, ProviderFailureClass.None);
            var envelope = ActionEnvelopeFactory.Create(proposal);
            var policyEngine = new PolicyEngine(capabilities, CreateOwnerTrustStore(engagementManifest, ownerKeyPem));
            using var issuer = new PermitIssuer(policyEngine);
            var permit = issuer.Issue(action, engagementManifest, "desk-http-worker");
            if (!issuer.TryConsume(permit, action, engagementManifest, "desk-http-worker"))
                return CommandDeskResult.Failure("one-use execution permit could not be bound", $"action_hash={policy.ActionHash}");

            Directory.CreateDirectory(dataDir);
            var journal = new DurableEvidenceJournal(Path.Combine(dataDir, "evidence.journal"), new DurableArtifactStore(Path.Combine(dataDir, "artifacts")));
            var evidence = new EvidenceLedger(new ArtifactStore(), journal);
            var keyStore = new ProvenanceKeyStore(Path.Combine(dataDir, "keys"), createProtector(), "cyber-sop-harness");
            using var signingKey = keyStore.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
            using var provenance = new ProvenanceAuthority(new ProductIdentity(
                "cyber-sop-harness",
                "0.1.0-desk",
                Canonicalization.Sha256Hex("cyber-sop-harness-desk-build"),
                ProvenanceKeyCustody.Fingerprint(signingKey)), signingKey);
            var origin = targetUri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/";
            var toolManifest = new ToolCapabilityManifest(
                "http-header-inspect",
                "1.0",
                HttpHeaderInspectTool.CapabilityRef,
                "unprivileged",
                true,
                new[] { origin },
                new[] { "http_metadata" },
                true,
                new[] { "raw", "redacted", "observation" },
                true,
                TimeSpan.FromSeconds(15),
                64 * 1024);
            await using var adapter = new HttpHeaderInspectTool("http-header-inspect", "1.0", action.ResolvedAddresses.ToArray());
            var registry = new ToolRegistry();
            registry.Register(toolManifest, adapter);
            registry.Freeze();
            var broker = new ToolBroker(registry, evidence, issuer, provenance);
            var outcome = await broker.ExecuteAsync(envelope, engagementManifest, policy, permit, "desk-http-worker", null, CancellationToken.None);
            var provenanceVerified = provenance.Verify(outcome.Provenance, outcome.Evidence, engagementManifest);
            if (!outcome.Dispatched || !provenanceVerified)
                return CommandDeskResult.Failure("governed dispatch did not complete safely", [
                    $"status={outcome.Evidence.Status}",
                    $"failure={outcome.FailureReason ?? "none"}",
                    $"evidence={outcome.Evidence.ResultEventId}",
                    $"provenance_verified={provenanceVerified}"
                ]);

            return CommandDeskResult.Success(
                "governed HTTP inspection completed",
                $"evidence={outcome.Evidence.ResultEventId}",
                $"status={outcome.Evidence.Status}",
                $"provenance_verified={provenanceVerified}",
                $"cleanup={outcome.Evidence.CleanupResult}");
        }

        async Task<CommandDeskResult> ModelCommandAsync(CommandDeskInvocation invocation, CancellationToken cancellationToken)
        {
            var action = invocation.Arguments.ElementAtOrDefault(0)?.ToLowerInvariant() ?? "status";
            if (action == "status") return modelControl.Status(selection, deskRuntime);
            if (action == "stop")
            {
                if (deskRuntime is not null) await deskRuntime.StopAsync();
                return CommandDeskResult.Success("local model runtime stopped");
            }
            if (action is not ("pin" or "serve"))
            {
                return CommandDeskResult.UsageError("unknown model action; expected pin, serve, stop, or status");
            }

            var modelName = invocation.Arguments.ElementAtOrDefault(1) ?? ArgumentValue(args, "--model");
            var resolution = await modelControl.ResolveAsync(modelName ?? string.Empty, cancellationToken);
            if (!resolution.IsValid || resolution.Manifest is null) return resolution.Result!;
            var manifest = resolution.Manifest;
            if (action == "pin")
            {
                var acknowledged = ArgumentValue(invocation.Arguments.ToArray(), "--ack-license")?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true;
                var pinned = await modelControl.PinAsync(modelName!, acknowledged, cancellationToken);
                selection = await selectionStore.LoadAsync(cancellationToken);
                providerModel = selection?.ProviderRef ?? "none";
                return pinned;
            }

            deskRuntime ??= new LocalModelRuntime(readinessTimeout: TimeSpan.FromSeconds(180));
            await deskRuntime.StopAsync();
            var validation = await ModelRuntimeValidator.ValidateAsync(manifest, cancellationToken);
            if (!validation.IsValid) return CommandDeskResult.Failure("pinned artifacts failed verification", validation.Errors.ToArray());
            var resources = DeviceResourceGate.Check(manifest, manifest.ModelPath);
            if (!resources.IsValid) return CommandDeskResult.Failure("device failed the model resource gate", resources.Errors.ToArray());
            var portValue = ArgumentValue(invocation.Arguments.ToArray(), "--port");
            var port = portValue is not null && int.TryParse(portValue, out var parsedPort) ? parsedPort : 18080;
            var started = await deskRuntime.StartAsync(manifest, port, cancellationToken);
            if (!started.Ready || started.Identity is null) return CommandDeskResult.Failure("model runtime did not become ready", started.Error ?? "unknown");
            return CommandDeskResult.Success(
                $"model runtime ready: {started.Identity.ModelId}",
                $"endpoint={started.Endpoint}",
                $"pid={started.ProcessId}",
                $"revision={started.Identity.ModelRevision}",
                $"runtime={started.Identity.RuntimeVersion}",
                "tools=disabled bind=loopback offline=true");
        }

        CommandDeskResult ValidateEngagement()
        {
            if (engagementManifest is null) return CommandDeskResult.UsageError("engagement validate requires --engagement-manifest");
            var keyPath = ArgumentValue(args, "--owner-public-key");
            if (keyPath is null) return CommandDeskResult.UsageError("engagement validate requires --owner-public-key");
            var validation = EngagementManifestFile.Validate(engagementManifest, File.ReadAllText(keyPath));
            return validation.IsValid
                ? CommandDeskResult.Success(
                    "engagement authorization is valid",
                    $"engagement={engagementManifest.EngagementId}",
                    $"mode={engagementManifest.EngagementMode}",
                    $"expires={engagementManifest.TimeWindow.ExpiresAt:O}",
                    "no target interaction performed")
                : CommandDeskResult.Failure("engagement authorization is invalid", validation.Errors.ToArray());
        }

        CommandDeskResult ValidateProposal(string path)
        {
            if (engagementManifest is null) return CommandDeskResult.UsageError("proposal validation requires --engagement-manifest");
            var keyPath = ArgumentValue(args, "--owner-public-key");
            if (keyPath is null) return CommandDeskResult.UsageError("proposal validation requires --owner-public-key");
            var trustValidation = EngagementManifestFile.Validate(engagementManifest, File.ReadAllText(keyPath));
            if (!trustValidation.IsValid) return CommandDeskResult.Failure("engagement authorization is invalid", trustValidation.Errors.ToArray());
            var proposalText = File.ReadAllText(path);
            if (!ActionProposalParser.TryParse(proposalText, out var action, out var parseReason) || action is null)
                return CommandDeskResult.Failure("model proposal was rejected", parseReason ?? "invalid action request");
            var policy = new PolicyEngine(CreateDeskCapabilities(), CreateOwnerTrustStore(engagementManifest, File.ReadAllText(keyPath))).Evaluate(action, engagementManifest, null);
            return policy.Decision switch
            {
                PolicyDecision.Allow => CommandDeskResult.Success(
                    "proposal passed policy validation; execution custody is not wired on this platform yet",
                    $"policy={policy.PolicyRef}:{policy.PolicyVersion}",
                    $"action_hash={policy.ActionHash}",
                    $"capability={policy.CapabilityRef}"),
                PolicyDecision.ApprovalRequired => CommandDeskResult.Warning(
                    "proposal requires a valid signed approval before dispatch",
                    $"reason={policy.Reason}",
                    $"action_hash={policy.ActionHash}"),
                _ => CommandDeskResult.Failure("proposal blocked by policy", policy.Reason, $"action_hash={policy.ActionHash}")
            };
        }

        async Task<CommandDeskExecution> Handler(CommandDeskInvocation invocation, CommandDeskState state, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommandDeskResult result;
            if (invocation.Verb.Equals("model", StringComparison.OrdinalIgnoreCase))
            {
                result = await ModelCommandAsync(invocation, cancellationToken);
            }
            else if (invocation.Verb.Equals("emergency", StringComparison.OrdinalIgnoreCase)
                && (invocation.Arguments.Count == 0 || invocation.Arguments[0].Equals("stop", StringComparison.OrdinalIgnoreCase)))
            {
                if (deskRuntime is not null) await deskRuntime.StopAsync();
                result = new(1, CommandDeskSeverity.Critical, "emergency stop engaged; local inference stopped and governed workers must now be cancelled by the supervisor", Array.Empty<string>());
            }
            else
            {
                result = invocation.Verb.ToLowerInvariant() switch
                {
                    "help" => CommandDeskResult.Info("Registered verbs", registry.Verbs.Select(verb => $"{verb.Name}: {verb.Summary}").ToArray()),
                    "engagement" when invocation.Arguments.ElementAtOrDefault(0)?.Equals("validate", StringComparison.OrdinalIgnoreCase) == true => ValidateEngagement(),
                    "proposal" when invocation.Arguments.ElementAtOrDefault(0)?.Equals("validate", StringComparison.OrdinalIgnoreCase) == true =>
                        ArgumentValue(invocation.Arguments.ToArray(), "--file") is { Length: > 0 } proposalPath
                            ? ValidateProposal(Path.GetFullPath(proposalPath))
                            : CommandDeskResult.UsageError("proposal validate requires --file"),
                    "proposal" when invocation.Arguments.ElementAtOrDefault(0)?.Equals("submit", StringComparison.OrdinalIgnoreCase) == true =>
                        ArgumentValue(invocation.Arguments.ToArray(), "--file") is { Length: > 0 } submissionPath
                            ? await SubmitProposalAsync(Path.GetFullPath(submissionPath))
                            : CommandDeskResult.UsageError("proposal submit requires --file"),
                    "doctor" => DoctorResult(state, dataDir, selection?.ProviderRef, endpoint?.ToString()),
                    "emergency" when invocation.Arguments.Count == 1 && invocation.Arguments[0].Equals("status", StringComparison.OrdinalIgnoreCase) =>
                        state.EmergencyStopped
                            ? CommandDeskResult.Warning("emergency stop is engaged")
                            : CommandDeskResult.Info("emergency stop is clear"),
                    "status" => CommandDeskResult.Info(
                        $"provider={providerModel} egress={(selection?.ExternalEgressAllowed == true ? "allowed" : "denied")} endpoint={endpoint?.ToString() ?? "none"}"),
                    _ => new(3, CommandDeskSeverity.Warning, $"{invocation.Verb} is registered but its governed execution path is not wired yet", Array.Empty<string>())
                };
            }
            var nextState = result.Severity == CommandDeskSeverity.Critical ? state with { EmergencyStopped = true } : null;
            return new CommandDeskExecution(result, nextState);
        }

        var repl = new CommandDeskRepl(renderer, registry, new DelegateCommandDeskHandler(Handler), replOptions, historyDirectory);
        var command = ArgumentValue(args, "--command");
        ICommandDeskInputReader reader = command is null
            ? new ConsoleCommandDeskInputReader()
            : new TextReaderCommandDeskInputReader(new StringReader(command.Replace("\\n", "\n", StringComparison.Ordinal)));
        try
        {
            return await repl.RunAsync(Console.Out, Console.Error, reader, initialState, CancellationToken.None);
        }
        finally
        {
            if (deskRuntime is not null) await deskRuntime.StopAsync();
        }
    }

    private static CommandDeskResult DoctorResult(
        CommandDeskState state,
        string dataDir,
        string? providerRef,
        string? endpoint)
    {
        var details = new List<string>();
        try
        {
            Directory.CreateDirectory(dataDir);
            var probePath = Path.Combine(dataDir, ".doctor-write-test");
            File.WriteAllText(probePath, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            File.Delete(probePath);
            details.Add("data_dir=writeable");
        }
        catch (IOException exception)
        {
            return CommandDeskResult.Failure("data directory preflight failed: " + exception.Message, "state=" + state.ResourceHealth);
        }
        details.Add("provider=" + (providerRef ?? "none"));
        details.Add("endpoint=" + (endpoint ?? "none"));
        details.Add("resource_health=" + state.ResourceHealth);
        details.Add("emergency_stop=" + (state.EmergencyStopped ? "ENGAGED" : "clear"));
        return CommandDeskResult.Success("preflight completed; runtime/model checks require pinned manifests", details.ToArray());
    }

    private static async Task<int> StatusAsync(ModelProviderSelectionStore selectionStore, ExternalEndpointStore endpointStore)
    {
        var selection = await selectionStore.LoadAsync(CancellationToken.None);
        if (selection is null)
        {
            Console.WriteLine("NO_SELECTION");
            return 0;
        }
        var egress = selection.Kind switch
        {
            ModelProviderKind.VerifiedLocal => ProviderEgressStatus.Offline,
            ModelProviderKind.UserLocal => ProviderEgressStatus.Local,
            ModelProviderKind.ExternalApi => ProviderEgressStatus.External,
            _ => ProviderEgressStatus.Offline
        };
        Console.WriteLine("SELECTION: " + selection.SelectionId);
        Console.WriteLine("PROVIDER: " + selection.ProviderRef);
        Console.WriteLine("MODEL: " + selection.ModelRef);
        Console.WriteLine("KIND: " + selection.Kind);
        Console.WriteLine("EGRESS: " + egress.ToString().ToUpperInvariant());
        Console.WriteLine("EGRESS_ALLOWED: " + selection.ExternalEgressAllowed);
        var endpoint = await endpointStore.LoadAsync(CancellationToken.None);
        Console.WriteLine("ENDPOINT: " + (endpoint is null ? "NONE" : endpoint.ToString()));
        return 0;
    }

    private static async Task<int> EndpointAsync(ExternalEndpointStore endpointStore, string[] args)
    {
        var action = args.ElementAtOrDefault(1) ?? "show";
        switch (action)
        {
            case "set":
                var value = args.ElementAtOrDefault(2);
                if (value is null) throw new InvalidOperationException("endpoint set requires a URL");
                if (!ExternalEndpointValidator.TryValidate(value, out var endpoint, out var reason) || endpoint is null) throw new InvalidOperationException(reason);
                await endpointStore.SaveAsync(endpoint, CancellationToken.None);
                Console.WriteLine("ENDPOINT_SET: " + endpoint);
                return 0;
            case "clear":
                await endpointStore.ClearAsync(CancellationToken.None);
                Console.WriteLine("ENDPOINT_CLEARED");
                return 0;
            case "show":
                var current = await endpointStore.LoadAsync(CancellationToken.None);
                Console.WriteLine("ENDPOINT: " + (current is null ? "NONE" : current.ToString()));
                return 0;
            default:
                throw new InvalidOperationException("unknown endpoint action; expected set, clear, or show");
        }
    }

    private static async Task<int> SecretAsync(PersistentSecretStore secrets, string[] args)
    {
        var action = args.ElementAtOrDefault(1) ?? "help";
        var providerId = args.ElementAtOrDefault(2);
        if (providerId is null) throw new InvalidOperationException("provider id is required for secret " + action);
        switch (action)
        {
            case "set":
                Console.Write("Secret value (hidden input): ");
                var secret = ReadSecretLine();
                secrets.Store(providerId, secret);
                Console.WriteLine("SECRET_STORED: " + providerId);
                return 0;
            case "clear":
                secrets.Delete(providerId);
                Console.WriteLine("SECRET_CLEARED: " + providerId);
                return 0;
            case "has":
                Console.WriteLine(secrets.Exists(providerId) ? "SECRET_PRESENT: " + providerId : "SECRET_ABSENT: " + providerId);
                return 0;
            default:
                throw new InvalidOperationException("unknown secret action; expected set, clear, or has");
        }
    }

    private static async Task<int> RunAsync(PersistentSecretStore secrets, ModelProviderSelectionStore selectionStore, string[] args, string dataDir)
    {
        var selection = await selectionStore.LoadAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("no provider selection; run setup first");
        var manifests = await StagedModelCatalog.LoadAsync(Path.Combine(dataDir, "..", "models"), CancellationToken.None);
        var consents = new Dictionary<string, ExternalEgressConsent>(StringComparer.Ordinal);
        if (selection.Kind == ModelProviderKind.ExternalApi && selection.ExternalEgressAllowed)
        {
            consents[selection.ProviderRef] = new ExternalEgressConsent("cli-" + selection.SelectionId, selection.ProviderRef, DateTimeOffset.UtcNow, "acknowledged during setup");
        }
        var bootstrapper = new HarnessBootstrapper(selectionStore, secrets, manifests, consents);
        var port = int.TryParse(ArgumentValue(args, "--port"), out var parsed) ? parsed : 18080;
        var telemetry = args.Contains("--telemetry", StringComparer.OrdinalIgnoreCase);
        var baseline = telemetry ? await ResourceTelemetry.SampleAsync(null, CancellationToken.None) : null;
        await using var session = await bootstrapper.StartAsync(port, CancellationToken.None);
        Console.WriteLine("READY: " + session.Selection.SelectionId + " " + session.Selection.ProviderRef + " kind=" + session.Selection.Kind);
        if (telemetry)
        {
            var ready = await ResourceTelemetry.SampleAsync(session.LocalProcessId, CancellationToken.None);
            Console.WriteLine("TELEMETRY_READY: ws=" + FormatBytes(ready?.WorkingSetBytes ?? 0) + " vram=" + FormatBytes(ready?.VramUsedBytes ?? 0) + "/" + FormatBytes(ready?.VramTotalBytes ?? 0) + " gpu=" + (ready?.GpuUtilizationPercent ?? 0).ToString("F1") + "%");
        }
        using var key = RSA.Create(2048);
        var probe = await session.Provider.ProposeAsync("Return only one JSON object with these exact synthetic fixture values: type ACTION_REQUEST, run_id cli-probe, action_id cli-probe-action, phase phase3, target_ref http://127.0.0.1:8080/, capability_ref fixture.inspect, arguments {mode: safe}, purpose exercise the selected provider, expected_observation fixture response, risk_class R0, scope_ref scope-cli, authorization_ref cli-probe-auth, methodology_refs [phase3-fixture-v1], approval_ref null, credential_ref null, resolved_addresses []. Do not use markdown.", CreateProbeAuthorization(key), CancellationToken.None);
        Console.WriteLine("PROBE: " + probe.FailureClass + " action=" + (probe.FailureClass == ProviderFailureClass.None ? probe.Action.ActionId : "n/a") + " latency=" + probe.Latency.TotalSeconds.ToString("F1") + "s tokens=" + probe.TokenUsage);
        if (probe.FailureClass == ProviderFailureClass.None)
        {
            await ExecuteGovernedAsync(probe, session, key, secrets, dataDir, CancellationToken.None);
        }
        if (telemetry)
        {
            var duringProbe = await ResourceTelemetry.SampleAsync(session.LocalProcessId, CancellationToken.None);
            Console.WriteLine("TELEMETRY_PROBE: ws=" + FormatBytes(duringProbe?.WorkingSetBytes ?? 0) + " vram=" + FormatBytes(duringProbe?.VramUsedBytes ?? 0) + "/" + FormatBytes(duringProbe?.VramTotalBytes ?? 0) + " gpu=" + (duringProbe?.GpuUtilizationPercent ?? 0).ToString("F1") + "%");
        }
        Console.WriteLine("STOPPED: " + session.Selection.SelectionId);
        if (telemetry)
        {
            var after = await ResourceTelemetry.SampleAsync(null, CancellationToken.None);
            Console.WriteLine("TELEMETRY_STOPPED: baseline_ws=" + FormatBytes(baseline?.WorkingSetBytes ?? 0) + " stopped_ws=" + FormatBytes(after?.WorkingSetBytes ?? 0) + " vram=" + FormatBytes(after?.VramUsedBytes ?? 0) + "/" + FormatBytes(after?.VramTotalBytes ?? 0));
        }
        return 0;
    }

    private static async Task ExecuteGovernedAsync(ProviderProposal probe, RuntimeSession session, RSA authorizationKey, PersistentSecretStore secrets, string dataDir, CancellationToken cancellationToken)
    {
        var action = probe.Action;
        var validation = ActionRequestValidator.Validate(action);
        if (!validation.IsValid) throw new InvalidOperationException("proposal failed action validation: " + string.Join("; ", validation.Errors));
        var authorization = CreateProbeAuthorization(authorizationKey);
        var capabilities = new CapabilityRegistry();
        capabilities.Register(new CapabilityManifest("fixture.inspect", RiskClass.R0, new[] { "http://127.0.0.1:8080/" }, "unprivileged", true, Array.Empty<string>(), new[] { "synthetic" }, TimeSpan.FromSeconds(10), 1024, false, true));
        capabilities.Freeze();
        var trustStore = new AuthorizationTrustStore();
        trustStore.Register("owner-1", authorizationKey);
        trustStore.Register("operator-1", authorizationKey);
        trustStore.Freeze();
        var policyEngine = new PolicyEngine(capabilities, trustStore);
        var policy = policyEngine.Evaluate(action, authorization, null);
        Console.WriteLine("POLICY: " + policy.Decision + " ref=" + policy.PolicyRef + " version=" + policy.PolicyVersion);
        if (policy.Decision != PolicyDecision.Allow)
        {
            Console.WriteLine("GOVERNED: BLOCKED by policy; no evidence written");
            return;
        }
        using var issuer = new PermitIssuer(policyEngine);
        var permit = issuer.Issue(action, authorization, "cli-worker");
        Console.WriteLine("PERMIT: " + permit.PermitId + " issued=" + issuer.TryConsume(permit, action, authorization, "cli-worker"));

        var toolAdapter = new SyntheticFixtureToolAdapter("cli-fixture-tool", "1.0", "fixture response", ToolResultStatus.Success, "fixture.observation");
        var toolManifest = new ToolCapabilityManifest("cli-fixture-tool", "1.0", action.CapabilityRef, "unprivileged", true, Array.Empty<string>(), new[] { "synthetic" }, true, new[] { "raw", "redacted", "observation" }, true, TimeSpan.FromSeconds(5), 1024);
        var registry = new ToolRegistry();
        registry.Register(toolManifest, toolAdapter);
        registry.Freeze();

        var artifactsDir = Path.Combine(dataDir, "artifacts");
        Directory.CreateDirectory(artifactsDir);
        var journal = new DurableEvidenceJournal(Path.Combine(dataDir, "evidence.journal"), new DurableArtifactStore(artifactsDir));
        var evidence = new EvidenceLedger(new ArtifactStore(), journal);
        var audit = new WorkflowAuditLog(journal);
        var keyStore = new ProvenanceKeyStore(Path.Combine(dataDir, "keys"), secrets.Protector, "cyber-sop-harness");
        using var signingKey = keyStore.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
        using var provenance = new ProvenanceAuthority(new ProductIdentity("cyber-sop-harness", "0.1.0-cli", Canonicalization.Sha256Hex("cyber-sop-harness-cli-build"), ProvenanceKeyCustody.Fingerprint(signingKey)), signingKey);
        var redactor = new OutputRedactor(await LoadConfiguredSecretsAsync(secrets, session.Selection, cancellationToken));
        var broker = new ToolBroker(registry, evidence, issuer, provenance, redactor);
        var envelope = ActionEnvelopeFactory.Create(probe);
        var outcome = await broker.ExecuteAsync(envelope, authorization, policy, permit, "cli-worker", null, cancellationToken);
        Console.WriteLine("DISPATCH: " + (outcome.Dispatched ? "executed" : "blocked") + " evidence=" + outcome.Evidence.ResultEventId + " status=" + outcome.Evidence.Status + " failure=" + (outcome.FailureReason ?? "none"));
        Console.WriteLine("PROVENANCE: " + outcome.Provenance.EvidenceEventId + " verified=" + provenance.Verify(outcome.Provenance, outcome.Evidence, authorization));

        var run = new WorkflowRun(action.RunId, action.ActionId, envelope.ActionHash);
        var machine = new WorkflowStateMachine(evidence, audit);
        await PrintTransitionAsync(machine, run, WorkflowState.Planned, null, null, null);
        await PrintTransitionAsync(machine, run, WorkflowState.Proposed, null, null, null);
        await PrintTransitionAsync(machine, run, WorkflowState.Allowed, outcome.Evidence.ResultEventId, null, null);
        await PrintTransitionAsync(machine, run, WorkflowState.Running, outcome.Evidence.ResultEventId, null, null);
        await PrintTransitionAsync(machine, run, WorkflowState.Observed, outcome.Evidence.ResultEventId, null, null);
        var verifier = new IndependentFixtureVerifier(evidence, audit);
        var verification = verifier.Verify(outcome.Evidence.ResultEventId, Encoding.UTF8.GetBytes("fixture response"), "fixture.observation");
        Console.WriteLine("VERIFIED: " + verification.VerificationEventId + " passed=" + verification.Passed);
        await PrintTransitionAsync(machine, run, WorkflowState.Verified, outcome.Evidence.ResultEventId, verification, null);
        var finding = new FindingRecord("finding-" + action.RunId, run.RunId, run.ActionId, run.ActionHash);
        var lifecycle = new FindingLifecycle(evidence, audit);
        lifecycle.TryAdvance(finding, FindingState.Candidate);
        lifecycle.TryAdvance(finding, FindingState.Reproducible, outcome.Evidence.ResultEventId, verification.VerificationEventId);
        lifecycle.TryAdvance(finding, FindingState.Verified, outcome.Evidence.ResultEventId, verification.VerificationEventId);
        var report = new ReportPolicy(evidence, audit).Decide(finding);
        Console.WriteLine("REPORT: " + report.ReportEventId + " allowed=" + report.Allowed + " (" + report.Reason + ")");
        lifecycle.TryAdvance(finding, FindingState.Reportable, outcome.Evidence.ResultEventId, verification.VerificationEventId, report.ReportEventId);
        await PrintTransitionAsync(machine, run, WorkflowState.Reportable, outcome.Evidence.ResultEventId, verification, report);

        using (var reopened = new DurableEvidenceJournal(Path.Combine(dataDir, "evidence.journal"), new DurableArtifactStore(artifactsDir)))
        {
            var recovered = reopened.Recover();
            Console.WriteLine("JOURNAL: " + recovered.Status + " events=" + recovered.Events.Count + " audit=" + recovered.AuditEntries.Count);
        }
    }

    private static async Task PrintTransitionAsync(WorkflowStateMachine machine, WorkflowRun run, WorkflowState target, string? evidenceEventId, VerificationRecord? verification, ReportDecision? report)
    {
        var result = machine.Transition(run, target, evidenceEventId, verification, report);
        Console.WriteLine("WORKFLOW: " + run.State + " (" + (result.Allowed ? "accepted" : result.Reason) + ")");
    }

    private static async Task<IReadOnlyList<string>> LoadConfiguredSecretsAsync(PersistentSecretStore secrets, ModelProviderSelection selection, CancellationToken cancellationToken)
    {
        if (selection.Kind != ModelProviderKind.ExternalApi || selection.SecretHandleRef is null) return Array.Empty<string>();
        return new[] { secrets.Load(selection.SecretHandleRef) };
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024L * 1024L => (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GiB",
        >= 1024L * 1024L => (bytes / (1024.0 * 1024.0)).ToString("F1") + " MiB",
        _ => bytes + " B"
    };

    private static AuthorizationManifest CreateProbeAuthorization(RSA key)
    {
        var now = DateTimeOffset.UtcNow;
        var draft = new AuthorizationManifest
        {
            EngagementId = "cli-probe",
            EngagementMode = EngagementMode.Fixture,
            Authorization = new AuthorizationProof("owner-1", "operator-1", "cli-probe-auth", string.Empty, string.Empty, string.Empty),
            Scope = new ScopeDefinition(new[] { "127.0.0.1" }, Array.Empty<string>(), "single-level", "same-origin", "block"),
            TimeWindow = new TimeWindow(now.AddMinutes(-1), now.AddMinutes(10), "UTC", Array.Empty<ExcludedWindow>()),
            Methods = new MethodDefinition(new[] { "fixture.inspect" }, Array.Empty<string>()),
            AssetCriticality = new AssetCriticalityDefinition("unknown", new Dictionary<string, string>()),
            DataHandling = new DataHandlingDefinition("synthetic-only", "required", "phase"),
            EscalationContacts = new[] { new EscalationContact("owner", "email", "owner@example.invalid") },
            CredentialPolicy = new CredentialPolicy(Array.Empty<string>(), false, "short-lived"),
            RateLimits = new RateLimitDefinition(2, 1, 1024),
            Cleanup = new CleanupDefinition(true, "operator-1", "cli-probe-cleanup"),
            StopConditions = new[] { "scope-mismatch", "relay-loss" }
        };
        return draft with { Authorization = AuthorizationSigner.Sign(draft, key) };
    }

    private static CapabilityRegistry CreateDeskCapabilities()
    {
        var registry = new CapabilityRegistry();
        registry.Register(new CapabilityManifest(
            "fixture.inspect",
            RiskClass.R0,
            new[] { "http://127.0.0.1:8080/" },
            "unprivileged",
            true,
            Array.Empty<string>(),
            new[] { "synthetic" },
            TimeSpan.FromSeconds(10),
            1024,
            false,
            true));
        registry.Register(new CapabilityManifest(
            HttpHeaderInspectTool.CapabilityRef,
            RiskClass.R0,
            new[] { "*" },
            "unprivileged",
            true,
            new[] { "https://policy-gated.invalid" },
            new[] { "http_metadata" },
            TimeSpan.FromSeconds(15),
            64 * 1024,
            false,
            true));
        registry.Freeze();
        return registry;
    }

    private static AuthorizationTrustStore CreateOwnerTrustStore(AuthorizationManifest manifest, string publicKeyPem)
    {
        using var publicKey = RSA.Create();
        publicKey.ImportFromPem(publicKeyPem);
        var trustStore = new AuthorizationTrustStore();
        trustStore.Register(manifest.Authorization.Owner, publicKey);
        trustStore.Freeze();
        return trustStore;
    }

    private static async Task<IReadOnlyList<ProviderDisclosure>> BuildDisclosuresAsync(string dataDir, PersistentSecretStore secrets, ExternalEndpointStore endpointStore)
    {
        var result = new List<ProviderDisclosure>();
        var modelsDir = Path.Combine(dataDir, "..", "models");
        if (Directory.Exists(modelsDir))
        {
            foreach (var modelDirectory in Directory.EnumerateDirectories(modelsDir).Order(StringComparer.Ordinal))
            {
                var gguf = Directory.EnumerateFiles(modelDirectory, "*.gguf").FirstOrDefault();
                if (gguf is null) continue;
                var name = Path.GetFileName(modelDirectory);
                result.Add(new ProviderDisclosure(name, name, Path.GetFileNameWithoutExtension(gguf), "local", "bundled/local", "notice preserved; redistribution pending review", gguf, "local-only; no retention", "resource estimate from runtime manifest", ProviderEgressStatus.Local));
            }
        }
        var externalProvider = "external-api";
        var externalEndpoint = await endpointStore.LoadAsync(CancellationToken.None);
        if (secrets.Exists(externalProvider) && externalEndpoint is not null)
        {
            result.Add(new ProviderDisclosure(externalProvider, "External API", "external-model", "remote", "user-configured endpoint", "unknown", externalEndpoint.ToString(), "remote retention; data leaves host", "network", ProviderEgressStatus.External));
        }
        return result;
    }

    private static string ReadSecretLine()
    {
        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && builder.Length > 0) builder.Length--;
            else if (key.KeyChar != '\0') builder.Append(key.KeyChar);
        }
        Console.WriteLine();
        return builder.ToString();
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("cyber-sop-harness commands:");
        Console.WriteLine("  setup [--provider <id>] [--ack-egress yes|no] [--data-dir <dir>]");
        Console.WriteLine("  status [--data-dir <dir>]");
        Console.WriteLine("  secret set|clear|has <providerId> [--data-dir <dir>]");
        Console.WriteLine("  endpoint set|clear|show <url> [--data-dir <dir>]");
        Console.WriteLine("  desk [--command <input>] [--engagement <label>] [--scope <ref>] [--risk R0-R4]");
        Console.WriteLine("       [--json] [--compact] [--no-color] [--no-history] [--data-dir <dir>]");
        Console.WriteLine("  run [--port <n>] [--telemetry] [--data-dir <dir>]");
        Console.WriteLine("  --help");
    }
}
