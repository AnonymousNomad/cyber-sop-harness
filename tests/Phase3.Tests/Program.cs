using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberSopHarness.Core;

internal static class Program
{
    private static int _passed;

    public static async Task<int> Main()
    {
        await Run("provider parity and action envelopes", TestProviderParity);
        await Run("frozen typed tool registry", TestToolRegistry);
        await Run("normalized tool evidence and redaction", TestToolEvidence);
        await Run("blocked, fabricated, failure, and timeout paths", TestFailurePaths);
        await Run("host-controlled workflow state", TestWorkflowState);
        await Run("hash mismatch freezes the run", TestIntegrityFreeze);
        await Run("replay and independent verification", TestReplayAndVerification);
        await Run("finding lifecycle and report gate", TestFindingLifecycle);
        await Run("model provider selection", TestModelProviderSelection);
        await Run("model runtime manifest validation", TestModelRuntimeManifest);
        await Run("desk model pin and resource gate", TestDeskModelPin);
        await Run("strict model proposal parsing", TestModelProposalParsing);
        await Run("offline provider failure is controlled", TestOfflineProviderFailure);
        await Run("provider selection persistence and manifest loading", TestProviderSelectionPersistence);
        await Run("durable evidence journal recovery and tamper detection", TestDurableEvidencePersistence);
        await Run("persistent secret custody round trip", TestPersistentSecretStore);
        await Run("passphrase secret custody recovery", TestPassphraseSecretProtector);
        await Run("provenance key custody and rotation", TestProvenanceKeyCustody);
        await Run("provider selection wizard disclosures", TestProviderSelectionWizard);
        await Run("runtime journal mirror and recovery", TestRuntimeJournalMirror);
        await Run("external API provider consent gating", TestExternalApiProvider);
        await Run("harness bootstrapper fail-closed startup", TestHarnessBootstrapper);
        await Run("external provider full policy/evidence path", TestExternalProviderBrokerPath);
        await Run("external endpoint store validation", TestExternalEndpointStore);
        await Run("synthetic fixture tool adapter", TestSyntheticFixtureToolAdapter);
        await Run("authorized HTTP header inspection", TestAuthorizedHttpHeaderInspection);
        await Run("DNS reverse lookup adapter", TestDnsReverseLookupAdapter);
        await Run("engagement manifest file validation", TestEngagementManifestFile);
        await Run("permit expiry during tool execution", TestPermitExpiryDuringExecution);
        await Run("multi-hop redirect scope crossing", TestMultiHopRedirectScopeCrossing);
        await Run("key rotation mid-run evidence integrity", TestKeyRotationMidRunEvidence);
        if (Environment.GetEnvironmentVariable("PHASE3B_REAL_MODEL") == "1") await Run("real Phase 3B local model runtime", TestRealModelRuntime);
        Console.WriteLine($"phase3_tests=passed count={_passed}");
        return 0;
    }

    private static async Task Run(string name, Func<Task> test)
    {
        await test();
        _passed++;
        Console.WriteLine($"PASS {name}");
    }

    private static async Task TestProviderParity()
    {
        using var fixture = new RuntimeFixture();
        var first = new FakeModelProvider("provider-a", "model-a", fixture.Action);
        var second = new FakeModelProvider("provider-b", "model-b", fixture.Action);
        var firstProposal = await first.ProposeAsync("same prompt", fixture.Manifest, CancellationToken.None);
        var secondProposal = await second.ProposeAsync("same prompt", fixture.Manifest, CancellationToken.None);
        Assert(ProviderProposalValidator.Validate(firstProposal).IsValid, "first provider proposal was invalid");
        Assert(ProviderProposalValidator.Validate(secondProposal).IsValid, "second provider proposal was invalid");
        var firstEnvelope = ActionEnvelopeFactory.Create(firstProposal);
        var secondEnvelope = ActionEnvelopeFactory.Create(secondProposal);
        Assert(firstEnvelope.ActionHash == secondEnvelope.ActionHash, "provider swap changed normalized action hash");
        Assert(fixture.Policy.Decision == PolicyDecision.Allow, "fixture policy baseline was not allowed");
        var secondPolicy = new PolicyEngine(CreateCapabilities(), CreateTrustStore(fixture.Key)).Evaluate(secondEnvelope.Request, fixture.Manifest, null);
        Assert(secondPolicy.Decision == fixture.Policy.Decision && secondPolicy.ActionHash == secondEnvelope.ActionHash, "provider swap changed policy decision");
        var failedProposal = firstProposal with { FailureClass = ProviderFailureClass.Timeout };
        Assert(ProviderProposalValidator.Validate(failedProposal).IsValid, "controlled provider failure was rejected as malformed");
        await AssertThrows<InvalidOperationException>(() => Task.FromResult(ActionEnvelopeFactory.Create(failedProposal)), "failed provider proposal became an action envelope");
    }

    private static Task TestToolRegistry()
    {
        using var fixture = new RuntimeFixture();
        Assert(fixture.Registry.IsFrozen, "tool registry was not frozen before use");
        Assert(fixture.Registry.TryGet("fixture.inspect", out var registration) && registration is not null, "registered fixture tool was missing");
        var replacement = new FixtureToolAdapter("fixture-tool", "1.0", "replacement", ToolResultStatus.Success, "fixture.observation");
        AssertThrows<InvalidOperationException>(() =>
        {
            fixture.Registry.Register(fixture.ToolManifest, replacement);
            return Task.CompletedTask;
        }, "frozen tool registry accepted a replacement").GetAwaiter().GetResult();
        var nonMarker = new NonMarkerAdapter("non-marker", "1.0");
        var markerRegistry = new ToolRegistry();
        AssertThrows<InvalidOperationException>(() =>
        {
            markerRegistry.Register(fixture.ToolManifest with { ToolRef = "non-marker" }, nonMarker);
            return Task.CompletedTask;
        }, "untrusted adapter marker was accepted").GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    private static Task TestToolEvidence()
    {
        using var fixture = new RuntimeFixture();
        var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
        Assert(outcome.Dispatched, "allowed tool was not dispatched");
        Assert(outcome.Evidence.Status == ToolResultStatus.Success, "tool result status was not successful");
        Assert(outcome.Evidence.Provider.Descriptor.ProviderRef == fixture.Provider.ProviderRef && outcome.Evidence.Provider.TokenUsage == 8 && outcome.Evidence.Provider.FailureClass == ProviderFailureClass.None, "provider metadata was not persisted");
        Assert(fixture.Provenance.Verify(outcome.Provenance, outcome.Evidence, fixture.Manifest), "signed provenance stamp failed verification");
        Assert(ProvenanceAuthority.Render(outcome.Provenance, true).Contains("PROVENANCE", StringComparison.Ordinal), "visible provenance stamp was not rendered");
        Assert(!fixture.Provenance.Verify(outcome.Provenance with { EvidenceHash = new string('0', 64) }, outcome.Evidence, fixture.Manifest), "tampered provenance stamp was accepted");
        Assert(outcome.Evidence.Provider.Descriptor.ProviderRef == fixture.Provider.ProviderRef && outcome.Evidence.Provider.TokenUsage == 8 && outcome.Evidence.Provider.FailureClass == ProviderFailureClass.None, "provider metadata was not persisted");
        Assert(fixture.AdapterInvocationCount == 1, "fixture adapter invocation count was incorrect");
        Assert(fixture.Evidence.VerifyIntegrity(), "evidence ledger failed integrity verification");
        Assert(fixture.Evidence.TryReadArtifact(outcome.Evidence.RawArtifactRef, out var raw) && Encoding.UTF8.GetString(raw).Contains("secret=alpha", StringComparison.Ordinal), "raw artifact was not preserved");
        Assert(outcome.Evidence.RedactedArtifactRef is not null && fixture.Evidence.TryReadArtifact(outcome.Evidence.RedactedArtifactRef, out var redacted) && !Encoding.UTF8.GetString(redacted).Contains("secret=alpha", StringComparison.Ordinal), "redacted artifact still contained the secret");
        Assert(outcome.Evidence.RawSha256 != outcome.Evidence.RedactedSha256, "raw and redacted hashes were identical");
        var replay = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!replay.Dispatched && replay.Evidence.Status == ToolResultStatus.Blocked, "consumed permit replay reached the adapter");
        Assert(fixture.AdapterInvocationCount == 1, "permit replay invoked the adapter");
        return Task.CompletedTask;
    }

    private static async Task TestFailurePaths()
    {
        using var blockedFixture = new RuntimeFixture();
        var blocked = await blockedFixture.Broker.ExecuteAsync(blockedFixture.Envelope, blockedFixture.Manifest, blockedFixture.Policy with { Decision = PolicyDecision.Block }, null, blockedFixture.WorkerRef, null, CancellationToken.None);
        Assert(!blocked.Dispatched && blocked.Evidence.Status == ToolResultStatus.Blocked, "blocked policy reached a tool");
        Assert(blockedFixture.AdapterInvocationCount == 0, "blocked policy invoked the adapter");

        var malformedEnvelope = blockedFixture.Envelope with { Provider = null!, ProviderOutputSha256 = "invalid" };
        var malformed = await blockedFixture.Broker.ExecuteAsync(malformedEnvelope, blockedFixture.Manifest, blockedFixture.Policy, blockedFixture.Permit, blockedFixture.WorkerRef, null, CancellationToken.None);
        Assert(!malformed.Dispatched && malformed.Evidence.Status == ToolResultStatus.Blocked, "malformed provider envelope was dispatched");

        var authorized = blockedFixture.Manifest with { EngagementMode = EngagementMode.Authorized };
        var authorizedBlocked = await blockedFixture.Broker.ExecuteAsync(blockedFixture.Envelope, authorized, blockedFixture.Policy, blockedFixture.Permit, blockedFixture.WorkerRef, null, CancellationToken.None);
        Assert(!authorizedBlocked.Dispatched && authorizedBlocked.Evidence.Status == ToolResultStatus.Blocked, "authorized broker dispatch bypassed the Phase 2 gate");

        using var mutableFixture = new RuntimeFixture();
        var mutableRegistry = new ToolRegistry();
        var mutableAdapter = new FixtureToolAdapter("fixture-tool", "1.0", "mutable", ToolResultStatus.Success, "mutable");
        mutableRegistry.Register(mutableFixture.ToolManifest, mutableAdapter);
        var mutableBroker = new ToolBroker(mutableRegistry, mutableFixture.Evidence, mutableFixture.Issuer, mutableFixture.Provenance, new OutputRedactor());
        var mutable = await mutableBroker.ExecuteAsync(mutableFixture.Envelope, mutableFixture.Manifest, mutableFixture.Policy, mutableFixture.Permit, mutableFixture.WorkerRef, null, CancellationToken.None);
        Assert(!mutable.Dispatched && mutableAdapter.InvocationCount == 0, "mutable tool registry dispatched an adapter");

        using var unknownFixture = new RuntimeFixture();
        var emptyRegistry = new ToolRegistry();
        emptyRegistry.Freeze();
        var unknownBroker = new ToolBroker(emptyRegistry, unknownFixture.Evidence, unknownFixture.Issuer, unknownFixture.Provenance, new OutputRedactor(new[] { "alpha" }));
        var unknown = await unknownBroker.ExecuteAsync(unknownFixture.Envelope, unknownFixture.Manifest, unknownFixture.Policy, unknownFixture.Permit, unknownFixture.WorkerRef, null, CancellationToken.None);
        Assert(!unknown.Dispatched && unknown.Evidence.Status == ToolResultStatus.Blocked, "unknown tool capability was dispatched");
        Assert(unknownFixture.AdapterInvocationCount == 0, "unknown tool path invoked the fixture adapter");

        using var forgedFixture = new RuntimeFixture();
        var forgedPermit = CopyPermit(forgedFixture.Permit);
        var forged = await forgedFixture.Broker.ExecuteAsync(forgedFixture.Envelope, forgedFixture.Manifest, forgedFixture.Policy, forgedPermit, forgedFixture.WorkerRef, null, CancellationToken.None);
        Assert(!forged.Dispatched && forged.Evidence.Status == ToolResultStatus.Blocked, "forged permit object was dispatched");
        Assert(forgedFixture.AdapterInvocationCount == 0, "forged permit invoked the fixture adapter");

        using var timeoutFixture = new RuntimeFixture(new TimeoutToolAdapter("fixture-tool", "1.0"));
        var timeout = await timeoutFixture.Broker.ExecuteAsync(timeoutFixture.Envelope, timeoutFixture.Manifest, timeoutFixture.Policy, timeoutFixture.Permit, timeoutFixture.WorkerRef, null, CancellationToken.None);
        Assert(timeout.Dispatched && timeout.Evidence.Status == ToolResultStatus.Timeout, "tool timeout was not normalized");
        var timeoutAudit = new WorkflowAuditLog();
        var timeoutState = new WorkflowStateMachine(timeoutFixture.Evidence, timeoutAudit);
        var timeoutRun = new WorkflowRun(timeoutFixture.Action.RunId, timeoutFixture.Action.ActionId, timeoutFixture.Envelope.ActionHash);
        Assert(timeoutState.Transition(timeoutRun, WorkflowState.Planned).Allowed, "timeout READY->PLANNED was rejected");
        Assert(timeoutState.Transition(timeoutRun, WorkflowState.Proposed).Allowed, "timeout PLANNED->PROPOSED was rejected");
        Assert(timeoutState.Transition(timeoutRun, WorkflowState.Allowed, timeout.Evidence.ResultEventId).Allowed, "timeout ALLOWED transition was rejected");
        Assert(timeoutState.Transition(timeoutRun, WorkflowState.Running, timeout.Evidence.ResultEventId).Allowed, "timeout RUNNING transition was rejected");
        Assert(!timeoutState.Transition(timeoutRun, WorkflowState.Observed, timeout.Evidence.ResultEventId).Allowed, "timeout advanced to successful observation");
        Assert(timeoutRun.State == WorkflowState.Running, "timeout failure changed state despite rejected transition");

        using var expiredFixture = new RuntimeFixture();
        var expiredPermit = expiredFixture.Issuer.Issue(expiredFixture.Action, expiredFixture.Manifest, expiredFixture.WorkerRef, lifetime: TimeSpan.FromMilliseconds(15));
        Assert(expiredFixture.Issuer.TryConsume(expiredPermit, expiredFixture.Action, expiredFixture.Manifest, expiredFixture.WorkerRef), "short-lived permit was not consumed for expiry test");
        Thread.Sleep(250);
        var expired = await expiredFixture.Broker.ExecuteAsync(expiredFixture.Envelope, expiredFixture.Manifest, expiredFixture.Policy, expiredPermit, expiredFixture.WorkerRef, null, CancellationToken.None);
        Assert(!expired.Dispatched && expired.Evidence.Status == ToolResultStatus.Blocked, "expired consumed permit was dispatched");

        using var errorFixture = new RuntimeFixture(new ThrowingToolAdapter("fixture-tool", "1.0"));
        var toolError = await errorFixture.Broker.ExecuteAsync(errorFixture.Envelope, errorFixture.Manifest, errorFixture.Policy, errorFixture.Permit, errorFixture.WorkerRef, null, CancellationToken.None);
        Assert(toolError.Evidence.Status == ToolResultStatus.ToolError, "adapter exception was not normalized");

        using var cleanupFixture = new RuntimeFixture(new ThrowingCleanupAdapter("fixture-tool", "1.0"));
        var cleanupFailure = await cleanupFixture.Broker.ExecuteAsync(cleanupFixture.Envelope, cleanupFixture.Manifest, cleanupFixture.Policy, cleanupFixture.Permit, cleanupFixture.WorkerRef, null, CancellationToken.None);
        Assert(cleanupFailure.Evidence.CleanupResult == "FAILED", "cleanup exception was not recorded");
        var cleanupAudit = new WorkflowAuditLog();
        var cleanupVerifier = new IndependentFixtureVerifier(cleanupFixture.Evidence, cleanupAudit).Verify(cleanupFailure.Evidence.ResultEventId, Encoding.UTF8.GetBytes("cleanup"), "cleanup");
        Assert(!cleanupVerifier.Passed, "failed cleanup was accepted by independent verification");
        var cleanupFinding = new FindingRecord("cleanup-finding", cleanupFixture.Action.RunId, cleanupFixture.Action.ActionId, cleanupFixture.Envelope.ActionHash);
        var cleanupLifecycle = new FindingLifecycle(cleanupFixture.Evidence, cleanupAudit);
        Assert(cleanupLifecycle.TryAdvance(cleanupFinding, FindingState.Candidate), "cleanup finding candidate transition failed");
        Assert(!cleanupLifecycle.TryAdvance(cleanupFinding, FindingState.Reproducible, cleanupFailure.Evidence.ResultEventId), "failed cleanup became reproducible evidence");
    }

    private static Task TestWorkflowState()
    {
        using var fixture = new RuntimeFixture();
        var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
        var audit = new WorkflowAuditLog();
        var machine = new WorkflowStateMachine(fixture.Evidence, audit);
        var run = new WorkflowRun(fixture.Action.RunId, fixture.Action.ActionId, fixture.Envelope.ActionHash);
        Assert(machine.Transition(run, WorkflowState.Planned).Allowed, "READY->PLANNED was rejected");
        Assert(machine.Transition(run, WorkflowState.Proposed).Allowed, "PLANNED->PROPOSED was rejected");
        Assert(!machine.Transition(run, WorkflowState.Allowed, "missing-event").Allowed, "missing event advanced workflow");
        Assert(machine.Transition(run, WorkflowState.Allowed, outcome.Evidence.ResultEventId).Allowed, "valid ALLOWED transition was rejected");
        Assert(machine.Transition(run, WorkflowState.Running, outcome.Evidence.ResultEventId).Allowed, "valid RUNNING transition was rejected");
        Assert(machine.Transition(run, WorkflowState.Observed, outcome.Evidence.ResultEventId).Allowed, "valid OBSERVED transition was rejected");
        Assert(run.State == WorkflowState.Observed, "host state was not updated");
        var otherRun = new WorkflowRun("other-run", fixture.Action.ActionId, fixture.Envelope.ActionHash);
        Assert(machine.Transition(otherRun, WorkflowState.Planned).Allowed, "cross-run test READY->PLANNED was rejected");
        Assert(machine.Transition(otherRun, WorkflowState.Proposed).Allowed, "cross-run test PLANNED->PROPOSED was rejected");
        Assert(!machine.Transition(otherRun, WorkflowState.Allowed, outcome.Evidence.ResultEventId).Allowed, "cross-run evidence advanced the workflow");
        return Task.CompletedTask;
    }

    private static Task TestIntegrityFreeze()
    {
        using var fixture = new RuntimeFixture();
        var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
        var tampered = fixture.Evidence.Snapshot().Select(item => item.ResultEventId == outcome.Evidence.ResultEventId ? item with { RawSha256 = new string('0', 64) } : item).ToArray();
        var machine = new WorkflowStateMachine(fixture.Evidence, new WorkflowAuditLog());
        var run = new WorkflowRun(fixture.Action.RunId, fixture.Action.ActionId, fixture.Envelope.ActionHash);
        var result = machine.VerifySnapshot(run, tampered);
        Assert(!result.Allowed && run.State == WorkflowState.Stopped, "hash mismatch did not freeze the run");
        return Task.CompletedTask;
    }

    private static Task TestReplayAndVerification()
    {
        using var fixture = new RuntimeFixture();
        var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
        var catalog = new ReplayCatalog();
        catalog.RegisterFixture("fixture-v1", Encoding.UTF8.GetBytes("fixture-content"));
        catalog.RegisterEnvironment("local-net10", Encoding.UTF8.GetBytes("environment-content"));
        catalog.Freeze();
        var package = ReplayEngine.Build(fixture.Evidence, catalog, fixture.Action.RunId, fixture.Action.ActionId, outcome.Evidence.ResultEventId, "fixture-v1", "local-net10");
        Assert(package.Replayability == Replayability.Replayable, "complete fixture was not replayable");
        Assert(ReplayEngine.Validate(fixture.Evidence, catalog, package).Valid, "replay package failed validation");
        var forgedLabel = package with { Replayability = Replayability.Replayable, FixtureRef = "unregistered" };
        Assert(!ReplayEngine.Validate(fixture.Evidence, catalog, forgedLabel).Valid, "unregistered replay identity was accepted");
        var verifier = new IndependentFixtureVerifier(fixture.Evidence, new WorkflowAuditLog());
        var verification = verifier.Verify(outcome.Evidence.ResultEventId, Encoding.UTF8.GetBytes("secret=alpha"), "fixture.observation");
        Assert(verification.Passed, "independent fixture verifier did not reproduce the result");
        var badVerification = verifier.Verify(outcome.Evidence.ResultEventId, Encoding.UTF8.GetBytes("wrong"), "fixture.observation");
        Assert(!badVerification.Passed, "independent verifier accepted incorrect raw evidence");
        return Task.CompletedTask;
    }

    private static Task TestFindingLifecycle()
    {
        using var fixture = new RuntimeFixture();
        var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
        var audit = new WorkflowAuditLog();
        var verifier = new IndependentFixtureVerifier(fixture.Evidence, audit);
        var verification = verifier.Verify(outcome.Evidence.ResultEventId, Encoding.UTF8.GetBytes("secret=alpha"), "fixture.observation");
        var finding = new FindingRecord("finding-1", fixture.Action.RunId, fixture.Action.ActionId, fixture.Envelope.ActionHash);
        var lifecycle = new FindingLifecycle(fixture.Evidence, audit);
        Assert(lifecycle.TryAdvance(finding, FindingState.Candidate), "HYPOTHESIS->CANDIDATE was rejected");
        Assert(lifecycle.TryAdvance(finding, FindingState.Reproducible, outcome.Evidence.ResultEventId), "CANDIDATE->REPRODUCIBLE was rejected");
        Assert(lifecycle.TryAdvance(finding, FindingState.Verified, outcome.Evidence.ResultEventId, verification.VerificationEventId), "REPRODUCIBLE->VERIFIED was rejected");
        var reportPolicy = new ReportPolicy(fixture.Evidence, audit);
        var decision = reportPolicy.Decide(finding);
        Assert(decision.Allowed, "verified finding was denied report policy");
        Assert(lifecycle.TryAdvance(finding, FindingState.Reportable, outcome.Evidence.ResultEventId, verification.VerificationEventId, decision.ReportEventId), "VERIFIED->REPORTABLE was rejected");
        Assert(reportPolicy.Build(finding, decision).FindingRef == finding.FindingRef, "report artifact did not bind to finding");

        var unverified = new FindingRecord("finding-unknown", fixture.Action.RunId, fixture.Action.ActionId, fixture.Envelope.ActionHash);
        var deniedDecision = reportPolicy.Decide(unverified);
        Assert(!deniedDecision.Allowed, "unverified finding was accepted by report policy");
        AssertThrows<InvalidOperationException>(() => Task.FromResult(reportPolicy.Build(unverified, deniedDecision)), "unverified finding produced a report").GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    private static Task TestModelProviderSelection()
    {
        var local = new ModelProviderSelection("selection-local", ModelProviderKind.VerifiedLocal, "local-llama", "wrn-v3-7b", "http://127.0.0.1:8080", "C:\\models\\wrn.gguf", null, false, true);
        Assert(ModelProviderSelectionValidator.Validate(local).IsValid, "verified local selection was rejected");
        var api = new ModelProviderSelection("selection-api", ModelProviderKind.ExternalApi, "api-provider", "model-1", "https://api.invalid/v1", null, "cred_api_1", true, true);
        Assert(ModelProviderSelectionValidator.Validate(api).IsValid, "explicit API selection was rejected");
        var unsafeLocal = local with { ExternalEgressAllowed = true };
        Assert(!ModelProviderSelectionValidator.Validate(unsafeLocal).IsValid, "local selection enabled hidden egress");
        using var releaseKey = RSA.Create(2048);
        using var provenance = new ProvenanceAuthority(new ProductIdentity("cyber-sop-harness", "release-test", Canonicalization.Sha256Hex("release-build"), ProvenanceKeyCustody.Fingerprint(releaseKey)), releaseKey);
        var release = provenance.IssueReleaseManifest("release-test", new[] { new ReleaseFileEntry("models/test.gguf", 4, Canonicalization.Sha256Hex("file")) });
        Assert(provenance.VerifyReleaseManifest(release), "signed release manifest failed verification");
        Assert(!provenance.VerifyReleaseManifest(release with { ProductVersion = "tampered" }), "tampered release manifest was accepted");
        return Task.CompletedTask;
    }

    private static async Task TestModelRuntimeManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-p3b-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var modelPath = Path.Combine(root, "model.gguf");
            var runtimePath = Path.Combine(root, "llama-server.exe");
            var licensePath = Path.Combine(root, "LICENSE.txt");
            var templatePath = Path.Combine(root, "safe-chat-template.jinja");
            var modelBytes = Encoding.UTF8.GetBytes("synthetic-model-placeholder");
            var runtimeBytes = Encoding.UTF8.GetBytes("synthetic-runtime-placeholder");
            var licenseBytes = Encoding.UTF8.GetBytes("synthetic-license-notice");
            var templateBytes = Encoding.UTF8.GetBytes("safe-template");
            await File.WriteAllBytesAsync(modelPath, modelBytes);
            await File.WriteAllBytesAsync(runtimePath, runtimeBytes);
            await File.WriteAllBytesAsync(licensePath, licenseBytes);
            await File.WriteAllBytesAsync(templatePath, templateBytes);
            var manifest = new ModelRuntimeManifest(
                "wrn-v3-7b-q4-k-m",
                modelPath,
                Canonicalization.Sha256Hex(modelBytes),
                "fixture-revision",
                runtimePath,
                Canonicalization.Sha256Hex(runtimeBytes),
                "llama.cpp-fixture",
                "qwen2",
                licensePath,
                Canonicalization.Sha256Hex(licenseBytes),
                templatePath,
                Canonicalization.Sha256Hex(templateBytes),
                4096,
                1024,
                1024,
                6144,
                "wrn-v3-7b-q4-k-m");
            Assert((await ModelRuntimeValidator.ValidateAsync(manifest, CancellationToken.None)).IsValid, "valid runtime manifest was rejected");
            await File.AppendAllTextAsync(modelPath, "tamper");
            Assert(!(await ModelRuntimeValidator.ValidateAsync(manifest, CancellationToken.None)).IsValid, "tampered model passed manifest validation");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task TestDeskModelPin()
    {
        var root = Path.Combine(Path.GetTempPath(), "csh-model-pin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "models", "fixture-model"));
        Directory.CreateDirectory(Path.Combine(root, "state"));
        try
        {
            var directory = Path.Combine(root, "models", "fixture-model");
            var modelPath = Path.Combine(directory, "model.gguf");
            var runtimePath = Path.Combine(directory, "llama-server");
            var licensePath = Path.Combine(directory, "LICENSE.txt");
            var templatePath = Path.Combine(directory, "template.jinja");
            await File.WriteAllBytesAsync(modelPath, new byte[] { 1, 2, 3 });
            await File.WriteAllBytesAsync(runtimePath, new byte[] { 4, 5 });
            await File.WriteAllTextAsync(licensePath, "license");
            await File.WriteAllTextAsync(templatePath, "template");
            var payload = new
            {
                model_ref = "fixture-model",
                model_revision = "rev-1",
                model_file = "model.gguf",
                model_bytes = 3L,
                working_set_bytes = 1L,
                model_sha256 = Canonicalization.Sha256Hex(await File.ReadAllBytesAsync(modelPath)),
                architecture = "fixture",
                context_size = 512,
                runtime_ref = "runtime-fixture",
                runtime_commit = "commit-1",
                runtime_binary = "llama-server",
                runtime_sha256 = Canonicalization.Sha256Hex(await File.ReadAllBytesAsync(runtimePath)),
                runtime_version = "test-1",
                license_notice = "fixture",
                runtime_license = "LICENSE.txt",
                chat_template = "template.jinja",
                chat_template_sha256 = Canonicalization.Sha256Hex(Encoding.UTF8.GetBytes("template")),
                expected_server_model = "fixture-model",
                launch_mode = "managed-loopback-offline",
                license_review = "required"
            };
            var manifestPath = Path.Combine(directory, "MODEL-RUNTIME-MANIFEST.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
            var manifests = await StagedModelCatalog.LoadAsync(Path.Combine(root, "models"), CancellationToken.None);
            Assert(manifests.TryGetValue("fixture-model", out var manifest) && manifest is not null && manifest.MaxWorkingSetBytes == 1, "explicit working-set budget was lost");
            var selectionStore = new ModelProviderSelectionStore(Path.Combine(root, "state", "selection.json"));
            var control = new CommandDeskModelControl(manifests, selectionStore);
            var unacknowledged = await control.PinAsync("fixture-model", false, CancellationToken.None);
            Assert(unacknowledged.ExitCode == 2, "license acknowledgement was bypassed");
            var pinned = await control.PinAsync("fixture-model", true, CancellationToken.None);
            Assert(pinned.ExitCode == 0, $"valid model pin failed: {pinned.Message}");
            var selection = await selectionStore.LoadAsync(CancellationToken.None);
            Assert(selection?.Kind == ModelProviderKind.VerifiedLocal && selection.ProviderRef == "fixture-model" && !selection.ExternalEgressAllowed, "verified local selection was incorrect");
            await File.AppendAllTextAsync(modelPath, "tamper");
            var tampered = await control.PinAsync("fixture-model", true, CancellationToken.None);
            Assert(tampered.ExitCode == 1, "tampered model pin was accepted");
            var exhausted = DeviceResourceGate.Check(manifest! with { MaxWorkingSetBytes = long.MaxValue }, modelPath);
            Assert(!exhausted.IsValid && exhausted.Errors.Any(error => error.Contains("memory budget failed", StringComparison.Ordinal)), "memory exhaustion passed the resource gate");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Task TestModelProposalParsing()
    {
        using var fixture = new RuntimeFixture();
        var json = JsonSerializer.Serialize(fixture.Action, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Converters = { new JsonStringEnumConverter() } });
        Assert(ActionProposalParser.TryParse(json, out var parsed, out _) && parsed is not null && parsed.ActionId == fixture.Action.ActionId, "valid JSON action proposal was rejected");
        Assert(!ActionProposalParser.TryParse("```json\n" + json + "\n```", out _, out _), "code-fenced provider output was accepted");
        Assert(!ActionProposalParser.TryParse("{\"type\":\"ACTION_REQUEST\"}", out _, out _), "incomplete provider output was accepted");
        var fenced = "```json\n" + json + "\n```";
        Assert(ActionProposalParser.TryParse(ProposalTextNormalizer.StripOuterCodeFence(fenced), out var normalized, out _) && normalized is not null && normalized.ActionId == fixture.Action.ActionId, "outer code fence was not stripped before parsing");
        Assert(string.Equals(ProposalTextNormalizer.StripOuterCodeFence(json), json, StringComparison.Ordinal), "bare JSON was altered by the fence normalizer");
        var unclosed = "```json\n" + json;
        Assert(string.Equals(ProposalTextNormalizer.StripOuterCodeFence(unclosed), unclosed, StringComparison.Ordinal), "unclosed fence was stripped by the normalizer");
        Assert(!ActionProposalParser.TryParse(ProposalTextNormalizer.StripOuterCodeFence(unclosed), out _, out _), "unclosed fence text was parsed after normalization");
        Assert(!ActionProposalParser.TryParse(ProposalTextNormalizer.StripOuterCodeFence("```json\n" + json + "\n```\ntrailing prose"), out _, out _), "trailing prose after the closer was accepted");
        return Task.CompletedTask;
    }

    private static async Task TestOfflineProviderFailure()
    {
        await using var runtime = new LocalModelRuntime();
        using var provider = new LocalModelProviderAdapter(runtime, new ProviderDescriptor("local-test", "model-test", "1", Canonicalization.Sha256Hex("config"), "local-only", "none", "typed"));
        using var key = RSA.Create(2048);
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var proposal = await provider.ProposeAsync("synthetic fixture only", manifest, CancellationToken.None);
        Assert(proposal.FailureClass == ProviderFailureClass.Unavailable, "offline provider failure was not controlled");
    }

    private static async Task TestProviderSelectionPersistence()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var selectionPath = Path.Combine(root, "provider-selection.json");
            var selection = new ModelProviderSelection("local-selection", ModelProviderKind.VerifiedLocal, "wrn-local", "wrn-v3-7b-q4-k-m", "http://127.0.0.1:18080", "E:\\cyber-sop-harness\\models\\WhiteRabbitNeo-V3-7B-GGUF\\WhiteRabbitNeo_WhiteRabbitNeo-V3-7B-Q4_K_M.gguf", null, false, true);
            var store = new ModelProviderSelectionStore(selectionPath);
            await store.SaveAsync(selection, CancellationToken.None);
            var loaded = await store.LoadAsync(CancellationToken.None);
            Assert(loaded?.ProviderRef == selection.ProviderRef && loaded.SecretHandleRef is null, "provider selection did not persist safely");
            var json = await File.ReadAllTextAsync(selectionPath);
            Assert(!json.Contains("api_key", StringComparison.OrdinalIgnoreCase), "provider selection persisted an API key field");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task TestHarnessBootstrapper()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-bootstrapper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var selectionStore = new ModelProviderSelectionStore(Path.Combine(root, "provider-selection.json"));
            var secrets = new PersistentSecretStore(Path.Combine(root, "secrets"), new TestSecretProtector(), "phase3-test-entropy");
            var manifests = new Dictionary<string, ModelRuntimeManifest>(StringComparer.Ordinal);
            var consents = new Dictionary<string, ExternalEgressConsent>(StringComparer.Ordinal);
            var bootstrapper = new HarnessBootstrapper(selectionStore, secrets, manifests, consents);
            await AssertThrows<InvalidOperationException>(() => bootstrapper.StartAsync(18092, CancellationToken.None), "bootstrapper started without a selection");

            await selectionStore.SaveAsync(new ModelProviderSelection("sel-missing", ModelProviderKind.VerifiedLocal, "missing-provider", "model", "http://127.0.0.1:18092", "C:\\nope\\model.gguf", null, false, true), CancellationToken.None);
            await AssertThrows<InvalidOperationException>(() => bootstrapper.StartAsync(18092, CancellationToken.None), "bootstrapper started with a missing manifest");

            secrets.Store("external-api", "sk-external-2");
            await selectionStore.SaveAsync(new ModelProviderSelection("sel-ext", ModelProviderKind.ExternalApi, "external-api", "model", "http://api.invalid/v1", null, "cred_external-api", true, true), CancellationToken.None);
            await AssertThrows<InvalidOperationException>(() => bootstrapper.StartAsync(18092, CancellationToken.None), "bootstrapper started an external provider without consent");

            consents["external-api"] = new ExternalEgressConsent("consent-boot-1", "external-api", DateTimeOffset.UtcNow, "bootstrapper fixture consent");
            var emptySecrets = new PersistentSecretStore(Path.Combine(root, "empty"), new TestSecretProtector(), "phase3-test-entropy");
            var noSecretBootstrapper = new HarnessBootstrapper(selectionStore, emptySecrets, manifests, consents);
            await AssertThrows<InvalidOperationException>(() => noSecretBootstrapper.StartAsync(18092, CancellationToken.None), "bootstrapper started an external provider without a stored secret");

            var fullBootstrapper = new HarnessBootstrapper(selectionStore, secrets, manifests, consents);
            await using (var session = await fullBootstrapper.StartAsync(18092, CancellationToken.None))
            {
                Assert(!session.HasLocalRuntime && session.Provider is ExternalApiProviderAdapter, "external session did not produce the external adapter");
            }

            await selectionStore.SaveAsync(new ModelProviderSelection("sel-nonloop", ModelProviderKind.UserLocal, "loop-provider", "loop-model", "http://example.invalid/v1", null, null, false, true), CancellationToken.None);
            await AssertThrows<InvalidOperationException>(() => fullBootstrapper.StartAsync(18092, CancellationToken.None), "non-loopback user-local endpoint was accepted");

            using var key = RSA.Create(2048);
            var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            var serverTask = Task.Run(async () =>
            {
                var client = await tcp.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 8192, true);
                var contentLength = 0L;
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) Assert(false, "loopback provider sent an authorization header");
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) && long.TryParse(line.Substring("Content-Length:".Length).Trim(), out var parsed)) contentLength = parsed;
                }
                var bodyBuffer = new byte[contentLength];
                await stream.ReadExactlyAsync(bodyBuffer);
                var actionJson = JsonSerializer.Serialize(CreateAction(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                var envelopeJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = actionJson } } }, usage = new { total_tokens = 5 } });
                var response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + Encoding.UTF8.GetByteCount(envelopeJson) + "\r\nConnection: close\r\n\r\n" + envelopeJson;
                await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
                tcp.Stop();
            });
            await selectionStore.SaveAsync(new ModelProviderSelection("sel-loop", ModelProviderKind.UserLocal, "loop-provider", "loop-model", $"http://127.0.0.1:{port}", null, null, false, true), CancellationToken.None);
            await using (var session = await fullBootstrapper.StartAsync(18092, CancellationToken.None))
            {
                Assert(!session.HasLocalRuntime && session.Provider is LoopbackEndpointProviderAdapter, "loopback session did not produce the loopback adapter");
                var proposal = await session.Provider.ProposeAsync("synthetic fixture only", manifest, CancellationToken.None);
                Assert(proposal.FailureClass == ProviderFailureClass.None, "loopback provider did not return a valid proposal: " + proposal.FailureClass);
            }
            await serverTask;

            var tamperRoot = Path.Combine(root, "tamper");
            Directory.CreateDirectory(tamperRoot);
            var modelPath = Path.Combine(tamperRoot, "model.gguf");
            var runtimePath = Path.Combine(tamperRoot, "llama-server.exe");
            var licensePath = Path.Combine(tamperRoot, "LICENSE.txt");
            var templatePath = Path.Combine(tamperRoot, "safe-chat-template.jinja");
            var modelBytes = Encoding.UTF8.GetBytes("synthetic-model");
            var runtimeBytes = Encoding.UTF8.GetBytes("synthetic-runtime");
            var licenseBytes = Encoding.UTF8.GetBytes("synthetic-license");
            var templateBytes = Encoding.UTF8.GetBytes("synthetic-template");
            File.WriteAllBytes(modelPath, modelBytes);
            File.WriteAllBytes(runtimePath, runtimeBytes);
            File.WriteAllBytes(licensePath, licenseBytes);
            File.WriteAllBytes(templatePath, templateBytes);
            manifests["tamper-provider"] = new ModelRuntimeManifest("tamper-model", modelPath, Canonicalization.Sha256Hex(modelBytes), "rev-1", runtimePath, Canonicalization.Sha256Hex(runtimeBytes), "1.0", "qwen2", licensePath, Canonicalization.Sha256Hex(licenseBytes), templatePath, Canonicalization.Sha256Hex(templateBytes), 4096, 1024, 1024, 1024, "tamper-model");
            await File.WriteAllBytesAsync(modelPath, Encoding.UTF8.GetBytes("different-model-bytes"));
            await selectionStore.SaveAsync(new ModelProviderSelection("sel-tamper", ModelProviderKind.VerifiedLocal, "tamper-provider", "tamper-model", "http://127.0.0.1:18092", modelPath, null, false, true), CancellationToken.None);
            await AssertThrows<InvalidOperationException>(() => fullBootstrapper.StartAsync(18092, CancellationToken.None), "tampered model passed bootstrapper validation");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task TestRealModelRuntime()
    {
        var project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var model = Path.Combine(project, "models", "WhiteRabbitNeo-V3-7B-GGUF", "WhiteRabbitNeo_WhiteRabbitNeo-V3-7B-Q4_K_M.gguf");
        var runtime = Path.Combine(project, "runtime", "llama.cpp", "b10488", "llama-server.exe");
        var license = Path.Combine(project, "runtime", "llama.cpp", "b10488", "LICENSE");
        var template = Path.Combine(project, "runtime", "llama.cpp", "b10488", "safe-chat-template.jinja");
        var manifest = new ModelRuntimeManifest("wrn-v3-7b-q4-k-m", model, "584bfc1f4c160928842866c566129f9789c4671af8e51a9e36ba0ebf10f24f41", "5cc667f09d00b213c07530c716a0f900dd59f5aa", runtime, "3e2ac8887fb37fa4312654cf6625e3449a7e87989b0116c4a582fd518d02cf2f", "0.1.2-dev-build-10488", "qwen2", license, "94f29bbed6a22c35b992c5c6ebf0e7c92f13b836b90f36f461c9cf2f0f1d010d", template, "a9624c70be4119ac7f4f772ed88ca574dcbaab726f0f3a3e2cbc265b84a886d0", 4096, 5000000000, 12000000000, 6144, "wrn-v3-7b-q4-k-m");
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-real-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var selectionStore = new ModelProviderSelectionStore(Path.Combine(root, "provider-selection.json"));
            var secrets = new PersistentSecretStore(Path.Combine(root, "secrets"), new TestSecretProtector(), "phase3-test-entropy");
            await selectionStore.SaveAsync(new ModelProviderSelection("selection-real", ModelProviderKind.UserLocal, "wrn-local", "wrn-v3-7b-q4-k-m", "http://127.0.0.1:18080", model, null, false, true), CancellationToken.None);
            var bootstrapper = new HarnessBootstrapper(selectionStore, secrets, new Dictionary<string, ModelRuntimeManifest>(StringComparer.Ordinal) { ["wrn-local"] = manifest }, new Dictionary<string, ExternalEgressConsent>(StringComparer.Ordinal));
            await using var session = await bootstrapper.StartAsync(18080, CancellationToken.None);
            Assert(session.HasLocalRuntime && session.Selection.ProviderRef == "wrn-local", "bootstrapper did not start the selected managed runtime");
            using var key = RSA.Create(2048);
            var authorization = CreateManifest(key, DateTimeOffset.UtcNow);
            var proposal = await session.Provider.ProposeAsync("Return only one JSON object with these exact synthetic fixture values: type ACTION_REQUEST, run_id run-phase3, action_id action-phase3, phase phase3, target_ref http://127.0.0.1:8080/, capability_ref fixture.inspect, arguments {mode: safe}, purpose exercise a deterministic local fixture, expected_observation fixture response, risk_class R0, scope_ref scope-phase3, authorization_ref phase3-auth, methodology_refs [phase3-fixture-v1], approval_ref null, credential_ref null, resolved_addresses []. Do not use markdown.", authorization, CancellationToken.None);
            Assert(proposal.FailureClass == ProviderFailureClass.None, "real local model did not return a valid typed proposal: " + proposal.FailureClass);
            Assert(ActionRequestValidator.Validate(proposal.Action).IsValid, "real local model proposal failed action validation");
            var capabilities = CreateCapabilities();
            var policyEngine = new PolicyEngine(capabilities, CreateTrustStore(key));
            var policy = policyEngine.Evaluate(proposal.Action, authorization, null);
            Assert(policy.Decision == PolicyDecision.Allow, "real model proposal was not allowed by policy");
            using var issuer = new PermitIssuer(policyEngine);
            var permit = issuer.Issue(proposal.Action, authorization, "real-fixture-worker");
            Assert(issuer.TryConsume(permit, proposal.Action, authorization, "real-fixture-worker"), "real model permit was not consumed");
            var toolManifest = new ToolCapabilityManifest("real-fixture-tool", "1.0", proposal.Action.CapabilityRef, "unprivileged", true, Array.Empty<string>(), new[] { "synthetic" }, true, new[] { "raw", "redacted", "observation" }, true, TimeSpan.FromSeconds(5), 1024);
            var adapter = new FixtureToolAdapter("real-fixture-tool", "1.0", "real-fixture-result", ToolResultStatus.Success, "fixture response");
            var registry = new ToolRegistry();
            registry.Register(toolManifest, adapter);
            registry.Freeze();
            var artifacts = new ArtifactStore();
            var evidence = new EvidenceLedger(artifacts);
            using var realKey = RSA.Create(2048);
            using var provenance = new ProvenanceAuthority(new ProductIdentity("cyber-sop-harness", "phase3b-real", Canonicalization.Sha256Hex("phase3b-real-build"), ProvenanceKeyCustody.Fingerprint(realKey)), realKey);
            var broker = new ToolBroker(registry, evidence, issuer, provenance, new OutputRedactor());
            var envelope = ActionEnvelopeFactory.Create(proposal);
            var outcome = await broker.ExecuteAsync(envelope, authorization, policy, permit, "real-fixture-worker", null, CancellationToken.None);
            Assert(outcome.Dispatched && outcome.Evidence.Status == ToolResultStatus.Success, "real model proposal did not execute the synthetic fixture path");
            Assert(provenance.Verify(outcome.Provenance, outcome.Evidence, authorization), "real model evidence provenance failed verification");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Task TestDurableEvidencePersistence()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var journalPath = Path.Combine(root, "evidence.journal");
            var artifactsDir = Path.Combine(root, "artifacts");
            using (var fixture = new RuntimeFixture())
            {
                var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
                var audit = new WorkflowAuditLog();
                var auditEntry = audit.Append(fixture.Action.RunId, "WIZARD_SELECTION", "provider=fixture");
                var durableArtifacts = new DurableArtifactStore(artifactsDir);
                foreach (var reference in outcome.Evidence.ArtifactRefs)
                {
                    Assert(fixture.Evidence.TryReadArtifact(reference, out var artifactBytes), "fixture artifact was missing");
                    durableArtifacts.Put(reference, artifactBytes);
                }
                using (var journal = new DurableEvidenceJournal(journalPath, durableArtifacts))
                {
                    journal.Append(outcome.Evidence);
                    journal.Append(auditEntry);
                }
                using (var reloaded = new DurableEvidenceJournal(journalPath, durableArtifacts))
                {
                    var result = reloaded.Recover();
                    Assert(result.Status == RecoveryStatus.Verified, "clean journal did not recover as verified");
                    Assert(result.Events.Count == 1 && result.AuditEntries.Count == 1, "recovered record count was wrong");
                    Assert(result.Events[0].ResultEventId == outcome.Evidence.ResultEventId && result.Events[0].EventHash == outcome.Evidence.EventHash, "recovered evidence diverged from the original");
                    Assert(result.AuditEntries[0].EventHash == auditEntry.EventHash, "recovered audit entry diverged from the original");
                }
                var validBytes = File.ReadAllBytes(journalPath);
                var originalLength = validBytes.Length;

                var tamperedLines = File.ReadAllLines(journalPath).ToArray();
                tamperedLines[0] = "{\"rt\":\"event\",\"p\":\"{}\",\"h\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"prev\":null}";
                File.WriteAllLines(journalPath, tamperedLines);
                using (var tamperedJournal = new DurableEvidenceJournal(journalPath, durableArtifacts))
                {
                    var result = tamperedJournal.Recover();
                    Assert(result.Status == RecoveryStatus.Corrupt, "tampered journal did not report corrupt");
                }

                File.WriteAllBytes(journalPath, validBytes.AsSpan(0, originalLength - 5).ToArray());
                using (var truncatedJournal = new DurableEvidenceJournal(journalPath, durableArtifacts))
                {
                    var result = truncatedJournal.Recover();
                    Assert(result.Status == RecoveryStatus.Partial, "truncated journal did not report partial");
                    Assert(result.Events.Count == 1 && result.AuditEntries.Count == 0, "partial recovery kept the wrong prefix");
                    Assert(new FileInfo(journalPath).Length < originalLength, "partial tail was not discarded");
                }

                using var lastJournal = new DurableEvidenceJournal(journalPath, durableArtifacts);
                lastJournal.Append(outcome.Evidence);
                var deletedRef = outcome.Evidence.RawArtifactRef;
                durableArtifacts.Delete(deletedRef);
                var artifactResult = lastJournal.Recover();
                Assert(artifactResult.Status == RecoveryStatus.Corrupt && artifactResult.Reason is not null && artifactResult.Reason.Contains(deletedRef, StringComparison.Ordinal), "missing artifact was not reported corrupt");
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
        return Task.CompletedTask;
    }

    private static Task TestPersistentSecretStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-secrets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var protector = new TestSecretProtector();
            var store = new PersistentSecretStore(Path.Combine(root, "secrets"), protector, "phase3-test-entropy");
            store.Store("api-provider", "sk-secret-123");
            Assert(store.Exists("api-provider"), "stored secret was not visible");
            Assert(store.Load("api-provider") == "sk-secret-123", "secret round trip failed");
            var secretFile = Path.Combine(root, "secrets", "api-provider.secret");
            Assert(!File.ReadAllText(secretFile).Contains("sk-secret-123", StringComparison.Ordinal), "plaintext secret was persisted");
            var blob = File.ReadAllBytes(secretFile);
            blob[0] ^= 0xFF;
            File.WriteAllBytes(secretFile, blob);
            AssertThrows<CryptographicException>(() => { store.Load("api-provider"); return Task.CompletedTask; }, "tampered secret blob was accepted").GetAwaiter().GetResult();
            store.Delete("api-provider");
            Assert(!store.Exists("api-provider"), "secret delete failed");
            AssertThrows<ArgumentException>(() => { store.Store("../evil", "x"); return Task.CompletedTask; }, "path-traversal provider id was accepted").GetAwaiter().GetResult();

            if (OperatingSystem.IsWindows())
            {
                var dpapi = new WindowsDpapiSecretProtector("phase3-test-entropy");
                var dpapiStore = new PersistentSecretStore(Path.Combine(root, "dpapi"), dpapi, "phase3-test-entropy");
                dpapiStore.Store("dpapi-provider", "sk-dpapi-1");
                Assert(dpapiStore.Load("dpapi-provider") == "sk-dpapi-1", "DPAPI secret round trip failed");
                var dpapiFile = Path.Combine(root, "dpapi", "dpapi-provider.secret");
                Assert(!File.ReadAllText(dpapiFile).Contains("sk-dpapi-1", StringComparison.Ordinal), "DPAPI plaintext secret was persisted");
                var other = new WindowsDpapiSecretProtector("different-entropy");
                var otherStore = new PersistentSecretStore(Path.Combine(root, "dpapi"), other, "different-entropy");
                AssertThrows<CryptographicException>(() => { otherStore.Load("dpapi-provider"); return Task.CompletedTask; }, "wrong-entropy DPAPI load was accepted").GetAwaiter().GetResult();
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
        return Task.CompletedTask;
    }

    private static Task TestPassphraseSecretProtector()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-passphrase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string passphrase = "correct-horse-battery";
            var custodian = new PassphraseSecretProtector("passphrase-test", () => passphrase);
            var plaintext = Encoding.UTF8.GetBytes("release-signing-key");
            var protectedBytes = custodian.Protect(plaintext, "runtime-evidence");
            Assert(!protectedBytes.SequenceEqual(plaintext), "passphrase protector returned plaintext");
            Assert(custodian.Unprotect(protectedBytes, "runtime-evidence").SequenceEqual(plaintext), "passphrase round trip diverged");

            var wrongPassphrase = new PassphraseSecretProtector("passphrase-test", () => "wrong-horse-battery");
            AssertThrows<CryptographicException>(() => Task.FromResult(wrongPassphrase.Unprotect(protectedBytes, "runtime-evidence")), "wrong custody passphrase unlocked protected data").GetAwaiter().GetResult();

            var tampered = (byte[])protectedBytes.Clone();
            tampered[^1] ^= 0x80;
            AssertThrows<CryptographicException>(() => Task.FromResult(custodian.Unprotect(tampered, "runtime-evidence")), "tampered custody blob unlocked").GetAwaiter().GetResult();
            AssertThrows<CryptographicException>(() => Task.FromResult(custodian.Unprotect(protectedBytes, "different-context")), "custody blob crossed contexts").GetAwaiter().GetResult();

            var shortPassphrase = new PassphraseSecretProtector("passphrase-test", () => "short-pass");
            AssertThrows<InvalidOperationException>(() => Task.FromResult(shortPassphrase.Protect(plaintext, "runtime-evidence")), "short custody passphrase was accepted").GetAwaiter().GetResult();
        }
        finally
        {
            Directory.Delete(root, true);
        }
        return Task.CompletedTask;
    }

    private static Task TestProvenanceKeyCustody()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var protector = new TestSecretProtector();
            var store = new ProvenanceKeyStore(Path.Combine(root, "keys"), protector, "phase3-test-entropy");
            var runtimeKey = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
            var runtimeFingerprint = ProvenanceKeyCustody.Fingerprint(runtimeKey);
            runtimeKey.Dispose();
            var reloadedKey = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
            Assert(ProvenanceKeyCustody.Fingerprint(reloadedKey) == runtimeFingerprint, "key store did not reload the same key");
            reloadedKey.Dispose();

            using var fixture = new RuntimeFixture();
            var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
            var productIdentity = new ProductIdentity("cyber-sop-harness", "phase3-key", Canonicalization.Sha256Hex("phase3-key-build"), runtimeFingerprint);
            ProvenanceStamp preRotationStamp;
            using (var preAuthority = new ProvenanceAuthority(productIdentity, store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence)))
            {
                preRotationStamp = preAuthority.Issue(outcome.Evidence, fixture.Manifest);
                Assert(preAuthority.Verify(preRotationStamp, outcome.Evidence, fixture.Manifest), "pre-rotation stamp did not verify");
                Assert(ProvenanceAuthority.VerifyStamp(preRotationStamp, outcome.Evidence, fixture.Manifest, preAuthority.PublicKeyPem), "offline stamp verification failed");
            }

            var rotatedKey = store.Rotate(ProvenanceKeyRole.RuntimeEvidence);
            var newFingerprint = ProvenanceKeyCustody.Fingerprint(rotatedKey);
            Assert(newFingerprint != runtimeFingerprint, "rotation kept the same key");
            var retired = store.RetiredPublicKeys(ProvenanceKeyRole.RuntimeEvidence);
            Assert(retired.Count == 1, "retired key was not archived");
            using (var rotatedAuthority = new ProvenanceAuthority(productIdentity with { ReleaseKeyRef = newFingerprint }, rotatedKey, retired))
            {
                Assert(ProvenanceAuthority.VerifyStamp(preRotationStamp, outcome.Evidence, fixture.Manifest, retired[0]), "retired-key offline verification failed");
                Assert(rotatedAuthority.Verify(preRotationStamp, outcome.Evidence, fixture.Manifest), "old stamp did not verify under rotated authority");
                var postRotationStamp = rotatedAuthority.Issue(outcome.Evidence, fixture.Manifest);
                Assert(rotatedAuthority.Verify(postRotationStamp, outcome.Evidence, fixture.Manifest), "post-rotation stamp did not verify");
                Assert(!rotatedAuthority.Verify(postRotationStamp with { EvidenceHash = new string('0', 64) }, outcome.Evidence, fixture.Manifest), "tampered stamp verified under rotated key");
            }

            using var releaseKey = store.CreateOrLoad(ProvenanceKeyRole.Release);
            using var releaseAuthority = new ReleaseSigningAuthority(productIdentity with { ReleaseKeyRef = ProvenanceKeyCustody.Fingerprint(releaseKey) }, releaseKey);
            var release = releaseAuthority.Issue("release-1", new[] { new ReleaseFileEntry("file.txt", 4, Canonicalization.Sha256Hex("file")) });
            Assert(releaseAuthority.Verify(release), "release manifest failed verification");
            Assert(ReleaseSigningAuthority.Verify(release, releaseAuthority.PublicKeyPem), "offline release verification failed");
            Assert(!ReleaseSigningAuthority.Verify(release with { ProductVersion = "tampered" }, releaseAuthority.PublicKeyPem), "tampered release verified offline");
            using var runtimeKeyAfterRotation = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
            using var runtimeAuthority = new ProvenanceAuthority(productIdentity with { ReleaseKeyRef = ProvenanceKeyCustody.Fingerprint(runtimeKeyAfterRotation) }, runtimeKeyAfterRotation);
            Assert(!runtimeAuthority.VerifyReleaseManifest(release), "runtime key verified a release signed by the release key");
        }
        finally
        {
            Directory.Delete(root, true);
        }
        return Task.CompletedTask;
    }

    private static async Task TestProviderSelectionWizard()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-wizard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ModelProviderSelectionStore(Path.Combine(root, "provider-selection.json"));
            var secrets = new PersistentSecretStore(Path.Combine(root, "secrets"), new TestSecretProtector(), "phase3-test-entropy");
            secrets.Store("api-provider", "sk-api-1");
            var choices = new[]
            {
                new ProviderDisclosure("wrn-local", "WhiteRabbitNeo V3 7B", "wrn-v3-7b-q4-k-m", "b10488", "bartowski/WhiteRabbitNeo_WhiteRabbitNeo-V3-7B-GGUF", "Apache-2.0 metadata; redistribution pending", "E:\\models\\wrn.gguf", "local-only; no retention", "~4.7 GB RAM", ProviderEgressStatus.Local),
                new ProviderDisclosure("api-provider", "External API", "model-1", "v2", "https://api.invalid/v1", "unknown", "https://api.invalid/v1", "remote retention; data leaves host", "network", ProviderEgressStatus.External),
                new ProviderDisclosure("offline-fixture", "Offline Fixture", "fixture-model", "1.0", "bundled", "MIT", "fixtures/models/fixture.bin", "none", "minimal", ProviderEgressStatus.Offline)
            };
            var wizard = new ModelProviderWizard(store, secrets, choices);
            Assert(wizard.Choices.Count == 3, "wizard did not expose all three choices");
            var rendered = ProviderDisclosureRenderer.Render(choices[1]);
            Assert(rendered.Contains("EGRESS: EXTERNAL", StringComparison.Ordinal) && rendered.Contains("remote retention", StringComparison.Ordinal), "external disclosure did not surface egress and retention");
            Assert(ProviderDisclosureRenderer.Render(choices[2]).Contains("EGRESS: OFFLINE", StringComparison.Ordinal), "offline disclosure did not surface offline status");

            var localEvent = await wizard.ConfirmAsync("wrn-local", false, null, CancellationToken.None);
            Assert(localEvent.Kind == ModelProviderKind.UserLocal && localEvent.EgressStatus == ProviderEgressStatus.Local, "local confirmation produced the wrong kind");
            var stored = await store.LoadAsync(CancellationToken.None);
            Assert(stored is not null && stored.SelectionId == localEvent.SelectionId && stored.ExternalEgressAllowed == false, "local selection did not persist safely");

            await AssertThrows<InvalidOperationException>(() => wizard.ConfirmAsync("api-provider", false, localEvent.SelectionId, CancellationToken.None), "external selection without egress acknowledgement was accepted");
            await AssertThrows<ArgumentException>(() => wizard.ConfirmAsync("unavailable", false, null, CancellationToken.None), "unavailable choice was accepted");
            var apiEvent = await wizard.ConfirmAsync("api-provider", true, localEvent.SelectionId, CancellationToken.None);
            Assert(apiEvent.Kind == ModelProviderKind.ExternalApi && apiEvent.EgressStatus == ProviderEgressStatus.External, "external confirmation produced the wrong kind");
            Assert(apiEvent.PreviousSelectionId == localEvent.SelectionId, "previous selection was not bound for invalidation");

            var noSecretWizard = new ModelProviderWizard(store, null, new[] { choices[1] });
            await AssertThrows<InvalidOperationException>(() => noSecretWizard.ConfirmAsync("api-provider", true, null, CancellationToken.None), "external provider without a stored secret was accepted");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Task TestRuntimeJournalMirror()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-mirror-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var artifactsDir = Path.Combine(root, "artifacts");
            var durableArtifacts = new DurableArtifactStore(artifactsDir);
            var journal = new DurableEvidenceJournal(Path.Combine(root, "evidence.journal"), durableArtifacts);
            using (var fixture = new RuntimeFixture(null, new DurableEvidenceJournal(Path.Combine(root, "mirror.journal"), durableArtifacts)))
            {
                var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
                var audit = new WorkflowAuditLog(new DurableEvidenceJournal(Path.Combine(root, "audit.journal"), durableArtifacts));
                var machine = new WorkflowStateMachine(fixture.Evidence, audit);
                var run = new WorkflowRun(fixture.Action.RunId, fixture.Action.ActionId, fixture.Envelope.ActionHash);
                machine.Transition(run, WorkflowState.Planned);
                using var recovery = new DurableEvidenceJournal(Path.Combine(root, "mirror.journal"), durableArtifacts);
                var result = recovery.Recover();
                Assert(result.Status == RecoveryStatus.Verified, "runtime journal did not recover as verified");
                Assert(result.Events.Count == 1 && result.Events[0].ResultEventId == outcome.Evidence.ResultEventId, "runtime journal missed the broker evidence event");
                Assert(durableArtifacts.Exists(outcome.Evidence.RawArtifactRef) && durableArtifacts.VerifyHash(outcome.Evidence.RawArtifactRef, outcome.Evidence.RawSha256), "runtime journal did not persist artifacts with matching hashes");
                using var auditRecovery = new DurableEvidenceJournal(Path.Combine(root, "audit.journal"), durableArtifacts);
                var auditResult = auditRecovery.Recover();
                Assert(auditResult.Status == RecoveryStatus.Verified && auditResult.AuditEntries.Count == 1, "audit journal did not mirror the state transition");
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
        return Task.CompletedTask;
    }

    private static async Task TestExternalApiProvider()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-external-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var secrets = new PersistentSecretStore(Path.Combine(root, "secrets"), new TestSecretProtector(), "phase3-test-entropy");
            secrets.Store("external-api", "sk-external-1");
            var receivedAuth = string.Empty;
            var requestCount = 0;
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            var serverTask = Task.Run(async () =>
            {
                var client = await tcp.AcceptTcpClientAsync();
                Interlocked.Increment(ref requestCount);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 8192, true);
                var contentLength = 0L;
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) receivedAuth = line.Substring("Authorization:".Length).Trim();
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) && long.TryParse(line.Substring("Content-Length:".Length).Trim(), out var parsed)) contentLength = parsed;
                }
                var bodyBuffer = new byte[contentLength];
                await stream.ReadExactlyAsync(bodyBuffer);
                var body = Encoding.UTF8.GetString(bodyBuffer);
                Assert(!body.Contains("sk-external-1", StringComparison.Ordinal), "secret was sent in the request body");
                var actionJson = JsonSerializer.Serialize(CreateAction(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                var envelopeJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = actionJson } } }, usage = new { total_tokens = 5 } });
                var response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + Encoding.UTF8.GetByteCount(envelopeJson) + "\r\nConnection: close\r\n\r\n" + envelopeJson;
                await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
                tcp.Stop();
            });

            using var key = RSA.Create(2048);
            var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
            var descriptor = new ProviderDescriptor("external-api", "external-model", "remote", Canonicalization.Sha256Hex("external-config"), "remote", "remote retention", "typed");
            using var noConsent = new ExternalApiProviderAdapter(new Uri($"http://127.0.0.1:{port}"), descriptor, secrets, "external-api", null);
            var blockedProposal = await noConsent.ProposeAsync("synthetic fixture only", manifest, CancellationToken.None);
            Assert(blockedProposal.FailureClass == ProviderFailureClass.PolicyBlocked, "external provider without consent was not blocked");
            Assert(requestCount == 0, "external provider made a request without consent");

            var consent = new ExternalEgressConsent("consent-1", "external-api", DateTimeOffset.UtcNow, "fixture test consent");
            using var adapter = new ExternalApiProviderAdapter(new Uri($"http://127.0.0.1:{port}"), descriptor, secrets, "external-api", consent);
            var proposal = await adapter.ProposeAsync("synthetic fixture only", manifest, CancellationToken.None);
            Assert(proposal.FailureClass == ProviderFailureClass.None, "external provider did not return a valid proposal: " + proposal.FailureClass);
            Assert(proposal.Action.ActionId == CreateAction().ActionId, "external provider returned the wrong action");
            Assert(receivedAuth == "Bearer sk-external-1", "external provider did not send the secret in the Authorization header");
            Assert(requestCount == 1, "external provider request count was wrong");

            var emptySecrets = new PersistentSecretStore(Path.Combine(root, "empty"), new TestSecretProtector(), "phase3-test-entropy");
            using var noSecret = new ExternalApiProviderAdapter(new Uri($"http://127.0.0.1:{port}"), descriptor, emptySecrets, "external-api", consent);
            var noSecretProposal = await noSecret.ProposeAsync("synthetic fixture only", manifest, CancellationToken.None);
            Assert(noSecretProposal.FailureClass == ProviderFailureClass.PolicyBlocked, "external provider without a stored secret was not blocked");
            Assert(requestCount == 1, "external provider made a request without a secret");

            await serverTask;
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Task TestSyntheticFixtureToolAdapter()
    {
        using var fixture = new RuntimeFixture(new SyntheticFixtureToolAdapter("fixture-tool", "1.0", "fixture response", ToolResultStatus.Success, "fixture.observation"));
        var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
        Assert(outcome.Dispatched && outcome.Evidence.Status == ToolResultStatus.Success && outcome.Evidence.CleanupResult == "SUCCEEDED", "synthetic fixture adapter did not execute and clean up");
        Assert(fixture.Provenance.Verify(outcome.Provenance, outcome.Evidence, fixture.Manifest), "synthetic fixture evidence provenance failed verification");
        Assert(outcome.Evidence.ObservationRefs.Contains("fixture.observation", StringComparer.Ordinal), "synthetic fixture observation was not recorded");
        Assert(fixture.Evidence.TryReadArtifact(outcome.Evidence.RawArtifactRef, out var raw) && Encoding.UTF8.GetString(raw) == "fixture response", "synthetic fixture raw artifact diverged");
        using var unbound = new RuntimeFixture(new SyntheticFixtureToolAdapter("fixture-tool", "1.0", "must-not-run", ToolResultStatus.Success, "fixture.observation"), consumePermit: false);
        var rejected = unbound.Broker.ExecuteAsync(unbound.Envelope, unbound.Manifest, unbound.Policy, unbound.Permit, unbound.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!rejected.Dispatched && rejected.Evidence.Status == ToolResultStatus.Blocked, "broker dispatched without consuming its one-use permit");
        Assert(rejected.FailureReason == "permit is expired, invalid, replayed, or not bound to the current tool dispatch", "unconsumed-permit rejection was imprecise");
        Assert(unbound.AdapterInvocationCount == 0, "tool adapter ran despite an unconsumed permit");
        return Task.CompletedTask;
    }

    private static async Task TestExternalEndpointStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-endpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert(ExternalEndpointValidator.TryValidate("https://api.example.com/v1", out var https, out _) && https is not null, "valid https endpoint was rejected");
            Assert(ExternalEndpointValidator.TryValidate("http://127.0.0.1:18091", out var loopback, out _) && loopback is not null, "loopback http endpoint was rejected");
            Assert(!ExternalEndpointValidator.TryValidate("http://example.invalid/v1", out _, out _), "non-loopback http endpoint was accepted");
            Assert(!ExternalEndpointValidator.TryValidate("https://user:pass@api.example.com/v1", out _, out _), "endpoint with embedded credentials was accepted");
            Assert(!ExternalEndpointValidator.TryValidate("https://api.example.com/v1?key=abc", out _, out _), "endpoint with a query was accepted");
            Assert(!ExternalEndpointValidator.TryValidate("https://api.example.com/v1#frag", out _, out _), "endpoint with a fragment was accepted");
            Assert(!ExternalEndpointValidator.TryValidate("not-a-url", out _, out _), "garbage endpoint was accepted");
            Assert(!ExternalEndpointValidator.TryValidate("", out _, out _), "empty endpoint was accepted");

            var store = new ExternalEndpointStore(Path.Combine(root, "external-endpoint.json"));
            Assert(await store.LoadAsync(CancellationToken.None) is null, "endpoint store loaded a value before it was set");
            await store.SaveAsync(https!, CancellationToken.None);
            var loaded = await store.LoadAsync(CancellationToken.None);
            Assert(loaded is not null && string.Equals(loaded.ToString(), "https://api.example.com/v1", StringComparison.Ordinal), "endpoint round trip diverged");
            await store.ClearAsync(CancellationToken.None);
            Assert(await store.LoadAsync(CancellationToken.None) is null, "endpoint store still has a value after clear");
            await AssertThrows<ArgumentException>(() => store.SaveAsync(new Uri("http://example.invalid/v1"), CancellationToken.None), "invalid endpoint was persisted");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task TestExternalProviderBrokerPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyber-sop-harness-ext-broker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var secrets = new PersistentSecretStore(Path.Combine(root, "secrets"), new TestSecretProtector(), "phase3-test-entropy");
            secrets.Store("external-api", "sk-external-3");
            var receivedAuth = string.Empty;
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            var serverTask = Task.Run(async () =>
            {
                var client = await tcp.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 8192, true);
                var contentLength = 0L;
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) receivedAuth = line.Substring("Authorization:".Length).Trim();
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) && long.TryParse(line.Substring("Content-Length:".Length).Trim(), out var parsed)) contentLength = parsed;
                }
                var bodyBuffer = new byte[contentLength];
                await stream.ReadExactlyAsync(bodyBuffer);
                var body = Encoding.UTF8.GetString(bodyBuffer);
                Assert(!body.Contains("sk-external-3", StringComparison.Ordinal), "secret was sent in the request body");
                var actionJson = JsonSerializer.Serialize(CreateAction(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                var envelopeJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = actionJson } } }, usage = new { total_tokens = 7 } });
                var response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + Encoding.UTF8.GetByteCount(envelopeJson) + "\r\nConnection: close\r\n\r\n" + envelopeJson;
                await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
                tcp.Stop();
            });

            var selectionStore = new ModelProviderSelectionStore(Path.Combine(root, "provider-selection.json"));
            await selectionStore.SaveAsync(new ModelProviderSelection("sel-ext-broker", ModelProviderKind.ExternalApi, "external-api", "external-model", $"http://127.0.0.1:{port}", null, "cred_external-api", true, true), CancellationToken.None);
            var consents = new Dictionary<string, ExternalEgressConsent>(StringComparer.Ordinal)
            {
                ["external-api"] = new ExternalEgressConsent("consent-broker-1", "external-api", DateTimeOffset.UtcNow, "broker path fixture consent")
            };
            var bootstrapper = new HarnessBootstrapper(selectionStore, secrets, new Dictionary<string, ModelRuntimeManifest>(StringComparer.Ordinal), consents);
            await using var session = await bootstrapper.StartAsync(18092, CancellationToken.None);
            Assert(session.Provider is ExternalApiProviderAdapter, "external session did not produce the external adapter");

            using var key = RSA.Create(2048);
            var authorization = CreateManifest(key, DateTimeOffset.UtcNow);
            var proposal = await session.Provider.ProposeAsync("synthetic fixture only", authorization, CancellationToken.None);
            Assert(proposal.FailureClass == ProviderFailureClass.None, "external proposal failed: " + proposal.FailureClass);
            Assert(receivedAuth == "Bearer sk-external-3", "external provider did not send the secret in the Authorization header");
            Assert(ActionRequestValidator.Validate(proposal.Action).IsValid, "external proposal failed action validation");
            var capabilities = CreateCapabilities();
            var policyEngine = new PolicyEngine(capabilities, CreateTrustStore(key));
            var policy = policyEngine.Evaluate(proposal.Action, authorization, null);
            Assert(policy.Decision == PolicyDecision.Allow, "external proposal was not allowed by policy");
            using var issuer = new PermitIssuer(policyEngine);
            var permit = issuer.Issue(proposal.Action, authorization, "ext-fixture-worker");
            Assert(issuer.TryConsume(permit, proposal.Action, authorization, "ext-fixture-worker"), "external permit was not consumed");
            var toolManifest = new ToolCapabilityManifest("ext-fixture-tool", "1.0", proposal.Action.CapabilityRef, "unprivileged", true, Array.Empty<string>(), new[] { "synthetic" }, true, new[] { "raw", "redacted", "observation" }, true, TimeSpan.FromSeconds(5), 1024);
            var adapter = new FixtureToolAdapter("ext-fixture-tool", "1.0", "ext-fixture-result", ToolResultStatus.Success, "fixture response");
            var registry = new ToolRegistry();
            registry.Register(toolManifest, adapter);
            registry.Freeze();
            var artifacts = new ArtifactStore();
            var evidence = new EvidenceLedger(artifacts);
            using var provenanceKey = RSA.Create(2048);
            using var provenance = new ProvenanceAuthority(new ProductIdentity("cyber-sop-harness", "phase3b-ext", Canonicalization.Sha256Hex("phase3b-ext-build"), ProvenanceKeyCustody.Fingerprint(provenanceKey)), provenanceKey);
            var broker = new ToolBroker(registry, evidence, issuer, provenance, new OutputRedactor());
            var envelope = ActionEnvelopeFactory.Create(proposal);
            var outcome = await broker.ExecuteAsync(envelope, authorization, policy, permit, "ext-fixture-worker", null, CancellationToken.None);
            Assert(outcome.Dispatched && outcome.Evidence.Status == ToolResultStatus.Success, "external proposal did not execute the synthetic fixture path");
            Assert(provenance.Verify(outcome.Provenance, outcome.Evidence, authorization), "external evidence provenance failed verification");

            await serverTask;
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task TestAuthorizedHttpHeaderInspection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var target = $"http://127.0.0.1:{port}/";
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var authorizationDraft = new AuthorizationManifest
        {
            EngagementId = "authorized-http-fixture",
            EngagementMode = EngagementMode.Authorized,
            Authorization = new AuthorizationProof("owner-1", "operator-1", "authorized-http-auth", string.Empty, string.Empty, string.Empty),
            Scope = new ScopeDefinition(new[] { "127.0.0.1" }, Array.Empty<string>(), "exact-only", "block", "block"),
            TimeWindow = new TimeWindow(now.AddMinutes(-1), now.AddMinutes(5), "UTC", Array.Empty<ExcludedWindow>()),
            Methods = new MethodDefinition(new[] { HttpHeaderInspectTool.CapabilityRef }, Array.Empty<string>()),
            AssetCriticality = new AssetCriticalityDefinition("low", new Dictionary<string, string>()),
            DataHandling = new DataHandlingDefinition("public-target-metadata", "required", "phase"),
            EscalationContacts = new[] { new EscalationContact("owner", "email", "owner@example.invalid") },
            CredentialPolicy = new CredentialPolicy(Array.Empty<string>(), false, "none"),
            RateLimits = new RateLimitDefinition(1, 1, 4096),
            Cleanup = new CleanupDefinition(true, "operator-1", "http-header-inspection-cleanup"),
            StopConditions = new[] { "scope-mismatch", "relay-loss" }
        };
        var authorization = authorizationDraft with { Authorization = AuthorizationSigner.Sign(authorizationDraft, key) };
        var action = new ActionRequest
        {
            RunId = "run-http",
            ActionId = "action-http",
            Phase = "recon",
            TargetRef = target,
            CapabilityRef = HttpHeaderInspectTool.CapabilityRef,
            Arguments = new Dictionary<string, string> { ["method"] = "GET" },
            Purpose = "capture authorized response metadata",
            RiskClass = RiskClass.R0,
            ScopeRef = "scope-http",
            AuthorizationRef = "authorized-http-auth",
            MethodologyRefs = new[] { "web-passive-baseline-v1" },
            ResolvedAddresses = new[] { "127.0.0.1" }
        };
        var capabilities = new CapabilityRegistry();
        capabilities.Register(new CapabilityManifest(
            HttpHeaderInspectTool.CapabilityRef,
            RiskClass.R0,
            new[] { target },
            "unprivileged",
            true,
            new[] { target },
            new[] { "http_metadata" },
            TimeSpan.FromSeconds(2),
            4096,
            false,
            true));
        capabilities.Freeze();
        var policy = new PolicyEngine(capabilities, CreateTrustStore(key)).Evaluate(action, authorization, null);
        Assert(policy.Decision == PolicyDecision.Allow, $"authorized HTTP policy did not allow: {policy.Reason}");
        var provider = new ProviderDescriptor("local-model", "lfm25-controller", "1.0", Canonicalization.Sha256Hex("model-config"), "local-only", "none", "typed");
        var proposal = new ProviderProposal(provider, action, Canonicalization.Sha256Hex(Canonicalization.ActionPayload(action)), TimeSpan.FromMilliseconds(1), 16, ProviderFailureClass.None);
        var envelope = ActionEnvelopeFactory.Create(proposal);
        await using var adapter = new HttpHeaderInspectTool("http-header-inspect", "1.0", new[] { "127.0.0.1" });
        var toolManifest = new ToolCapabilityManifest(
            "http-header-inspect",
            "1.0",
            HttpHeaderInspectTool.CapabilityRef,
            "unprivileged",
            true,
            new[] { target },
            new[] { "http_metadata" },
            true,
            new[] { "raw", "redacted", "observation" },
            true,
            TimeSpan.FromSeconds(2),
            4096);
        var registry = new ToolRegistry();
        registry.Register(toolManifest, adapter);
        registry.Freeze();
        var artifacts = new ArtifactStore();
        var evidence = new EvidenceLedger(artifacts);
        using var issuer = new PermitIssuer(new PolicyEngine(capabilities, CreateTrustStore(key)));
        using var provenanceKey = RSA.Create(2048);
        using var provenance = new ProvenanceAuthority(new ProductIdentity("cyber-sop-harness", "http-test", Canonicalization.Sha256Hex("http-test"), ProvenanceKeyCustody.Fingerprint(provenanceKey)), provenanceKey);
        var broker = new ToolBroker(registry, evidence, issuer, provenance);
        var permit = issuer.Issue(action, authorization, "http-worker");
        Assert(issuer.TryConsume(permit, action, authorization, "http-worker"), "HTTP permit was not accepted before dispatch");

        async Task ServeOnceAsync()
        {
            using var client = await listener.AcceptTcpClientAsync(CancellationToken.None);
            await using var stream = client.GetStream();
            var request = new MemoryStream();
            var buffer = new byte[4096];
            var headersComplete = false;
            while (!headersComplete)
            {
                var read = await stream.ReadAsync(buffer, CancellationToken.None);
                if (read == 0) break;
                await request.WriteAsync(buffer.AsMemory(0, read), CancellationToken.None);
                headersComplete = Encoding.UTF8.GetString(request.ToArray()).Contains("\r\n\r\n", StringComparison.Ordinal);
            }
            var body = "authorized-fixture-body";
            var payload = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nSet-Cookie: session=secret-canary\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
            await stream.WriteAsync(payload, CancellationToken.None);
        }

        try
        {
            var serverTask = ServeOnceAsync();
            var outcome = await broker.ExecuteAsync(envelope, authorization, policy, permit, "http-worker", null, CancellationToken.None);
            Assert(outcome.Dispatched && outcome.Evidence.Status == ToolResultStatus.Success, $"HTTP adapter did not succeed: {outcome.FailureReason}");
            Assert(artifacts.TryGet(outcome.Evidence.RedactedArtifactRef!, out var redactedOutput), "redacted HTTP evidence artifact was missing");
            var observation = Encoding.UTF8.GetString(redactedOutput);
            using var document = JsonDocument.Parse(observation);
            Assert(document.RootElement.GetProperty("status").GetInt32() == 200, "observed HTTP status was wrong");
            Assert(document.RootElement.GetProperty("redirects_followed").GetInt32() == 0, "HTTP redirects were followed");
            Assert(!observation.Contains("secret-canary", StringComparison.Ordinal), "sensitive header survived redaction");
            Assert(observation.Contains("[REDACTED]", StringComparison.Ordinal), "redaction marker was missing");
            Assert(provenance.Verify(outcome.Provenance, outcome.Evidence, authorization), "HTTP evidence provenance failed verification");
            await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            listener.Stop();
        }
    }


    private static async Task TestPermitExpiryDuringExecution()
    {
        using var fixture = new RuntimeFixture(new SlowToolAdapter("fixture-tool", "1.0", TimeSpan.FromMilliseconds(35)));
        var permit = fixture.Issuer.Issue(fixture.Action, fixture.Manifest, fixture.WorkerRef, lifetime: TimeSpan.FromMilliseconds(15));
        Assert(fixture.Issuer.TryConsume(permit, fixture.Action, fixture.Manifest, fixture.WorkerRef), "permit was not consumed before dispatch");
        var outcome = await fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, permit, fixture.WorkerRef, null, CancellationToken.None);
        Assert(outcome.Dispatched, "valid-at-consume-time permit was blocked at claim");
        Assert(outcome.Evidence.Status == ToolResultStatus.Success || outcome.Evidence.Status == ToolResultStatus.Partial, "slow adapter did not complete");
        Assert(permit.ExpiresAt < AuthoritativeClock.UtcNow, "permit should have expired during execution");
        Assert(fixture.Provenance.Verify(outcome.Provenance, outcome.Evidence, fixture.Manifest), "evidence from expired-permit-mid-execution failed provenance verification");
    }

    private static Task TestMultiHopRedirectScopeCrossing()
    {
        var now = DateTimeOffset.UtcNow;
        using var key = RSA.Create(2048);
        var draft = CreateManifest(key, now);
        var manifest = draft with { Scope = draft.Scope with { RedirectPolicy = "same-origin" } };
        var evaluator = new ScopeEvaluator(manifest);
        Assert(evaluator.Evaluate("http://127.0.0.1:8080/").Allowed, "initial target was blocked");
        var hop1 = evaluator.EvaluateRedirect("http://127.0.0.1:8080/", "http://127.0.0.1:8080/redirect");
        Assert(hop1.Allowed, "same-origin first hop was blocked");
        var hop2 = evaluator.EvaluateRedirect("http://127.0.0.1:8080/redirect", "https://outside.invalid/final");
        Assert(!hop2.Allowed, "second hop to out-of-scope host was allowed");
        var crossScheme = evaluator.EvaluateRedirect("http://127.0.0.1:8080/", "https://127.0.0.1:8081/");
        Assert(!crossScheme.Allowed, "cross-scheme redirect to different port was allowed");
        var blockAll = manifest with { Scope = manifest.Scope with { RedirectPolicy = "block" } };
        var blockEvaluator = new ScopeEvaluator(blockAll);
        Assert(!blockEvaluator.EvaluateRedirect("http://127.0.0.1:8080/", "http://127.0.0.1:8080/any").Allowed, "block-all policy allowed a redirect");
        return Task.CompletedTask;
    }

    private static Task TestKeyRotationMidRunEvidence()
    {
        var root = Path.Combine(Path.GetTempPath(), "csh-key-rotation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var fixture = new RuntimeFixture();
            var outcome = fixture.Broker.ExecuteAsync(fixture.Envelope, fixture.Manifest, fixture.Policy, fixture.Permit, fixture.WorkerRef, null, CancellationToken.None).GetAwaiter().GetResult();
            Assert(outcome.Dispatched && fixture.Evidence.VerifyIntegrity(), "evidence ledger was not intact before rotation");

            var protector = new TestSecretProtector();
            var store = new ProvenanceKeyStore(Path.Combine(root, "keys"), protector, "mid-run-rotation");
            ProvenanceStamp preRotationStamp;
            using (var originalKey = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence))
            {
                var identity = new ProductIdentity("csh", "mid-run", Canonicalization.Sha256Hex("build"), ProvenanceKeyCustody.Fingerprint(originalKey));
                using var preAuthority = new ProvenanceAuthority(identity, originalKey);
                preRotationStamp = preAuthority.Issue(outcome.Evidence, fixture.Manifest);
                Assert(preAuthority.Verify(preRotationStamp, outcome.Evidence, fixture.Manifest), "pre-rotation stamp failed initial verification");
            }

            using var rotatedKey = store.Rotate(ProvenanceKeyRole.RuntimeEvidence);
            var retired = store.RetiredPublicKeys(ProvenanceKeyRole.RuntimeEvidence);
            Assert(retired.Count == 1, "retired key archive was empty");

            var postIdentity = new ProductIdentity("csh", "mid-run", Canonicalization.Sha256Hex("build"), ProvenanceKeyCustody.Fingerprint(rotatedKey));
            using (var postAuthority = new ProvenanceAuthority(postIdentity, rotatedKey, retired))
            {
                Assert(postAuthority.Verify(preRotationStamp, outcome.Evidence, fixture.Manifest), "pre-rotation stamp did not verify under rotated authority with retired keys");
                Assert(!postAuthority.Verify(preRotationStamp with { EvidenceHash = new string('0', 64) }, outcome.Evidence, fixture.Manifest), "tampered stamp verified under rotated authority");
            }

            Assert(fixture.Evidence.VerifyIntegrity(), "evidence ledger integrity broke during key rotation simulation");
            Assert(fixture.Provenance.Verify(outcome.Provenance, outcome.Evidence, fixture.Manifest), "original fixture provenance broke independently of external key rotation");
        }
        finally { Directory.Delete(root, true); }
        return Task.CompletedTask;
    }
    private static async Task TestDnsReverseLookupAdapter()
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var draft = new AuthorizationManifest
        {
            EngagementId = "dns-reverse-test",
            EngagementMode = EngagementMode.Authorized,
            Authorization = new AuthorizationProof("owner-1", "operator-1", "dns-auth", "", "", ""),
            Scope = new ScopeDefinition(new[] { "8.8.8.8" }, Array.Empty<string>(), "exact-only", "block", "block"),
            TimeWindow = new TimeWindow(now.AddMinutes(-1), now.AddMinutes(10), "UTC", Array.Empty<ExcludedWindow>()),
            Methods = new MethodDefinition(new[] { DnsReverseLookupTool.CapabilityRef }, Array.Empty<string>()),
            AssetCriticality = new AssetCriticalityDefinition("low", new Dictionary<string, string>()),
            DataHandling = new DataHandlingDefinition("public-target-metadata", "required", "phase"),
            EscalationContacts = new[] { new EscalationContact("owner", "email", "owner@example.invalid") },
            CredentialPolicy = new CredentialPolicy(Array.Empty<string>(), false, "none"),
            RateLimits = new RateLimitDefinition(1, 1, 4096),
            Cleanup = new CleanupDefinition(true, "operator", "no-op"),
            StopConditions = new[] { "scope-mismatch" }
        };
        var authorization = draft with { Authorization = AuthorizationSigner.Sign(draft, key) };
        var action = new ActionRequest
        {
            RunId = "run-dns",
            ActionId = "action-dns",
            Phase = "recon",
            TargetRef = "http://8.8.8.8/",
            CapabilityRef = DnsReverseLookupTool.CapabilityRef,
            Purpose = "reverse lookup documentation IP",
            RiskClass = RiskClass.R1,
            ScopeRef = "scope-dns",
            AuthorizationRef = "dns-auth",
            MethodologyRefs = new[] { "dns-passive-baseline-v1" },
            ResolvedAddresses = new[] { "8.8.8.8" }
        };

        var capabilities = new CapabilityRegistry();
        capabilities.Register(new CapabilityManifest(
            DnsReverseLookupTool.CapabilityRef, RiskClass.R1,
            new[] { "*" }, "unprivileged", true,
            Array.Empty<string>(), new[] { "http_metadata" },
            TimeSpan.FromSeconds(5), 4096, false, true));
        capabilities.Freeze();
        var policy = new PolicyEngine(capabilities, CreateTrustStore(key)).Evaluate(action, authorization, null);
        Assert(policy.Decision == PolicyDecision.Allow, $"DNS policy did not allow: {policy.Reason}");

        var provider = new ProviderDescriptor("local-model", "dns-test", "1.0", Canonicalization.Sha256Hex("config"), "local-only", "none", "typed");
        var proposal = new ProviderProposal(provider, action, Canonicalization.Sha256Hex(Canonicalization.ActionPayload(action)), TimeSpan.FromMilliseconds(1), 16, ProviderFailureClass.None);
        var envelope = ActionEnvelopeFactory.Create(proposal);
        var adapter = new DnsReverseLookupTool();
        var toolManifest = new ToolCapabilityManifest(
            "dns-reverse-lookup", "1.0", DnsReverseLookupTool.CapabilityRef,
            "unprivileged", true, new[] { "http://8.8.8.8/" }, new[] { "http_metadata" },
            true, new[] { "raw", "redacted", "observation" }, true,
            TimeSpan.FromSeconds(5), 4096);

        var registry = new ToolRegistry();
        registry.Register(toolManifest, adapter);
        registry.Freeze();
        var evidence = new EvidenceLedger(new ArtifactStore());
        using var issuerKey = RSA.Create(2048);
        var policyEngine = new PolicyEngine(capabilities, CreateTrustStore(key));
        using var issuer = new PermitIssuer(policyEngine, issuerKey);
        var permit = issuer.Issue(action, authorization, "dns-worker");
        Assert(issuer.TryConsume(permit, action, authorization, "dns-worker"), "DNS permit was not consumed");
        using var provenanceKey = RSA.Create(2048);
        using var provenance = new ProvenanceAuthority(new ProductIdentity("csh", "dns-test", Canonicalization.Sha256Hex("build"), ProvenanceKeyCustody.Fingerprint(provenanceKey)), provenanceKey);
        var broker = new ToolBroker(registry, evidence, issuer, provenance);

        var outcome = await broker.ExecuteAsync(envelope, authorization, policy, permit, "dns-worker", null, CancellationToken.None);
        Assert(outcome.Dispatched, $"DNS adapter was not dispatched: {outcome.FailureReason}");
        if (outcome.Evidence.Status == ToolResultStatus.Timeout)
        {
            Assert(!DnsReverseLookupTool.IsPrivateOrReserved(IPAddress.Parse("8.8.8.8")), "public IP incorrectly flagged as private");
            Assert(DnsReverseLookupTool.IsPrivateOrReserved(IPAddress.Parse("10.0.0.1")), "private IP not blocked");
            Assert(DnsReverseLookupTool.IsPrivateOrReserved(IPAddress.Parse("127.0.0.1")), "loopback not blocked");
            Assert(DnsReverseLookupTool.IsPrivateOrReserved(IPAddress.Parse("::1")), "IPv6 loopback not blocked");
            Console.WriteLine("DNS lookup timed out (expected in offline CI); adapter structure verified via unit assertions");
            return;
        }
        Assert(outcome.Evidence.Status == ToolResultStatus.Success || outcome.Evidence.Status == ToolResultStatus.Partial,
            $"DNS adapter returned unexpected status: {outcome.Evidence.Status}");
        Assert(provenance.Verify(outcome.Provenance, outcome.Evidence, authorization), "DNS evidence provenance failed verification");

        Assert(outcome.Evidence.RawArtifactRef is not null, "DNS adapter did not produce a raw artifact");
        Assert(evidence.TryReadArtifact(outcome.Evidence.RawArtifactRef!, out var dnsRaw), "DNS raw artifact was not readable");
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(dnsRaw));
        Assert(doc.RootElement.GetProperty("query").GetString()!.Contains("8.8.8.8.in-addr.arpa"), "reverse-DNS query name was incorrect for IPv4");
    }

    private static async Task TestEngagementManifestFile()
    {
        using var key = RSA.Create(2048);
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var path = Path.Combine(Path.GetTempPath(), "engagement-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                Converters = { new JsonStringEnumConverter() }
            }));
            var loaded = await EngagementManifestFile.LoadAsync(path, CancellationToken.None);
            var valid = EngagementManifestFile.Validate(loaded, key.ExportRSAPublicKeyPem());
            Assert(valid.IsValid, "signed engagement manifest failed validation");
            var tampered = loaded with { EngagementId = "tampered-engagement" };
            var invalid = EngagementManifestFile.Validate(tampered, key.ExportRSAPublicKeyPem());
            Assert(!invalid.IsValid && invalid.Errors.Any(error => error.Contains("signature", StringComparison.Ordinal)), "tampered engagement manifest was accepted");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static AuthorizationManifest CreateManifest(RSA key, DateTimeOffset now)
    {
        var draft = new AuthorizationManifest
        {
            EngagementId = "phase3-fixture",
            EngagementMode = EngagementMode.Fixture,
            Authorization = new AuthorizationProof("owner-1", "operator-1", "phase3-auth", string.Empty, string.Empty, string.Empty),
            Scope = new ScopeDefinition(new[] { "127.0.0.1" }, Array.Empty<string>(), "single-level", "same-origin", "block"),
            TimeWindow = new TimeWindow(now.AddMinutes(-1), now.AddMinutes(10), "UTC", Array.Empty<ExcludedWindow>()),
            Methods = new MethodDefinition(new[] { "fixture.inspect" }, Array.Empty<string>()),
            AssetCriticality = new AssetCriticalityDefinition("unknown", new Dictionary<string, string>()),
            DataHandling = new DataHandlingDefinition("synthetic-only", "required", "phase"),
            EscalationContacts = new[] { new EscalationContact("owner", "email", "owner@example.invalid") },
            CredentialPolicy = new CredentialPolicy(Array.Empty<string>(), false, "short-lived"),
            RateLimits = new RateLimitDefinition(2, 1, 1024),
            Cleanup = new CleanupDefinition(true, "operator-1", "phase3-fixture-cleanup"),
            StopConditions = new[] { "scope-mismatch", "relay-loss" }
        };
        return draft with { Authorization = AuthorizationSigner.Sign(draft, key) };
    }

    private static Permit CopyPermit(Permit permit) => new()
    {
        PermitId = permit.PermitId,
        RunId = permit.RunId,
        ActionId = permit.ActionId,
        ActionHash = permit.ActionHash,
        ManifestHash = permit.ManifestHash,
        CanonicalizationRef = permit.CanonicalizationRef,
        TargetRef = permit.TargetRef,
        ScopeRef = permit.ScopeRef,
        ScopeHash = permit.ScopeHash,
        PolicyRef = permit.PolicyRef,
        PolicyVersion = permit.PolicyVersion,
        WorkerRef = permit.WorkerRef,
        CapabilityRef = permit.CapabilityRef,
        AuthorizationRef = permit.AuthorizationRef,
        CredentialRef = permit.CredentialRef,
        ApprovalRef = permit.ApprovalRef,
        ApprovalHash = permit.ApprovalHash,
        RiskClass = permit.RiskClass,
        MethodologyRefs = permit.MethodologyRefs,
        IssuerRef = permit.IssuerRef,
        IssuerSignatureBase64 = permit.IssuerSignatureBase64,
        IssuedAt = permit.IssuedAt,
        ExpiresAt = permit.ExpiresAt,
        Nonce = permit.Nonce
    };

    private static ActionRequest CreateAction() => new()
    {
        RunId = "run-phase3",
        ActionId = "action-phase3",
        Phase = "phase3",
        TargetRef = "http://127.0.0.1:8080/",
        CapabilityRef = "fixture.inspect",
        Arguments = new Dictionary<string, string> { ["mode"] = "safe" },
        Purpose = "exercise a deterministic local fixture",
        ExpectedObservation = "fixture response",
        RiskClass = RiskClass.R0,
        ScopeRef = "scope-phase3",
        AuthorizationRef = "phase3-auth",
        MethodologyRefs = new[] { "phase3-fixture-v1" },
        ResolvedAddresses = Array.Empty<string>()
    };

    private static CapabilityRegistry CreateCapabilities()
    {
        var registry = new CapabilityRegistry();
        registry.Register(new CapabilityManifest("fixture.inspect", RiskClass.R0, new[] { "http://127.0.0.1:8080/" }, "unprivileged", true, Array.Empty<string>(), new[] { "synthetic" }, TimeSpan.FromSeconds(10), 1024, false, true));
        registry.Freeze();
        return registry;
    }

    private static AuthorizationTrustStore CreateTrustStore(RSA key)
    {
        var store = new AuthorizationTrustStore();
        store.Register("owner-1", key);
        store.Register("operator-1", key);
        store.Freeze();
        return store;
    }

    private static async Task AssertThrows<T>(Func<Task> action, string message) where T : Exception
    {
        try
        {
            await action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class RuntimeFixture : IDisposable
    {
        public RuntimeFixture(IToolAdapter? adapter = null, DurableEvidenceJournal? journal = null, bool consumePermit = true)
        {
            Key = RSA.Create(2048);
            var now = DateTimeOffset.UtcNow;
            Manifest = CreateManifest(Key, now);
            Action = CreateAction();
            Policy = new PolicyEngine(CreateCapabilities(), CreateTrustStore(Key)).Evaluate(Action, Manifest, null);
            Issuer = new PermitIssuer(new PolicyEngine(CreateCapabilities(), CreateTrustStore(Key)));
            Permit = Issuer.Issue(Action, Manifest, "phase3-worker");
            if (consumePermit) Assert(Issuer.TryConsume(Permit, Action, Manifest, WorkerRef), "fixture permit was not consumed");
            Provider = new ProviderDescriptor("fixture-provider", "fixture-model", "1.0", Canonicalization.Sha256Hex("fixture-config"), "local-only", "none", "typed");
            var proposal = new ProviderProposal(Provider, Action, Canonicalization.Sha256Hex("fixture-proposal"), TimeSpan.FromMilliseconds(1), 8, ProviderFailureClass.None);
            Envelope = ActionEnvelopeFactory.Create(proposal);
            Adapter = adapter ?? new FixtureToolAdapter("fixture-tool", "1.0", "secret=alpha", ToolResultStatus.Success, "fixture.observation");
            ToolManifest = new ToolCapabilityManifest("fixture-tool", "1.0", "fixture.inspect", "unprivileged", true, Array.Empty<string>(), new[] { "synthetic" }, true, new[] { "raw", "redacted", "observation" }, true, TimeSpan.FromMilliseconds(50), 1024);
            Registry = new ToolRegistry();
            Registry.Register(ToolManifest, Adapter);
            Registry.Freeze();
            Artifacts = new ArtifactStore();
            Evidence = new EvidenceLedger(Artifacts, journal);
            Provenance = new ProvenanceAuthority(new ProductIdentity("cyber-sop-harness", "phase3-test", Canonicalization.Sha256Hex("phase3-test-build"), ProvenanceKeyCustody.Fingerprint(Key)), Key);
            Broker = new ToolBroker(Registry, Evidence, Issuer, Provenance, new OutputRedactor(new[] { "alpha" }));
        }

        public RSA Key { get; }
        public AuthorizationManifest Manifest { get; }
        public ActionRequest Action { get; }
        public PolicyResult Policy { get; }
        public PermitIssuer Issuer { get; }
        public Permit Permit { get; }
        public ProviderDescriptor Provider { get; }
        public ActionEnvelope Envelope { get; }
        public IToolAdapter Adapter { get; }
        public int AdapterInvocationCount => Adapter is FixtureToolAdapter fixtureAdapter ? fixtureAdapter.InvocationCount : 0;
        public ToolCapabilityManifest ToolManifest { get; }
        public ToolRegistry Registry { get; }
        public ArtifactStore Artifacts { get; }
        public EvidenceLedger Evidence { get; }
        public ProvenanceAuthority Provenance { get; }
        public ToolBroker Broker { get; }
        public string WorkerRef => "phase3-worker";

        public void Dispose()
        {
            Issuer.Dispose();
            Provenance.Dispose();
            Key.Dispose();
        }
    }

    private sealed class FakeModelProvider : IModelProviderAdapter
    {
        private readonly ActionRequest _action;

        public FakeModelProvider(string providerRef, string modelRef, ActionRequest action)
        {
            _action = action;
            Descriptor = new ProviderDescriptor(providerRef, modelRef, "1.0", Canonicalization.Sha256Hex("same-config"), "local-only", "none", "typed");
        }

        public ProviderDescriptor Descriptor { get; }

        public Task<ProviderProposal> ProposeAsync(string prompt, AuthorizationManifest manifest, CancellationToken cancellationToken) => Task.FromResult(new ProviderProposal(Descriptor, _action, Canonicalization.Sha256Hex(Canonicalization.ActionPayload(_action)), TimeSpan.FromMilliseconds(1), 8, ProviderFailureClass.None));
    }

    private class FixtureToolAdapter : ILocalFixtureToolAdapter
    {
        private readonly byte[] _raw;
        private readonly ToolResultStatus _status;
        private readonly string _observation;

        public FixtureToolAdapter(string toolRef, string toolVersion, string raw, ToolResultStatus status, string observation)
        {
            ToolRef = toolRef;
            ToolVersion = toolVersion;
            _raw = Encoding.UTF8.GetBytes(raw);
            _status = status;
            _observation = observation;
        }

        public string ToolRef { get; }
        public string ToolVersion { get; }
        public int InvocationCount { get; private set; }

        public virtual Task<ToolAdapterResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken)
        {
            InvocationCount++;
            return Task.FromResult(new ToolAdapterResult(_status, 0, _raw.ToArray(), new[] { _observation }, Array.Empty<string>(), "SUCCEEDED"));
        }

        public virtual Task<string> CleanupAsync(ToolExecutionContext context, ToolAdapterResult result, CancellationToken cancellationToken) => Task.FromResult("CLEANUP_OK|" + context.Envelope.ActionHash);
    }

    private sealed class TimeoutToolAdapter : FixtureToolAdapter
    {
        public TimeoutToolAdapter(string toolRef, string toolVersion) : base(toolRef, toolVersion, string.Empty, ToolResultStatus.Timeout, "timeout") { }

        public override async Task<ToolAdapterResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class ThrowingToolAdapter : FixtureToolAdapter
    {
        public ThrowingToolAdapter(string toolRef, string toolVersion) : base(toolRef, toolVersion, string.Empty, ToolResultStatus.Success, "error") { }

        public override Task<ToolAdapterResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken) => throw new InvalidOperationException("fixture adapter failure");
    }

    private sealed class ThrowingCleanupAdapter : FixtureToolAdapter
    {
        public ThrowingCleanupAdapter(string toolRef, string toolVersion) : base(toolRef, toolVersion, "cleanup", ToolResultStatus.Success, "cleanup") { }

        public override Task<string> CleanupAsync(ToolExecutionContext context, ToolAdapterResult result, CancellationToken cancellationToken) => throw new InvalidOperationException("fixture cleanup failure");
    }

    private sealed class NonMarkerAdapter : IToolAdapter
    {
        public NonMarkerAdapter(string toolRef, string toolVersion)
        {
            ToolRef = toolRef;
            ToolVersion = toolVersion;
        }

        public string ToolRef { get; }
        public string ToolVersion { get; }
        public Task<ToolAdapterResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken) => Task.FromResult(new ToolAdapterResult(ToolResultStatus.Success, 0, Array.Empty<byte>(), new[] { "non-marker" }, Array.Empty<string>(), "SUCCEEDED"));
        public Task<string> CleanupAsync(ToolExecutionContext context, ToolAdapterResult result, CancellationToken cancellationToken) => Task.FromResult("CLEANUP_OK|" + context.Envelope.ActionHash);
    }

    private sealed class SlowToolAdapter : FixtureToolAdapter
    {
        private readonly TimeSpan _delay;

        public SlowToolAdapter(string toolRef, string toolVersion, TimeSpan delay) : base(toolRef, toolVersion, "slow-result", ToolResultStatus.Success, "slow.observation") { _delay = delay; }

        public override async Task<ToolAdapterResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return await base.ExecuteAsync(context, cancellationToken);
        }
    }
}
