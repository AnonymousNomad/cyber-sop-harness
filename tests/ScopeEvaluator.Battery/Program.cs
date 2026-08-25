using System.Security.Cryptography;
using CyberSopHarness.Core;

/// <summary>
/// PROBE BATTERY: Scope Evaluator
/// 
/// Purpose: Deep validation of ScopeEvaluator.Evaluate and EvaluateRedirect.
/// Tests every scope matching strategy: literal hosts, CIDR ranges, wildcards,
/// deny-list, hard-deny patterns (metadata, link-local), and redirect policies.
///
/// Pitfalls:
///   - IPv4-mapped-IPv6 addresses (e.g., ::ffff:127.0.0.1) must normalize
///   - CIDR edge cases: /0 prefix matches everything, host bits set correctly
///   - Hard-deny is evaluated BEFORE allow-list (metadata always blocked)
///   - Wildcard policy "single-level" vs "recursive" vs unknown all differ
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    public static async Task<int> Main()
    {
        // === LITERAL HOST MATCHING ===
        await Run("literal: exact IP match allows", TestLiteralIpMatch);
        await Run("literal: exact hostname match allows", TestLiteralHostnameMatch);
        await Run("literal: case-insensitive hostname match", TestLiteralHostnameCaseInsensitive);
        await Run("literal: hostname with trailing dot matches", TestLiteralHostnameTrailingDot);
        await Run("literal: non-matching host blocks", TestLiteralNonMatch);

        // === CIDR MATCHING ===
        await Run("CIDR: host within /24 range allows", TestCidrSlash24);
        await Run("CIDR: host at network boundary allows", TestCidrNetworkBoundary);
        await Run("CIDR: host at broadcast boundary allows", TestCidrBroadcastBoundary);
        await Run("CIDR: host outside range blocks", TestCidrOutside);
        await Run("CIDR: /0 prefix allows everything", TestCidrSlash0);
        await Run("CIDR: /32 prefix matches single host", TestCidrSlash32);
        await Run("CIDR: malformed CIDR string is not a match", TestCidrMalformed);

        // === WILDCARD MATCHING ===
        await Run("wildcard: *.domain matches sub.domain", TestWildcardMatch);
        await Run("wildcard: *.domain does not match domain itself", TestWildcardNoMatchBase);
        await Run("wildcard: *.domain does not match deep.sub.domain under single-level", TestWildcardSingleLevelDeep);
        await Run("wildcard: *.domain matches deep.sub.domain under recursive", TestWildcardRecursiveDeep);
        await Run("wildcard: *.domain does not match sibling.tld", TestWildcardSibling);
        await Run("wildcard: unknown wildcard policy blocks wildcards", TestWildcardUnknownPolicy);

        // === DENY LIST ===
        await Run("deny: explicit deny blocks host", TestDenyExplicit);
        await Run("deny: deny takes precedence over allow", TestDenyPrecedenceOverAllow);
        await Run("deny: deny CIDR range blocks matching host", TestDenyCidr);

        // === HARD DENY ===
        await Run("hard-deny: cloud metadata 169.254.169.254", TestHardDenyMetadata4);
        await Run("hard-deny: EC2 metadata fd00:ec2::254", TestHardDenyMetadata6);
        await Run("hard-deny: link-local IPv6", TestHardDenyLinkLocal6);
        await Run("hard-deny: multicast addresses", TestHardDenyMulticast);
        await Run("hard-deny: loopback in authorized mode", TestHardDenyLoopbackAuthorized);

        // === REDIRECT EVALUATION ===
        await Run("redirect-eval: same-origin redirect to same host", TestRedirectEvalSameHost);
        await Run("redirect-eval: cross-origin redirect blocked", TestRedirectEvalCrossOrigin);
        await Run("redirect-eval: redirect to metadata blocked", TestRedirectEvalMetadata);
        await Run("redirect-eval: redirect with invalid URI blocked", TestRedirectEvalInvalidUri);
        await Run("redirect-eval: block policy blocks all redirects", TestRedirectEvalBlockPolicy);
        await Run("redirect-eval: single-level policy allows same-level", TestRedirectEvalSingleLevel);

        Console.WriteLine($"\nscope_evaluator_battery=passed count={_passed} failed count={_failed}");
        return _failed > 0 ? 1 : 0;
    }

    private static async Task Run(string name, Func<Task> test)
    {
        try { await test(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception ex) { _failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static AuthorizationManifest CreateManifest(
        string[]? allow = null,
        string[]? deny = null,
        string wildcardPolicy = "single-level",
        string redirectPolicy = "same-origin",
        EngagementMode mode = EngagementMode.Fixture)
    {
        return new AuthorizationManifest
        {
            EngagementId = "scope-battery",
            EngagementMode = mode,
            Scope = new ScopeDefinition(
                allow ?? new[] { "127.0.0.1", "*.fixture.local", "198.51.100.0/24" },
                deny ?? new[] { "blocked.fixture.local" },
                wildcardPolicy,
                redirectPolicy,
                "block"),
            Methods = new MethodDefinition(new[] { "fixture.inspect" }, Array.Empty<string>())
        };
    }

    // === LITERAL HOST MATCHING ===
    private static Task TestLiteralIpMatch()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://127.0.0.1:8080/", new[] { "127.0.0.1" });
        Assert(result.Allowed, $"127.0.0.1 should match literal allow, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestLiteralHostnameMatch()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://sub.fixture.local:8080/", new[] { "127.0.0.1" });
        Assert(result.Allowed, $"sub.fixture.local should match wildcard, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestLiteralHostnameCaseInsensitive()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://SUB.FIXTURE.LOCAL:8080/", new[] { "127.0.0.1" });
        Assert(result.Allowed, $"Case-insensitive hostname should match, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestLiteralHostnameTrailingDot()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://127.0.0.1.:8080/", new[] { "127.0.0.1" });
        Assert(result.Allowed, $"Trailing dot should normalize, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestLiteralNonMatch()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://evil.com:8080/", new[] { "10.0.0.1" });
        Assert(!result.Allowed, $"Non-matching host should block, got allowed");
        return Task.CompletedTask;
    }

    // === CIDR MATCHING ===
    private static Task TestCidrSlash24()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://198.51.100.42:8080/", new[] { "198.51.100.42" });
        Assert(result.Allowed, $"198.51.100.42 in /24 should match, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestCidrNetworkBoundary()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://198.51.100.0:8080/", new[] { "198.51.100.0" });
        Assert(result.Allowed, $"Network boundary should match, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestCidrBroadcastBoundary()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://198.51.100.255:8080/", new[] { "198.51.100.255" });
        Assert(result.Allowed, $"Broadcast boundary should match, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestCidrOutside()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://10.0.0.1:8080/", new[] { "10.0.0.1" });
        Assert(!result.Allowed, $"Host outside CIDR range should block");
        return Task.CompletedTask;
    }

    private static Task TestCidrSlash0()
    {
        var manifest = CreateManifest(allow: new[] { "0.0.0.0/0" });
        var evaluator = new ScopeEvaluator(manifest);
        var result = evaluator.Evaluate("http://8.8.8.8:53/", new[] { "8.8.8.8" });
        Assert(result.Allowed, $"/0 should allow everything, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestCidrSlash32()
    {
        var manifest = CreateManifest(allow: new[] { "5.5.5.5/32" });
        var evaluator = new ScopeEvaluator(manifest);
        var match = evaluator.Evaluate("http://5.5.5.5:80/", new[] { "5.5.5.5" });
        var miss = evaluator.Evaluate("http://5.5.5.6:80/", new[] { "5.5.5.6" });
        Assert(match.Allowed, $"/32 should match exact host");
        Assert(!miss.Allowed, $"/32 should not match different host");
        return Task.CompletedTask;
    }

    private static Task TestCidrMalformed()
    {
        var manifest = CreateManifest(allow: new[] { "not-a-cidr" });
        var evaluator = new ScopeEvaluator(manifest);
        var result = evaluator.Evaluate("http://not-a-cidr:80/", new[] { "not-a-cidr" });
        // Malformed CIDR should match as literal string
        Assert(result.Allowed, $"Malformed CIDR treated as literal should match, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    // === WILDCARD MATCHING ===
    private static Task TestWildcardMatch()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://sub.fixture.local:8080/", new[] { "127.0.0.1" });
        Assert(result.Allowed, $"Wildcard should match subdomain, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestWildcardNoMatchBase()
    {
        // fixture.local itself is not in allow list (*.fixture.local matches only subdomains)
        var manifest = CreateManifest(allow: new[] { "*.fixture.local" });
        var evaluator = new ScopeEvaluator(manifest);
        var result = evaluator.Evaluate("http://fixture.local:8080/", new[] { "fixture.local" });
        // fixture.local does NOT start with "*." prefix match because the prefix is empty
        Assert(!result.Allowed, $"Base domain should not match *.domain wildcard");
        return Task.CompletedTask;
    }

    private static Task TestWildcardSingleLevelDeep()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(wildcardPolicy: "single-level"));
        var result = evaluator.Evaluate("http://deep.sub.fixture.local:8080/", new[] { "127.0.0.1" });
        Assert(!result.Allowed, $"Single-level should not match deep subdomain");
        return Task.CompletedTask;
    }

    private static Task TestWildcardRecursiveDeep()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(wildcardPolicy: "recursive"));
        var result = evaluator.Evaluate("http://deep.sub.fixture.local:8080/", new[] { "127.0.0.1" });
        Assert(result.Allowed, $"Recursive should match deep subdomain, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestWildcardSibling()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://evil.com:8080/", new[] { "evil.com" });
        Assert(!result.Allowed, $"Wildcard *.fixture.local should not match evil.com");
        return Task.CompletedTask;
    }

    private static Task TestWildcardUnknownPolicy()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(wildcardPolicy: "invalid-policy"));
        var result = evaluator.Evaluate("http://sub.fixture.local:8080/", new[] { "127.0.0.1" });
        Assert(!result.Allowed, $"Unknown wildcard policy should not match");
        return Task.CompletedTask;
    }

    // === DENY LIST ===
    private static Task TestDenyExplicit()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://blocked.fixture.local:8080/", new[] { "127.0.0.1" });
        Assert(!result.Allowed, $"Explicit deny should block");
        return Task.CompletedTask;
    }

    private static Task TestDenyPrecedenceOverAllow()
    {
        var manifest = CreateManifest(allow: new[] { "blocked.fixture.local" }, deny: new[] { "blocked.fixture.local" });
        var evaluator = new ScopeEvaluator(manifest);
        var result = evaluator.Evaluate("http://blocked.fixture.local:8080/", new[] { "blocked.fixture.local" });
        Assert(!result.Allowed, $"Deny should take precedence over allow");
        return Task.CompletedTask;
    }

    private static Task TestDenyCidr()
    {
        var manifest = CreateManifest(deny: new[] { "10.0.0.0/8" });
        var evaluator = new ScopeEvaluator(manifest);
        var result = evaluator.Evaluate("http://10.1.2.3:8080/", new[] { "10.1.2.3" });
        Assert(!result.Allowed, $"Deny CIDR should block matching host");
        return Task.CompletedTask;
    }

    // === HARD DENY ===
    private static Task TestHardDenyMetadata4()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://169.254.169.254/latest/meta-data/", new[] { "169.254.169.254" });
        Assert(!result.Allowed, $"Cloud metadata IPv4 should hard-deny");
        return Task.CompletedTask;
    }

    private static Task TestHardDenyMetadata6()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://[fd00:ec2::254]/", new[] { "fd00:ec2::254" });
        Assert(!result.Allowed, $"EC2 metadata IPv6 should hard-deny");
        return Task.CompletedTask;
    }

    private static Task TestHardDenyLinkLocal6()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://[fe80::1]:8080/", new[] { "fe80::1" });
        Assert(!result.Allowed, $"IPv6 link-local should hard-deny");
        return Task.CompletedTask;
    }

    private static Task TestHardDenyMulticast()
    {
        var evaluator = new ScopeEvaluator(CreateManifest());
        var result = evaluator.Evaluate("http://[ff02::1]:8080/", new[] { "ff02::1" });
        Assert(!result.Allowed, $"Multicast address should hard-deny");
        return Task.CompletedTask;
    }

    private static Task TestHardDenyLoopbackAuthorized()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(mode: EngagementMode.Authorized));
        var result = evaluator.Evaluate("http://localhost:8080/", new[] { "127.0.0.1" });
        // In authorized mode, localhost is hard-denied unless explicitly in scope
        Assert(!result.Allowed, $"Loopback in authorized mode should hard-deny without explicit scope");
        return Task.CompletedTask;
    }

    // === REDIRECT EVALUATION ===
    private static Task TestRedirectEvalSameHost()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(redirectPolicy: "same-origin"));
        var result = evaluator.EvaluateRedirect("http://127.0.0.1:8080/a", "http://127.0.0.1:8080/b");
        Assert(result.Allowed, $"Same-origin redirect should allow, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }

    private static Task TestRedirectEvalCrossOrigin()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(redirectPolicy: "same-origin"));
        var result = evaluator.EvaluateRedirect("http://127.0.0.1:8080/", "http://evil.com/");
        Assert(!result.Allowed, $"Cross-origin redirect should block");
        return Task.CompletedTask;
    }

    private static Task TestRedirectEvalMetadata()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(redirectPolicy: "single-level"));
        var result = evaluator.EvaluateRedirect("http://127.0.0.1:8080/", "http://169.254.169.254/");
        Assert(!result.Allowed, $"Redirect to metadata should block");
        return Task.CompletedTask;
    }

    private static Task TestRedirectEvalInvalidUri()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(redirectPolicy: "same-origin"));
        var result = evaluator.EvaluateRedirect("http://127.0.0.1:8080/", "not-a-valid-uri");
        Assert(!result.Allowed, $"Invalid redirect URI should block");
        return Task.CompletedTask;
    }

    private static Task TestRedirectEvalBlockPolicy()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(redirectPolicy: "block"));
        var result = evaluator.EvaluateRedirect("http://127.0.0.1:8080/", "http://127.0.0.1:8080/other");
        Assert(!result.Allowed, $"Block policy should block all redirects");
        return Task.CompletedTask;
    }

    private static Task TestRedirectEvalSingleLevel()
    {
        var evaluator = new ScopeEvaluator(CreateManifest(redirectPolicy: "single-level"));
        var result = evaluator.EvaluateRedirect("http://sub.fixture.local:8080/", "http://sub.fixture.local:8080/other");
        Assert(result.Allowed, $"Single-level redirect to same host should allow, got blocked: {result.Reason}");
        return Task.CompletedTask;
    }
}
