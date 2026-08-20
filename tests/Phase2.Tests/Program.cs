using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CyberSopHarness.Core;

internal static class Program
{
    private static int _passed;

    public static async Task<int> Main()
    {
        await Run("manifest signature and validation", TestManifestValidation);
        await Run("scope and redirect decisions", TestScope);
        await Run("policy risk and approval decisions", TestPolicy);
        await Run("permit signature, consumption, expiry, and replay", TestPermits);
        await Run("credential vault encryption and revocation", TestCredentials);
        await Run("rate limiting and concurrency", TestRateLimiter);
        await Run("worker containment and permit enforcement", TestWorkers);
        await Run("stop all active workers", TestWorkerStop);
        await Run("rollback order and idempotence", TestRollback);
        await Run("Windows Job Object setup and termination", TestWindowsJobObject);
        Console.WriteLine($"phase2_tests=passed count={_passed}");
        return 0;
    }

    private static async Task Run(string name, Func<Task> test)
    {
        await test();
        _passed++;
        Console.WriteLine($"PASS {name}");
    }

    private static AuthorizationManifest CreateManifest(RSA key, DateTimeOffset now, EngagementMode mode = EngagementMode.Fixture)
    {
        var draft = new AuthorizationManifest
        {
            EngagementId = "fixture-phase2",
            EngagementMode = mode,
            Authorization = new AuthorizationProof("owner-1", "operator-1", "auth-artifact-1", string.Empty, string.Empty, string.Empty),
            Scope = new ScopeDefinition(
                new[] { "127.0.0.1", "*.fixture.local", "198.51.100.0/24" },
                new[] { "blocked.fixture.local" },
                "single-level",
                "same-origin",
                "block"),
            TimeWindow = new TimeWindow(now.AddMinutes(-1), now.AddMinutes(10), "UTC", Array.Empty<ExcludedWindow>()),
            Methods = new MethodDefinition(new[] { "fixture.inspect", "fixture.state" }, new[] { "fixture.prohibited" }),
            AssetCriticality = new AssetCriticalityDefinition("unknown", new Dictionary<string, string> { ["127.0.0.1"] = "non-production" }),
            DataHandling = new DataHandlingDefinition("synthetic-only", "required", "phase"),
            EscalationContacts = new[] { new EscalationContact("owner", "email", "owner@example.invalid") },
            CredentialPolicy = new CredentialPolicy(Array.Empty<string>(), false, "five-minutes"),
            RateLimits = new RateLimitDefinition(2, 1, 1024),
            Cleanup = new CleanupDefinition(true, "operator-1", "fixture-cleanup-v1"),
            StopConditions = new[] { "sensitive-data", "scope-mismatch", "relay-loss" }
        };
        return draft with { Authorization = AuthorizationSigner.Sign(draft, key) };
    }

    private static ActionRequest CreateAction(string target = "http://127.0.0.1:8080/", RiskClass risk = RiskClass.R0, string? approvalRef = null) => new()
    {
        RunId = "run-phase2",
        ActionId = "action-" + Guid.NewGuid().ToString("N"),
        Phase = "fixture",
        TargetRef = target,
        CapabilityRef = "fixture.inspect",
        Arguments = new Dictionary<string, string> { ["mode"] = "safe" },
        Purpose = "exercise a local fixture",
        ExpectedObservation = "fixture response",
        RiskClass = risk,
        ScopeRef = "scope-phase2",
        AuthorizationRef = "auth-artifact-1",
        MethodologyRefs = new[] { "fixture-v1" },
        ApprovalRef = approvalRef
    };

    private static CapabilityRegistry CreateCapabilities()
    {
        var registry = new CapabilityRegistry();
        registry.Register(new CapabilityManifest("fixture.inspect", RiskClass.R0, new[] { "http://127.0.0.1:8080/" }, "unprivileged", true, Array.Empty<string>(), new[] { "synthetic" }, TimeSpan.FromSeconds(10), 1024, false, true));
        registry.Register(new CapabilityManifest("fixture.state", RiskClass.R3, new[] { "http://127.0.0.1:8080/" }, "unprivileged", false, Array.Empty<string>(), new[] { "synthetic" }, TimeSpan.FromSeconds(10), 1024, true, true));
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

    private static PolicyEngine CreatePolicy(RSA key) => new(CreateCapabilities(), CreateTrustStore(key));

    private static ApprovalRecord CreateApproval(ActionRequest action, AuthorizationManifest manifest, RSA key, DateTimeOffset now)
    {
        var approval = new ApprovalRecord(action.ApprovalRef ?? "approval-1", action.RunId, action.ActionId, Canonicalization.ActionHash(action), Canonicalization.AuthorizationHash(manifest), action.TargetRef, action.CapabilityRef, action.RiskClass, "operator-1", now.AddMinutes(2), "fixture approval", "approval-nonce-1");
        return ApprovalSigner.Sign(approval, key);
    }

    private static Task TestManifestValidation()
    {
        var now = DateTimeOffset.UtcNow;
        using var key = RSA.Create(2048);
        var manifest = CreateManifest(key, now);
        var valid = ManifestValidation.Validate(manifest, CreateTrustStore(key));
        Assert(valid.IsValid, string.Join(";", valid.Errors));
        var invalid = manifest with { Authorization = manifest.Authorization with { SignatureBase64 = "invalid" } };
        Assert(!ManifestValidation.Validate(invalid, CreateTrustStore(key)).IsValid, "invalid signature accepted");
        using var otherKey = RSA.Create(2048);
        Assert(!ManifestValidation.Validate(manifest, CreateTrustStore(otherKey)).IsValid, "self-authenticated authority accepted");
        var excludedDraft = manifest with { TimeWindow = manifest.TimeWindow with { ExcludedWindows = new[] { new ExcludedWindow(now.AddMinutes(-1), now.AddMinutes(1), "fixture freeze") } } };
        var excluded = excludedDraft with { Authorization = AuthorizationSigner.Sign(excludedDraft, key) };
        Assert(!ManifestValidation.Validate(excluded, CreateTrustStore(key)).IsValid, "excluded window was ignored");
        var tampered = manifest with { RateLimits = manifest.RateLimits with { RequestsPerSecond = 99 } };
        Assert(!ManifestValidation.Validate(tampered, CreateTrustStore(key)).IsValid, "unsigned manifest field mutation accepted");
        return Task.CompletedTask;
    }

    private static Task TestScope()
    {
        var now = DateTimeOffset.UtcNow;
        using var key = RSA.Create(2048);
        var fixture = CreateManifest(key, now);
        var evaluator = new ScopeEvaluator(fixture);
        Assert(evaluator.Evaluate("http://127.0.0.1:8080/").Allowed, "loopback fixture was blocked");
        Assert(evaluator.Evaluate("api.fixture.local").Allowed, "single-level wildcard was blocked");
        Assert(!evaluator.Evaluate("deep.api.fixture.local").Allowed, "single-level wildcard became recursive");
        Assert(!evaluator.Evaluate("blocked.fixture.local").Allowed, "deny list was ignored");
        Assert(evaluator.Evaluate("198.51.100.10").Allowed, "documentation CIDR was not accepted");
        Assert(!evaluator.Evaluate("169.254.169.254").Allowed, "metadata endpoint was allowed");
        var mappedMetadata = fixture with { Scope = fixture.Scope with { Allow = new[] { "::ffff:169.254.169.254" } } };
        Assert(!new ScopeEvaluator(mappedMetadata).Evaluate("::ffff:169.254.169.254").Allowed, "IPv4-mapped metadata endpoint was allowed");
        var mappedScope = fixture with { Scope = fixture.Scope with { Allow = new[] { "198.51.100.10" } } };
        Assert(new ScopeEvaluator(mappedScope).Evaluate("::ffff:198.51.100.10").Allowed, "IPv4-mapped exact scope was not normalized");
        var mappedCidr = fixture with { Scope = fixture.Scope with { Allow = new[] { "::ffff:198.51.100.0/120" } } };
        Assert(new ScopeEvaluator(mappedCidr).Evaluate("::ffff:198.51.100.10").Allowed, "IPv4-mapped CIDR scope was not normalized");
        Assert(!evaluator.Evaluate("file://127.0.0.1/etc/passwd").Allowed, "file scheme was allowed");
        Assert(!evaluator.EvaluateRedirect("api.fixture.local", "https://outside.invalid/").Allowed, "out-of-scope redirect was allowed");
        Assert(!evaluator.EvaluateRedirect("https://api.fixture.local/", "http://api.fixture.local:80/").Allowed, "cross-scheme redirect was allowed");
        var authorized = fixture with { EngagementMode = EngagementMode.Authorized, Scope = fixture.Scope with { Allow = new[] { "api.fixture.local", "198.51.100.0/24" } } };
        var authorizedEvaluator = new ScopeEvaluator(authorized);
        Assert(authorizedEvaluator.Evaluate("https://api.fixture.local/", new[] { "198.51.100.10" }).Allowed, "authorized resolved public address was rejected");
        Assert(!authorizedEvaluator.Evaluate("https://api.fixture.local/", new[] { "169.254.169.254" }).Allowed, "authorized metadata resolution was allowed");
        var authorizedDenied = authorized with { Scope = authorized.Scope with { Deny = new[] { "198.51.100.10" } } };
        Assert(!new ScopeEvaluator(authorizedDenied).Evaluate("https://api.fixture.local/", new[] { "198.51.100.10" }).Allowed, "authorized resolved deny entry was ignored");
        return Task.CompletedTask;
    }

    private static Task TestPolicy()
    {
        var now = DateTimeOffset.UtcNow;
        using var key = RSA.Create(2048);
        var manifest = CreateManifest(key, now);
        var policy = CreatePolicy(key);
        var low = CreateAction();
        Assert(policy.Evaluate(low, manifest, null).Decision == PolicyDecision.Allow, "low-risk fixture action was not allowed");
        AssertThrows<ArgumentNullException>(() => Task.FromResult(policy.Evaluate(null!, manifest, null)), "null action was not rejected").GetAwaiter().GetResult();
        Assert(policy.Evaluate(low with { Purpose = string.Empty }, manifest, null).Decision == PolicyDecision.Block, "incomplete action was authorized");
        Assert(policy.Evaluate(low with { AuthorizationRef = "wrong-artifact" }, manifest, null).Decision == PolicyDecision.Block, "mismatched authorization reference was accepted");
        Assert(policy.Evaluate(low with { Arguments = null!, MethodologyRefs = null!, ResolvedAddresses = null! }, manifest, null).Decision == PolicyDecision.Block, "null action collections were not blocked");
        Assert(policy.Evaluate(low with { MethodologyRefs = new[] { "" } }, manifest, null).Decision == PolicyDecision.Block, "blank methodology reference was accepted");
        var unknown = low with { CapabilityRef = "fixture.unknown" };
        Assert(policy.Evaluate(unknown, manifest, null).Decision == PolicyDecision.Block, "unknown capability was not blocked");
        Assert(policy.Evaluate(CreateAction("https://outside.invalid/"), manifest, null).Decision == PolicyDecision.Block, "out-of-scope action was not blocked");
        var high = CreateAction(risk: RiskClass.R3, approvalRef: "approval-1") with { CapabilityRef = "fixture.state" };
        Assert(policy.Evaluate(high, manifest, null).Decision == PolicyDecision.ApprovalRequired, "R3 action did not require approval");
        var approval = CreateApproval(high, manifest, key, now);
        Assert(policy.Evaluate(high, manifest, approval).Decision == PolicyDecision.Allow, "valid R3 approval was rejected");
        var unsignedApproval = approval with { SignatureBase64 = "invalid" };
        Assert(policy.Evaluate(high, manifest, unsignedApproval).Decision == PolicyDecision.ApprovalRequired, "unsigned approval was accepted");
        Assert(policy.Evaluate(CreateAction(risk: RiskClass.R4), manifest, null).Decision == PolicyDecision.Block, "R4 action was not blocked");
        return Task.CompletedTask;
    }

    private static async Task TestPermits()
    {
        var now = DateTimeOffset.UtcNow;
        using var key = RSA.Create(2048);
        var manifest = CreateManifest(key, now);
        var action = CreateAction();
        var policy = CreatePolicy(key);
        using var issuer = new PermitIssuer(policy);
        var permit = issuer.Issue(action, manifest, "worker-1");
        Assert(issuer.Verify(permit), "permit signature failed");
        Assert(issuer.TryConsume(permit, action, manifest, "worker-1"), "valid permit was not consumed");
        Assert(!issuer.TryConsume(permit, action, manifest, "worker-1"), "permit replay succeeded");
        var expiredAction = CreateAction();
        var expired = issuer.Issue(expiredAction, manifest, "worker-1", lifetime: TimeSpan.FromMilliseconds(1));
        Thread.Sleep(25);
        Assert(!issuer.TryConsume(expired, expiredAction, manifest, "worker-1"), "expired permit consumed");
        Assert(expired.ConsumptionState == PermitConsumptionState.Expired, "expired permit state was not recorded");
        var highAction = CreateAction(risk: RiskClass.R3, approvalRef: "approval-high") with { CapabilityRef = "fixture.state" };
        var highApproval = CreateApproval(highAction, manifest, key, now);
        var highPermit = issuer.Issue(highAction, manifest, "worker-1", highApproval);
        Assert(highPermit.ApprovalRef == "approval-high", "permit lost approval binding");
        await AssertThrows<InvalidOperationException>(() => Task.FromResult(issuer.Issue(highAction, manifest, "worker-1", highApproval)), "approval was replayed for a second permit");
        var outOfScope = action with { TargetRef = "https://outside.invalid/" };
        await AssertThrows<InvalidOperationException>(() => Task.FromResult(issuer.Issue(outOfScope, manifest, "worker-1")), "permit issuer accepted a newly out-of-scope action");
        return;
    }

    private static Task TestCredentials()
    {
        var now = DateTimeOffset.UtcNow;
        using var vault = new CredentialVault();
        var handle = vault.Store("synthetic-secret", now.AddMinutes(1));
        var observed = vault.Use(handle, value => Encoding.UTF8.GetString(value));
        Assert(observed == "synthetic-secret", "vault did not decrypt through callback");
        Assert(vault.Count == 1, "vault count incorrect");
        vault.Revoke(handle);
        Assert(vault.Count == 0, "credential was not revoked");
        return Task.CompletedTask;
    }

    private static Task TestRateLimiter()
    {
        var limiter = new RateLimiter(new RateLimitDefinition(2, 1, 1024));
        Assert(limiter.TryAcquire("127.0.0.1", 100, out var first) && first is not null, "first rate-limit token denied");
        Assert(!limiter.TryAcquire("127.0.0.1", 100, out _), "concurrency limit ignored");
        Assert(limiter.Release(first!), "first lease was not released");
        Assert(limiter.TryAcquire("127.0.0.1", 100, out var second) && second is not null, "second token denied after release");
        Assert(limiter.Release(second!), "second lease was not released");
        Assert(!limiter.TryAcquire("127.0.0.1", 100, out _), "per-second limit ignored");
        Thread.Sleep(600);
        Assert(limiter.TryAcquire("127.0.0.1", 100, out _), "rate window did not refresh");
        return Task.CompletedTask;
    }

    private static async Task TestWorkers()
    {
        var now = DateTimeOffset.UtcNow;
        using var key = RSA.Create(2048);
        var manifest = CreateManifest(key, now);
        var action = CreateAction();
        var policy = CreatePolicy(key);
        using var issuer = new PermitIssuer(policy);
        var permit = issuer.Issue(action, manifest, "fixture-worker");
        var capabilities = CreateCapabilities();
        using var authority = new ContainmentAuthority();
        using var vault = new CredentialVault();
        var supervisor = new WorkerSupervisor(manifest, capabilities, authority, new RollbackLedger(), vault, issuer);
        var worker = new FixtureWorker("fixture-worker", authority, (_, _) => Task.FromResult(new WorkerResult("SUCCESS", "fixture-artifact", new string('a', 64), 64)));
        Assert(!authority.Verify(worker.Containment, worker.WorkerRef, EngagementMode.Authorized), "fixture containment was accepted for authorized mode");
        var result = await supervisor.ExecuteAsync(permit, action, manifest, worker, CancellationToken.None);
        Assert(result.Status == "SUCCESS", "fixture worker result incorrect");
        await AssertThrows<InvalidOperationException>(() => supervisor.ExecuteAsync(permit, action, manifest, worker, CancellationToken.None), "consumed permit allowed worker replay");
        var uncontained = new UncontainedWorker();
        var secondAction = CreateAction();
        var secondPermit = issuer.Issue(secondAction, manifest, "uncontained");
        await AssertThrows<InvalidOperationException>(() => supervisor.ExecuteAsync(secondPermit, secondAction, manifest, uncontained, CancellationToken.None), "uncontained worker was accepted");
        var oversizedAction = action with { ActionId = "oversized", Arguments = new Dictionary<string, string> { ["payload_bytes"] = "1024" } };
        var oversizedPermit = issuer.Issue(oversizedAction, manifest, "fixture-worker");
        var oversizedWorker = new FixtureWorker("fixture-worker", authority, (_, _) => Task.FromResult(new WorkerResult("SUCCESS", "oversized", new string('b', 64), 2048)));
        await AssertThrows<InvalidOperationException>(() => supervisor.ExecuteAsync(oversizedPermit, oversizedAction, manifest, oversizedWorker, CancellationToken.None), "worker output limit was bypassed");

        var authorizedManifest = manifest with { EngagementMode = EngagementMode.Authorized };
        await AssertThrows<InvalidOperationException>(() => supervisor.ExecuteAsync(permit, action, authorizedManifest, worker, CancellationToken.None), "authorized dispatch was not blocked without a trusted provider");

        using var rogueIssuer = new PermitIssuer(policy);
        var rogueAction = CreateAction();
        var roguePermit = rogueIssuer.Issue(rogueAction, manifest, "fixture-worker");
        await AssertThrows<InvalidOperationException>(() => supervisor.ExecuteAsync(roguePermit, rogueAction, manifest, worker, CancellationToken.None), "permit from an unbound issuer was accepted");
    }

    private static async Task TestWorkerStop()
    {
        var now = DateTimeOffset.UtcNow;
        using var key = RSA.Create(2048);
        var manifest = CreateManifest(key, now);
        var action = CreateAction();
        var policy = CreatePolicy(key);
        using var issuer = new PermitIssuer(policy);
        var permit = issuer.Issue(action, manifest, "long-worker");
        using var authority = new ContainmentAuthority();
        using var vault = new CredentialVault();
        var supervisor = new WorkerSupervisor(manifest, CreateCapabilities(), authority, new RollbackLedger(), vault, issuer);
        var worker = new FixtureWorker("long-worker", authority, async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new WorkerResult("UNREACHABLE", "none", new string('0', 64), 0);
        });
        var running = supervisor.ExecuteAsync(permit, action, manifest, worker, CancellationToken.None);
        await Task.Delay(50);
        Assert(!running.IsCompleted, "fixture worker was not running");
        var relayLoss = new RelayLossController(issuer, supervisor);
        await relayLoss.HandleAsync();
        await AssertThrows<OperationCanceledException>(async () => await running, "worker did not stop from cancellation");
        await AssertThrows<InvalidOperationException>(() => Task.FromResult(issuer.Issue(CreateAction(), manifest, "long-worker")), "permit issuance continued after relay loss");

        using var directIssuer = new PermitIssuer(policy);
        var directSupervisor = new WorkerSupervisor(manifest, CreateCapabilities(), authority, new RollbackLedger(), vault, directIssuer);
        await directSupervisor.StopAllAsync("operator-stop");
        await AssertThrows<InvalidOperationException>(() => Task.FromResult(directIssuer.Issue(CreateAction(), manifest, "long-worker")), "direct stop did not latch permit issuance");

        using var forcedIssuer = new PermitIssuer(policy);
        var forcedSupervisor = new WorkerSupervisor(manifest, CreateCapabilities(), authority, new RollbackLedger(), vault, forcedIssuer);
        var forcedAction = CreateAction();
        var forcedPermit = forcedIssuer.Issue(forcedAction, manifest, "forced-worker");
        var forcedWorker = new ForcedStopWorker("forced-worker", authority);
        var forcedRunning = forcedSupervisor.ExecuteAsync(forcedPermit, forcedAction, manifest, forcedWorker, CancellationToken.None);
        await Task.Delay(50);
        await forcedSupervisor.StopAllAsync("forced-test", TimeSpan.FromMilliseconds(50));
        await AssertThrows<OperationCanceledException>(async () => await forcedRunning, "forced-stop worker did not cancel");
        Assert(forcedWorker.ForceCalled, "forced stop was not attempted after graceful stop timeout");
    }

    private static async Task TestRollback()
    {
        var ledger = new RollbackLedger();
        var order = new List<string>();
        ledger.Register("first", () => { order.Add("first"); return Task.CompletedTask; });
        ledger.Register("second", () => { order.Add("second"); return Task.CompletedTask; });
        var report = await ledger.ExecuteAsync();
        Assert(report.Failed.Count == 0, "rollback reported unexpected failure");
        Assert(string.Join(",", order) == "second,first", "rollback order was not reverse registration order");
        var secondReport = await ledger.ExecuteAsync();
        Assert(secondReport.Completed.Count == 0, "rollback was not idempotent");
    }

    private static Task TestWindowsJobObject()
    {
        if (!OperatingSystem.IsWindows()) return Task.CompletedTask;
        using var job = new WindowsJobObject();
        Assert(job.IsValid, "Windows Job Object handle is invalid");
        var shell = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        using var contained = WindowsContainedProcess.Start(job, shell, "/c timeout /t 30 /nobreak >nul");
        using var process = Process.GetProcessById(contained.ProcessId);
        contained.Stop();
        process.WaitForExit(5000);
        Assert(process.HasExited, "Job Object did not terminate the fixture process");
        return Task.CompletedTask;
    }

    private sealed class UncontainedWorker : IContainedWorker
    {
        public string WorkerRef => "uncontained";
        public ContainmentAttestation Containment => new("uncontained", "forged", new string('0', 64), false, "authorized", "unprivileged", false, "invalid");
        public Task<WorkerResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken) => Task.FromResult(new WorkerResult("BAD", "none", new string('0', 64), 0));
        public Task StopAsync(string reason) => Task.CompletedTask;
        public Task ForceStopAsync(string reason) => Task.CompletedTask;
    }

    private sealed class ForcedStopWorker : IContainedWorker
    {
        public ForcedStopWorker(string workerRef, ContainmentAuthority authority)
        {
            WorkerRef = workerRef;
            Containment = authority.IssueFixture(workerRef, Canonicalization.Sha256Hex("forced-stop-worker:" + workerRef));
        }

        public string WorkerRef { get; }
        public ContainmentAttestation Containment { get; }
        public bool ForceCalled { get; private set; }

        public async Task<WorkerResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new WorkerResult("UNREACHABLE", "none", new string('0', 64), 0);
        }

        public Task StopAsync(string reason) => Task.Delay(Timeout.InfiniteTimeSpan);

        public Task ForceStopAsync(string reason)
        {
            ForceCalled = true;
            return Task.CompletedTask;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
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
}
