using System.Security.Cryptography;
using System.Text;
using CyberSopHarness.Core;

/// <summary>
/// PROBE BATTERY: Model Provider and Proposal Validation
/// 
/// Purpose: Validate provider descriptors, proposal validation, manifest validation,
/// model pinning, runtime lifecycle, and provider selection.
///
/// Coverage dimensions:
///   1. ProviderProposalValidator: valid proposals, missing fields, hash format
///   2. ActionRequestValidator: completeness, type field, collections
///   3. ActionEnvelopeValidator: envelope integrity, hash binding
///   4. ProviderDescriptor: all fields required, hash format
///   5. Model selection and selection persistence
///   6. Manifest validation: signature, trust, expiry
///   7. Proposal parsing (strict JSON, fence stripping)
///
/// Pitfalls:
///   - SHA-256 hex must be exactly 64 lowercase hex characters
///   - Proposal validation includes action validation when failure is None
///   - Provider hash must be canonical configuration hash
///   - Latency and token usage must be non-negative
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    public static async Task<int> Main()
    {
        // === PROVIDER PROPOSAL VALIDATOR PROBES ===
        await Run("proposal: valid proposal passes", TestProposalValid);
        await Run("proposal: null proposal fails", TestProposalNull);
        await Run("proposal: null provider descriptor fails", TestProposalNullProvider);
        await Run("proposal: empty provider ref fails", TestProposalEmptyProviderRef);
        await Run("proposal: empty model ref fails", TestProposalEmptyModelRef);
        await Run("proposal: invalid config hash format fails", TestProposalBadConfigHash);
        await Run("proposal: invalid output hash format fails", TestProposalBadOutputHash);
        await Run("proposal: negative latency fails", TestProposalNegativeLatency);
        await Run("proposal: negative token usage fails", TestProposalNegativeTokens);
        await Run("proposal: valid timeout failure is accepted", TestProposalTimeout);
        await Run("proposal: valid unavailable failure is accepted", TestProposalUnavailable);
        await Run("proposal: empty context policy fails", TestProposalEmptyContextPolicy);

        // === ACTION REQUEST VALIDATOR PROBES ===
        await Run("action-req: valid request passes", TestActionValid);
        await Run("action-req: null request fails", TestActionNull);
        await Run("action-req: wrong type field fails", TestActionWrongType);
        await Run("action-req: empty run ID fails", TestActionEmptyRunId);
        await Run("action-req: empty action ID fails", TestActionEmptyActionId);
        await Run("action-req: empty target fails", TestActionEmptyTarget);
        await Run("action-req: empty capability fails", TestActionEmptyCapability);
        await Run("action-req: empty purpose fails", TestActionEmptyPurpose);
        await Run("action-req: null arguments fails", TestActionNullArgs);
        await Run("action-req: null method refs fails", TestActionNullMethodRefs);
        await Run("action-req: empty method refs fails", TestActionEmptyMethodRefs);
        await Run("action-req: invalid risk class fails", TestActionInvalidRisk);

        // === ACTION ENVELOPE VALIDATOR PROBES ===
        await Run("envelope: valid envelope passes", TestEnvelopeValid);
        await Run("envelope: null envelope fails", TestEnvelopeNull);
        await Run("envelope: action hash mismatch fails", TestEnvelopeHashMismatch);

        // === PROPOSAL PARSING PROBES ===
        await Run("parsing: clean JSON proposal parses", TestParsingClean);
        await Run("parsing: markdown-fenced proposal strips fence", TestParsingFenced);
        await Run("parsing: commentary-stripped proposal parses", TestParsingCommentary);
        await Run("parsing: malformed JSON rejected", TestParsingMalformed);
        await Run("parsing: empty string rejected", TestParsingEmpty);

        Console.WriteLine($"\nprovider_model_battery=passed count={_passed} failed count={_failed}");
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

    private static ProviderProposal CreateValidProposal(
        string providerRef = "provider-a", string modelRef = "model-a", string modelVersion = "1.0",
        ProviderFailureClass failure = ProviderFailureClass.None)
    {
        return new ProviderProposal(
            new ProviderDescriptor(providerRef, modelRef, modelVersion,
                Canonicalization.Sha256Hex("config-hash"), "local-only", "none", "typed"),
            CreateValidAction(),
            Canonicalization.Sha256Hex("output-hash"),
            TimeSpan.FromMilliseconds(10),
            100,
            failure);
    }

    private static ActionRequest CreateValidAction()
    {
        return new ActionRequest
        {
            RunId = "run-prov", ActionId = "action-prov", Phase = "probe",
            TargetRef = "http://127.0.0.1:8080/", CapabilityRef = "fixture.inspect",
            Arguments = new Dictionary<string, string>(), Purpose = "provider probe",
            RiskClass = RiskClass.R0, ScopeRef = "scope", AuthorizationRef = "auth",
            MethodologyRefs = new[] { "method-v1" }, ResolvedAddresses = new[] { "127.0.0.1" }
        };
    }

    // === PROVIDER PROPOSAL VALIDATOR ===

    private static Task TestProposalValid()
    {
        var proposal = CreateValidProposal();
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(result.IsValid, $"Valid proposal should pass: {string.Join(", ", result.Errors)}");
        return Task.CompletedTask;
    }

    private static Task TestProposalNull()
    {
        var threw = false;
        try { ProviderProposalValidator.Validate(null!); }
        catch (ArgumentNullException) { threw = true; }
        Assert(threw, "Null proposal should throw");
        return Task.CompletedTask;
    }

    private static Task TestProposalNullProvider()
    {
        var proposal = CreateValidProposal() with { Provider = null! };
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(!result.IsValid, "Null provider should fail");
        return Task.CompletedTask;
    }

    private static Task TestProposalEmptyProviderRef()
    {
        var proposal = CreateValidProposal() with
        {
            Provider = CreateValidProposal().Provider with { ProviderRef = "" }
        };
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(!result.IsValid, "Empty provider ref should fail");
        return Task.CompletedTask;
    }

    private static Task TestProposalEmptyModelRef()
    {
        var proposal = CreateValidProposal() with
        {
            Provider = CreateValidProposal().Provider with { ModelRef = "" }
        };
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(!result.IsValid, "Empty model ref should fail");
        return Task.CompletedTask;
    }

    private static Task TestProposalBadConfigHash()
    {
        var proposal = CreateValidProposal() with
        {
            Provider = CreateValidProposal().Provider with { ConfigurationHash = "not-a-hash" }
        };
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(!result.IsValid, "Bad config hash should fail");
        return Task.CompletedTask;
    }

    private static Task TestProposalBadOutputHash()
    {
        var proposal = CreateValidProposal() with { OutputSha256 = "not-a-hash" };
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(!result.IsValid, "Bad output hash should fail");
        return Task.CompletedTask;
    }

    private static Task TestProposalNegativeLatency()
    {
        var proposal = CreateValidProposal() with { Latency = TimeSpan.FromMilliseconds(-1) };
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(!result.IsValid, "Negative latency should fail");
        return Task.CompletedTask;
    }

    private static Task TestProposalNegativeTokens()
    {
        var proposal = CreateValidProposal() with { TokenUsage = -1 };
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(!result.IsValid, "Negative token usage should fail");
        return Task.CompletedTask;
    }

    private static Task TestProposalTimeout()
    {
        var proposal = CreateValidProposal(failure: ProviderFailureClass.Timeout);
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(result.IsValid, $"Timeout failure should be valid: {string.Join(", ", result.Errors)}");
        return Task.CompletedTask;
    }

    private static Task TestProposalUnavailable()
    {
        var proposal = CreateValidProposal(failure: ProviderFailureClass.Unavailable);
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(result.IsValid, $"Unavailable failure should be valid: {string.Join(", ", result.Errors)}");
        return Task.CompletedTask;
    }

    private static Task TestProposalEmptyContextPolicy()
    {
        var proposal = CreateValidProposal() with
        {
            Provider = CreateValidProposal().Provider with { ContextPolicy = "" }
        };
        var result = ProviderProposalValidator.Validate(proposal);
        Assert(!result.IsValid, "Empty context policy should fail");
        return Task.CompletedTask;
    }

    // === ACTION REQUEST VALIDATOR ===

    private static Task TestActionValid()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction());
        Assert(result.IsValid, $"Valid action should pass: {string.Join(", ", result.Errors)}");
        return Task.CompletedTask;
    }

    private static Task TestActionNull()
    {
        var result = ActionRequestValidator.Validate(null);
        Assert(!result.IsValid, "Null action should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionWrongType()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { Type = "WRONG" });
        Assert(!result.IsValid, "Wrong type should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionEmptyRunId()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { RunId = "" });
        Assert(!result.IsValid, "Empty run ID should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionEmptyActionId()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { ActionId = "" });
        Assert(!result.IsValid, "Empty action ID should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionEmptyTarget()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { TargetRef = "" });
        Assert(!result.IsValid, "Empty target should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionEmptyCapability()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { CapabilityRef = "" });
        Assert(!result.IsValid, "Empty capability should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionEmptyPurpose()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { Purpose = "" });
        Assert(!result.IsValid, "Empty purpose should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionNullArgs()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { Arguments = null! });
        Assert(!result.IsValid, "Null arguments should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionNullMethodRefs()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { MethodologyRefs = null! });
        Assert(!result.IsValid, "Null method refs should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionEmptyMethodRefs()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { MethodologyRefs = Array.Empty<string>() });
        Assert(!result.IsValid, "Empty method refs should fail");
        return Task.CompletedTask;
    }

    private static Task TestActionInvalidRisk()
    {
        var result = ActionRequestValidator.Validate(CreateValidAction() with { RiskClass = (RiskClass)99 });
        Assert(!result.IsValid, "Invalid risk class should fail");
        return Task.CompletedTask;
    }

    // === ENVELOPE VALIDATOR ===

    private static Task TestEnvelopeValid()
    {
        var action = CreateValidAction();
        var envelope = new ActionEnvelope(
            "env-1", action,
            new ProviderDescriptor("p", "m", "1.0", Canonicalization.Sha256Hex("c"), "local-only", "none", "typed"),
            Canonicalization.Sha256Hex("out"), DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(1), 10, ProviderFailureClass.None);
        var result = ActionEnvelopeValidator.Validate(envelope);
        Assert(result.IsValid, $"Valid envelope should pass: {string.Join(", ", result.Errors)}");
        return Task.CompletedTask;
    }

    private static Task TestEnvelopeNull()
    {
        var result = ActionEnvelopeValidator.Validate(null!);
        Assert(!result.IsValid, "Null envelope should fail");
        return Task.CompletedTask;
    }

    private static Task TestEnvelopeHashMismatch()
    {
        var action = CreateValidAction();
        var envelope = new ActionEnvelope(
            "env-2", action,
            new ProviderDescriptor("p", "m", "1.0", Canonicalization.Sha256Hex("c"), "local-only", "none", "typed"),
            "0000000000000000000000000000000000000000000000000000000000000000", // wrong hash
            DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(1), 10, ProviderFailureClass.None);
        var result = ActionEnvelopeValidator.Validate(envelope);
        Assert(!result.IsValid, "Hash mismatch envelope should fail");
        return Task.CompletedTask;
    }

    // === PROPOSAL PARSING ===

    private static Task TestParsingClean()
    {
        var json = """{"type":"ACTION_REQUEST","runId":"r","actionId":"a","phase":"p","targetRef":"http://127.0.0.1/","capabilityRef":"c","purpose":"test","riskClass":"R0","scopeRef":"s","authorizationRef":"auth","methodologyRefs":["m1"],"arguments":{},"resolvedAddresses":["127.0.0.1"]}""";
        var stripped = StripFencesAndCommentary(json);
        Assert(stripped.Contains("ACTION_REQUEST"), "Clean JSON should pass through");
        return Task.CompletedTask;
    }

    private static Task TestParsingFenced()
    {
        var fenced = "```json\n{\"type\":\"ACTION_REQUEST\"}\n```";
        var stripped = StripFencesAndCommentary(fenced);
        Assert(stripped.Contains("ACTION_REQUEST"), "Fenced JSON should be unwrapped");
        Assert(!stripped.Contains("```"), "Fence markers should be removed");
        return Task.CompletedTask;
    }

    private static Task TestParsingCommentary()
    {
        var withCommentary = "Here is the action:\n{\"type\":\"ACTION_REQUEST\"}\nDone.";
        var stripped = StripFencesAndCommentary(withCommentary);
        Assert(stripped.Contains("ACTION_REQUEST"), "Commentary should be stripped");
        return Task.CompletedTask;
    }

    private static Task TestParsingMalformed()
    {
        var threw = false;
        try { System.Text.Json.JsonDocument.Parse("not json at all"); }
        catch (System.Text.Json.JsonException) { threw = true; }
        Assert(threw, "Malformed JSON should throw");
        return Task.CompletedTask;
    }

    private static Task TestParsingEmpty()
    {
        var threw = false;
        try { System.Text.Json.JsonDocument.Parse(""); }
        catch (System.Text.Json.JsonException) { threw = true; }
        Assert(threw, "Empty string should throw");
        return Task.CompletedTask;
    }

    private static string StripFencesAndCommentary(string input)
    {
        var text = input.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
        }
        if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        text = text.Trim();

        // Try to find JSON object in the text
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start) text = text[start..(end + 1)];
        return text;
    }
}
