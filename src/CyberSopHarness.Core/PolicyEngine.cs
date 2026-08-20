namespace CyberSopHarness.Core;

public sealed class PolicyEngine
{
    private readonly string _policyRef;
    private readonly string _policyVersion;
    private readonly CapabilityRegistry _capabilities;
    private readonly AuthorizationTrustStore _trustStore;

    public PolicyEngine(CapabilityRegistry capabilities, AuthorizationTrustStore trustStore, string policyRef = "policy-phase2", string policyVersion = "1.0")
    {
        if (!capabilities.IsFrozen) throw new InvalidOperationException("capability registry must be frozen before policy engine construction");
        if (!trustStore.IsFrozen) throw new InvalidOperationException("authorization trust store must be frozen before policy engine construction");
        _capabilities = capabilities;
        _trustStore = trustStore;
        _policyRef = policyRef;
        _policyVersion = policyVersion;
    }

    public PolicyResult Evaluate(ActionRequest request, AuthorizationManifest manifest, ApprovalRecord? approval)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(manifest);
        var now = AuthoritativeClock.UtcNow;
        var actionHash = Canonicalization.ActionHash(request);
        var manifestHash = Canonicalization.AuthorizationHash(manifest);
        var scopeHash = Canonicalization.ScopeHash(manifest.Scope);
        if (request.Type != "ACTION_REQUEST" || string.IsNullOrWhiteSpace(request.RunId) || string.IsNullOrWhiteSpace(request.ActionId) || string.IsNullOrWhiteSpace(request.Phase) || string.IsNullOrWhiteSpace(request.TargetRef) || string.IsNullOrWhiteSpace(request.CapabilityRef) || string.IsNullOrWhiteSpace(request.Purpose) || string.IsNullOrWhiteSpace(request.ScopeRef) || string.IsNullOrWhiteSpace(request.AuthorizationRef) || request.Arguments is null || request.MethodologyRefs is null || request.ResolvedAddresses is null || request.MethodologyRefs.Count == 0 || request.MethodologyRefs.Any(string.IsNullOrWhiteSpace)) return Result(PolicyDecision.Block, "action request is incomplete", manifest, request, actionHash, manifestHash, scopeHash);
        if (!string.Equals(request.AuthorizationRef, manifest.Authorization.ArtifactRef, StringComparison.Ordinal)) return Result(PolicyDecision.Block, "action authorization reference does not match manifest", manifest, request, actionHash, manifestHash, scopeHash);
        if (!Enum.IsDefined(request.RiskClass)) return Result(PolicyDecision.Block, "action risk is invalid", manifest, request, actionHash, manifestHash, scopeHash);
        var validation = ManifestValidation.Validate(manifest, _trustStore);
        if (!validation.IsValid) return Result(PolicyDecision.Block, string.Join("; ", validation.Errors), manifest, request, actionHash, manifestHash, scopeHash);
        var capabilityValidation = _capabilities.Validate(request, manifest);
        if (!capabilityValidation.IsValid) return Result(PolicyDecision.Block, string.Join("; ", capabilityValidation.Errors), manifest, request, actionHash, manifestHash, scopeHash);
        var scope = new ScopeEvaluator(manifest).Evaluate(request.TargetRef, request.ResolvedAddresses);
        if (!scope.Allowed) return Result(PolicyDecision.Block, scope.Reason, manifest, request, actionHash, manifestHash, scopeHash);
        if (request.Arguments.TryGetValue("redirect_target", out var redirectTarget) && !new ScopeEvaluator(manifest).EvaluateRedirect(request.TargetRef, redirectTarget).Allowed) return Result(PolicyDecision.Block, "redirect target is not allowed", manifest, request, actionHash, manifestHash, scopeHash);
        if (!manifest.Methods.Allowed.Contains(request.CapabilityRef, StringComparer.OrdinalIgnoreCase) && !manifest.Methods.Allowed.Contains("*", StringComparer.Ordinal)) return Result(PolicyDecision.Block, "capability is not allowed by methods policy", manifest, request, actionHash, manifestHash, scopeHash);
        if (manifest.Methods.Prohibited.Contains(request.CapabilityRef, StringComparer.OrdinalIgnoreCase)) return Result(PolicyDecision.Block, "capability is prohibited by methods policy", manifest, request, actionHash, manifestHash, scopeHash);
        if (request.RiskClass == RiskClass.R4) return Result(PolicyDecision.Block, "R4 actions are denied by Phase 2 policy", manifest, request, actionHash, manifestHash, scopeHash);
        _capabilities.TryGet(request.CapabilityRef, out var capability);
        if (request.RiskClass == RiskClass.R3 || capability?.RequiresApproval == true)
        {
            if (approval is null || string.IsNullOrWhiteSpace(request.ApprovalRef) || approval.RunId != request.RunId || approval.ActionId != request.ActionId || approval.ActionHash != actionHash || approval.ManifestHash != manifestHash || approval.TargetRef != request.TargetRef || approval.CapabilityRef != request.CapabilityRef || approval.RiskClass != request.RiskClass || approval.ApproverRef != manifest.Authorization.Operator || string.IsNullOrWhiteSpace(approval.Nonce) || approval.ExpiresAt <= now || approval.ApprovalRef != request.ApprovalRef || !ApprovalVerifier.Verify(approval, _trustStore)) return Result(PolicyDecision.ApprovalRequired, "action requires a valid signed action-bound approval", manifest, request, actionHash, manifestHash, scopeHash);
        }
        return Result(PolicyDecision.Allow, "action is authorized by current policy", manifest, request, actionHash, manifestHash, scopeHash);
    }

    private PolicyResult Result(PolicyDecision decision, string reason, AuthorizationManifest manifest, ActionRequest request, string actionHash, string manifestHash, string scopeHash) => new(decision, _policyRef, _policyVersion, reason, request.ScopeRef, actionHash, manifestHash, scopeHash, request.AuthorizationRef, request.CapabilityRef, request.RiskClass, request.MethodologyRefs ?? Array.Empty<string>());
}
