using System.Security.Cryptography;
using System.Text;
using CyberSopHarness.Core;

/// <summary>
/// PROBE BATTERY: Policy Engine
/// 
/// Purpose: Exhaustively validate every decision path through PolicyEngine.Evaluate.
/// This is NOT a smoke test. Each probe targets a specific policy dimension,
/// boundary condition, or adversarial input that the engine must handle correctly.
///
/// Coverage dimensions:
///   1. Scope evaluation (CIDR, wildcard, deny-list, hard-deny patterns)
///   2. Risk class boundaries (R0-R4, approval requirements)
///   3. Method policy (allowed, prohibited, wildcard, disallowed)
///   4. Time window enforcement
///   5. Manifest validation (signature, trust store, tampering)
///   6. Redirect policy (same-origin, cross-origin, block)
///   7. Action request completeness (missing fields, malformed types)
///   8. Authorization reference binding
///   9. Approval record binding (R3/R4, expiry, nonce, hash mismatch)
///  10. Risk class escalation and denial
///
/// Dependency matrix:
///   - RSA key generation (System.Security.Cryptography)
///   - CapabilityRegistry (frozen before policy construction)
///   - AuthorizationTrustStore (frozen before policy construction)
///   - Canonicalization (deterministic hashing)
///   - AuthoritativeClock (testable via constructor)
///
/// Pitfalls:
///   - Clock skew: tests must construct manifests with explicit time windows
///   - Registry freeze order: capabilities and trust store MUST be frozen
///   - RSA key reuse: each manifest should use consistent key material
///   - CIDR parsing edge cases: /0 prefix, IPv4-mapped-IPv6
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    public static async Task<int> Main()
    {
        // === SCOPE PROBES ===
        await Run("scope: CIDR match allows action in allowed range", TestScopeCidrMatch);
        await Run("scope: CIDR miss blocks action outside allowed range", TestScopeCidrMiss);
        await Run("scope: wildcard single-level matches immediate subdomain", TestScopeWildcardSingleLevel);
        await Run("scope: wildcard single-level blocks multi-level subdomain", TestScopeWildcardSingleLevelBlock);
        await Run("scope: wildcard recursive matches deep subdomain", TestScopeWildcardRecursive);
        await Run("scope: deny-list blocks explicitly denied host", TestScopeDenyList);
        await Run("scope: hard-deny blocks cloud metadata endpoint", TestScopeHardDenyMetadata);
        await Run("scope: hard-deny blocks link-local address", TestScopeHardDenyLinkLocal);
        await Run("scope: allow-list entry with port suffix", TestScopeAllowWithPort);
        await Run("scope: empty target blocks", TestScopeEmptyTarget);
        await Run("scope: IP address literal matches CIDR entry", TestScopeIpLiteralMatch);
        await Run("scope: IPv6 loopback handled correctly", TestScopeIPv6Loopback);
        await Run("scope: blocked fixture host rejected by deny-list", TestScopeBlockedFixtureHost);

        // === METHOD POLICY PROBES ===
        await Run("methods: allowed capability passes", TestMethodsAllowed);
        await Run("methods: prohibited capability blocked", TestMethodsProhibited);
        await Run("methods: unknown capability blocked", TestMethodsUnknown);
        await Run("methods: wildcard allow permits any capability", TestMethodsWildcardAllow);
        await Run("methods: case-insensitive method matching", TestMethodsCaseInsensitive);

        // === RISK CLASS PROBES ===
        await Run("risk: R0 action allowed without approval", TestRiskR0);
        await Run("risk: R1 action allowed without approval", TestRiskR1);
        await Run("risk: R2 action allowed without approval", TestRiskR2);
        await Run("risk: R3 action requires approval", TestRiskR3RequiresApproval);
        await Run("risk: R3 without approval blocks", TestRiskR3NoApproval);
        await Run("risk: R4 always blocked", TestRiskR4Blocked);

        // === APPROVAL RECORD PROBES ===
        await Run("approval: valid R3 approval allows action", TestApprovalValid);
        await Run("approval: expired R3 approval blocks", TestApprovalExpired);
        await Run("approval: wrong approver ref blocks", TestApprovalWrongApprover);
        await Run("approval: action hash mismatch blocks", TestApprovalHashMismatch);
        await Run("approval: nonce required for R3", TestApprovalNonceRequired);

        // === ACTION REQUEST PROBES ===
        await Run("action: missing type field blocks", TestActionMissingType);
        await Run("action: wrong type value blocks", TestActionWrongType);
        await Run("action: missing run ID blocks", TestActionMissingRunId);
        await Run("action: empty method refs blocks", TestActionEmptyMethodRefs);
        await Run("action: null arguments blocks", TestActionNullArguments);
        await Run("action: authorization ref mismatch blocks", TestAuthRefMismatch);

        // === MANIFEST PROBES ===
        await Run("manifest: tampered signature blocks", TestManifestTamperedSignature);
        await Run("manifest: expired time window blocks", TestManifestExpiredWindow);
        await Run("manifest: future start time blocks", TestManifestFutureStart);
        await Run("manifest: excluded window blocks action in exclusion", TestManifestExcludedWindow);
        await Run("manifest: unregistered trust key blocks", TestManifestUnregisteredKey);

        // === REDIRECT PROBES ===
        await Run("redirect: same-origin redirect allowed", TestRedirectSameOrigin);
        await Run("redirect: cross-origin redirect blocked when policy is same-origin", TestRedirectCrossOriginBlocked);
        await Run("redirect: redirect to hard-deny host blocked", TestRedirectHardDenyHost);
        await Run("redirect: redirect with invalid URI blocked", TestRedirectInvalidUri);

        // === DECISION BINDING PROBES ===
        await Run("binding: policy hash matches manifest", TestBindingPolicyHash);
        await Run("binding: policy action hash matches envelope", TestBindingActionHash);
        await Run("binding: authorization ref bound to manifest", TestBindingAuthRef);

        Console.WriteLine($"\npolicy_engine_battery=passed count={_passed} failed count={_failed}");
        return _failed > 0 ? 1 : 0;
    }

    // ── Helpers ──

    private static async Task Run(string name, Func<Task> test)
    {
        try { await test(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception ex) { _failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
    }

    private static RSA CreateKey() => RSA.Create(2048);

    private static AuthorizationManifest CreateManifest(RSA key, DateTimeOffset now,
        string wildcardPolicy = "single-level",
        string redirectPolicy = "same-origin",
        EngagementMode mode = EngagementMode.Fixture,
        TimeSpan? windowOffset = null,
        IReadOnlyList<ExcludedWindow>? excluded = null)
    {
        var start = now.AddMinutes(-1);
        var expiry = now.AddMinutes(10);
        if (windowOffset.HasValue) { start = now + windowOffset.Value; }

        var draft = new AuthorizationManifest
        {
            EngagementId = "probe-battery",
            EngagementMode = mode,
            Authorization = new AuthorizationProof("owner-1", "operator-1", "auth-artifact-1", string.Empty, string.Empty, string.Empty),
            Scope = new ScopeDefinition(
                new[] { "127.0.0.1", "*.fixture.local", "198.51.100.0/24" },
                new[] { "blocked.fixture.local" },
                wildcardPolicy,
                redirectPolicy,
                "block"),
            TimeWindow = new TimeWindow(start, expiry, "UTC", excluded ?? Array.Empty<ExcludedWindow>()),
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

    private static ActionRequest CreateAction(string target = "http://127.0.0.1:8080/",
        RiskClass risk = RiskClass.R0, string? approvalRef = null, string? authRef = null,
        string capability = "fixture.inspect")
    {
        return new()
        {
            RunId = "run-probe",
            ActionId = "action-" + Guid.NewGuid().ToString("N"),
            Phase = "probe",
            TargetRef = target,
            CapabilityRef = capability,
            Arguments = new Dictionary<string, string> { ["mode"] = "safe" },
            Purpose = "probe battery test",
            ExpectedObservation = "expected",
            RiskClass = risk,
            ScopeRef = "scope-probe",
            AuthorizationRef = authRef ?? "auth-artifact-1",
            MethodologyRefs = new[] { "fixture-v1" },
            ApprovalRef = approvalRef,
            ResolvedAddresses = new[] { "127.0.0.1" }
        };
    }

    private static ApprovalRecord CreateApproval(ActionRequest action, AuthorizationManifest manifest,
        RSA key, DateTimeOffset now, string? overrideHash = null)
    {
        return new ApprovalRecord(
            action.ApprovalRef ?? "approval-probe",
            action.RunId,
            action.ActionId,
            overrideHash ?? Canonicalization.ActionHash(action),
            Canonicalization.AuthorizationHash(manifest),
            action.TargetRef,
            action.CapabilityRef,
            action.RiskClass,
            "operator",
            now.AddMinutes(5),
            "probe-rationale",
            "nonce-" + Guid.NewGuid().ToString("N"),
            "sig",
            "payload");
    }

    private static (CapabilityRegistry capabilities, AuthorizationTrustStore trustStore) CreateInfrastructure(RSA key)
    {
        var capabilities = new CapabilityRegistry();
        // AllowedTargetRefs must exactly match request.TargetRef (full URL), or use "*"
        capabilities.Register(new CapabilityManifest("fixture.inspect", RiskClass.R0,
            new[] { "*" },
            "unprivileged", true, Array.Empty<string>(), new[] { "synthetic" },
            TimeSpan.FromSeconds(10), 1024, false, true));
        capabilities.Register(new CapabilityManifest("fixture.state", RiskClass.R3,
            new[] { "*" },
            "unprivileged", false, Array.Empty<string>(), new[] { "synthetic" },
            TimeSpan.FromSeconds(10), 1024, true, true));
        capabilities.Freeze();

        var trustStore = new AuthorizationTrustStore();
        trustStore.Register("owner-1", key);
        trustStore.Register("operator-1", key);
        trustStore.Freeze();

        return (capabilities, trustStore);
    }

    private static PolicyEngine CreatePolicy(RSA key)
    {
        var (caps, trust) = CreateInfrastructure(key);
        return new PolicyEngine(caps, trust);
    }

    private static PolicyEngine CreatePolicyWithCapability(RSA key, string capabilityRef, RiskClass risk, bool requiresApproval = false)
    {
        var caps = new CapabilityRegistry();
        caps.Register(new CapabilityManifest(capabilityRef, risk,
            new[] { "*" },
            "unprivileged", requiresApproval, Array.Empty<string>(), new[] { "synthetic" },
            TimeSpan.FromSeconds(10), 1024, false, true));
        caps.Freeze();
        var trust = new AuthorizationTrustStore();
        trust.Register("owner-1", key);
        trust.Register("operator-1", key);
        trust.Freeze();
        return new PolicyEngine(caps, trust);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // ── SCOPE PROBES ──

    private static Task TestScopeCidrMatch()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://198.51.100.5:8080/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "198.51.100.5" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"CIDR match should allow, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeCidrMiss()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://10.0.0.5:8080/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "10.0.0.5" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"CIDR miss should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeWildcardSingleLevel()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://sub.fixture.local:8080/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "127.0.0.1" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"Single-level wildcard should allow, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeWildcardSingleLevelBlock()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow, wildcardPolicy: "single-level");
        var policy = CreatePolicy(key);
        var action = CreateAction("http://deep.sub.fixture.local:8080/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "127.0.0.1" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Multi-level under single-level should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeWildcardRecursive()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow, wildcardPolicy: "recursive");
        var policy = CreatePolicy(key);
        var action = CreateAction("http://deep.sub.fixture.local:8080/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "127.0.0.1" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"Recursive wildcard should allow deep subdomain, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeDenyList()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://blocked.fixture.local:8080/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "127.0.0.1" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Deny-list host should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeHardDenyMetadata()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://169.254.169.254/latest/meta-data/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "169.254.169.254" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Metadata endpoint should hard-deny, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeHardDenyLinkLocal()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://[fe80::1]:8080/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "fe80::1" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Link-local should hard-deny, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeAllowWithPort()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://127.0.0.1:9090/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "127.0.0.1" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"Allow entry with different port should still match host, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeEmptyTarget()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(target: "", RiskClass.R0);
        action = action with { ResolvedAddresses = Array.Empty<string>() };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Empty target should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeIpLiteralMatch()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://198.51.100.10:80/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "198.51.100.10" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"IP literal in CIDR range should allow, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeIPv6Loopback()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://[::1]:8080/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "::1" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"IPv6 loopback should allow in fixture mode, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestScopeBlockedFixtureHost()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://blocked.fixture.local/", RiskClass.R0);
        action = action with { ResolvedAddresses = new[] { "127.0.0.1" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Blocked fixture host should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    // ── METHOD POLICY PROBES ──

    private static Task TestMethodsAllowed()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://127.0.0.1:8080/", RiskClass.R0, capability: "fixture.inspect");
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"Allowed capability should pass, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestMethodsProhibited()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://127.0.0.1:8080/", RiskClass.R0, capability: "fixture.prohibited");
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Prohibited capability should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestMethodsUnknown()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://127.0.0.1:8080/", RiskClass.R0, capability: "nonexistent.tool");
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Unknown capability should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static async Task TestMethodsWildcardAllow()
    {
        using var key = CreateKey();
        var draft = CreateManifest(key, DateTimeOffset.UtcNow);
        draft = draft with { Methods = new MethodDefinition(new[] { "*" }, new string[0]) };
        draft = draft with { Authorization = AuthorizationSigner.Sign(draft, key) };
        var policy = CreatePolicy(key);
        var action = CreateAction("http://127.0.0.1:8080/", RiskClass.R0, capability: "any-tool");
        var result = policy.Evaluate(action, draft, null);
        Assert(result.Decision == PolicyDecision.Allow, $"Wildcard method should allow, got {result.Decision}");
        await Task.CompletedTask;
    }

    private static Task TestMethodsCaseInsensitive()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction("http://127.0.0.1:8080/", RiskClass.R0, capability: "FIXTURE.INSPECT");
        var result = policy.Evaluate(action, manifest, null);
        // Methods are matched case-insensitively via Contains with OrdinalIgnoreCase
        Assert(result.Decision == PolicyDecision.Allow, $"Case-insensitive match should pass, got {result.Decision}");
        return Task.CompletedTask;
    }

    // ── RISK CLASS PROBES ──

    private static Task TestRiskR0()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R0);
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"R0 should allow, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestRiskR1()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R1);
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"R1 should allow, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestRiskR2()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R2);
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"R2 should allow, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestRiskR3RequiresApproval()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R3, approvalRef: "approval-r3");
        var approval = CreateApproval(action, manifest, key, DateTimeOffset.UtcNow);
        var result = policy.Evaluate(action, manifest, approval);
        Assert(result.Decision == PolicyDecision.Allow, $"R3 with valid approval should allow, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestRiskR3NoApproval()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R3);
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"R3 without approval should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestRiskR4Blocked()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R4, approvalRef: "approval-r4");
        var approval = CreateApproval(action, manifest, key, DateTimeOffset.UtcNow);
        var result = policy.Evaluate(action, manifest, approval);
        Assert(result.Decision == PolicyDecision.Block, $"R4 should always block, got {result.Decision}");
        return Task.CompletedTask;
    }

    // ── APPROVAL PROBES ──

    private static Task TestApprovalValid()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R3, approvalRef: "approval-valid");
        var approval = CreateApproval(action, manifest, key, DateTimeOffset.UtcNow);
        var result = policy.Evaluate(action, manifest, approval);
        Assert(result.Decision == PolicyDecision.Allow, $"Valid approval should allow, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestApprovalExpired()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R3, approvalRef: "approval-expired");
        var approval = CreateApproval(action, manifest, key, DateTimeOffset.UtcNow)
            with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5) };
        var result = policy.Evaluate(action, manifest, approval);
        Assert(result.Decision == PolicyDecision.Block, $"Expired approval should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestApprovalWrongApprover()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R3, approvalRef: "approval-wrong");
        var approval = CreateApproval(action, manifest, key, DateTimeOffset.UtcNow)
            with { ApproverRef = "attacker" };
        var result = policy.Evaluate(action, manifest, approval);
        Assert(result.Decision == PolicyDecision.Block, $"Wrong approver should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestApprovalHashMismatch()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R3, approvalRef: "approval-tampered");
        var approval = CreateApproval(action, manifest, key, DateTimeOffset.UtcNow, overrideHash: "0000000000000000000000000000000000000000000000000000000000000000");
        var result = policy.Evaluate(action, manifest, approval);
        Assert(result.Decision == PolicyDecision.Block, $"Hash mismatch should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestApprovalNonceRequired()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(risk: RiskClass.R3, approvalRef: "approval-nonce");
        var approval = CreateApproval(action, manifest, key, DateTimeOffset.UtcNow)
            with { Nonce = string.Empty };
        var result = policy.Evaluate(action, manifest, approval);
        Assert(result.Decision == PolicyDecision.Block, $"Empty nonce should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    // ── ACTION REQUEST PROBES ──

    private static Task TestActionMissingType()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction() with { Type = "" };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Missing type should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestActionWrongType()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction() with { Type = "WRONG_TYPE" };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Wrong type should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestActionMissingRunId()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction() with { RunId = "" };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Missing run ID should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestActionEmptyMethodRefs()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction() with { MethodologyRefs = Array.Empty<string>() };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Empty method refs should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestActionNullArguments()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction() with { Arguments = null! };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Null arguments should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestAuthRefMismatch()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction(authRef: "WRONG-AUTH-REF");
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Auth ref mismatch should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    // ── MANIFEST PROBES ──

    private static Task TestManifestTamperedSignature()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        manifest = manifest with { Authorization = manifest.Authorization with { SignatureBase64 = "TAMPERED" } };
        var policy = CreatePolicy(key);
        var action = CreateAction();
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Tampered signature should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestManifestExpiredWindow()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow, windowOffset: TimeSpan.FromMinutes(-20));
        // Manifest starts at now-20m, expires at now-20m+10m = now-10m → expired
        manifest = manifest with { TimeWindow = manifest.TimeWindow with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5) } };
        manifest = manifest with { Authorization = AuthorizationSigner.Sign(manifest, key) };
        var policy = CreatePolicy(key);
        var action = CreateAction();
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Expired time window should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestManifestFutureStart()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow, windowOffset: TimeSpan.FromMinutes(30));
        // Starts 30 minutes from now → should block
        var policy = CreatePolicy(key);
        var action = CreateAction();
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Future start time should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestManifestExcludedWindow()
    {
        using var key = CreateKey();
        var now = DateTimeOffset.UtcNow;
        var excluded = new[] { new ExcludedWindow(now.AddMinutes(-1), now.AddMinutes(5), "maintenance") };
        var manifest = CreateManifest(key, now, excluded: excluded);
        var policy = CreatePolicy(key);
        var action = CreateAction();
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Action in excluded window should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestManifestUnregisteredKey()
    {
        using var key = CreateKey();
        using var unregisteredKey = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        // Re-sign with unregistered key
        manifest = manifest with { Authorization = AuthorizationSigner.Sign(manifest, unregisteredKey) };
        var policy = CreatePolicy(key);
        var action = CreateAction();
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Unregistered signing key should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    // ── REDIRECT PROBES ──

    private static Task TestRedirectSameOrigin()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow, redirectPolicy: "same-origin");
        var policy = CreatePolicy(key);
        var action = CreateAction() with { Arguments = new Dictionary<string, string> { ["mode"] = "safe", ["redirect_target"] = "http://127.0.0.1:8080/other" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Allow, $"Same-origin redirect should allow, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestRedirectCrossOriginBlocked()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow, redirectPolicy: "same-origin");
        var policy = CreatePolicy(key);
        var action = CreateAction() with { Arguments = new Dictionary<string, string> { ["mode"] = "safe", ["redirect_target"] = "http://evil.com/" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Cross-origin redirect should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestRedirectHardDenyHost()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow, redirectPolicy: "single-level");
        var policy = CreatePolicy(key);
        var action = CreateAction() with { Arguments = new Dictionary<string, string> { ["mode"] = "safe", ["redirect_target"] = "http://169.254.169.254/latest/" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Redirect to metadata should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    private static Task TestRedirectInvalidUri()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow, redirectPolicy: "same-origin");
        var policy = CreatePolicy(key);
        var action = CreateAction() with { Arguments = new Dictionary<string, string> { ["mode"] = "safe", ["redirect_target"] = "not-a-uri" } };
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.Decision == PolicyDecision.Block, $"Invalid redirect URI should block, got {result.Decision}");
        return Task.CompletedTask;
    }

    // ── BINDING PROBES ──

    private static Task TestBindingPolicyHash()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction();
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.ManifestHash == Canonicalization.AuthorizationHash(manifest),
            $"Policy manifest hash should match manifest, got {result.ManifestHash}");
        return Task.CompletedTask;
    }

    private static Task TestBindingActionHash()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction();
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.ActionHash == Canonicalization.ActionHash(action),
            $"Policy action hash should match action, got {result.ActionHash}");
        return Task.CompletedTask;
    }

    private static Task TestBindingAuthRef()
    {
        using var key = CreateKey();
        var manifest = CreateManifest(key, DateTimeOffset.UtcNow);
        var policy = CreatePolicy(key);
        var action = CreateAction();
        var result = policy.Evaluate(action, manifest, null);
        Assert(result.AuthorizationRef == "auth-artifact-1",
            $"Authorization ref should be bound, got {result.AuthorizationRef}");
        return Task.CompletedTask;
    }
}
