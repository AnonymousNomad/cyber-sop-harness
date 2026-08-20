using System.Security.Cryptography;
using System.Text;

namespace CyberSopHarness.Core;

public sealed class PermitIssuer : IDisposable
{
    private readonly RSA _issuerKey;
    private readonly PolicyEngine _policyEngine;
    private readonly object _gate = new();
    private readonly Dictionary<string, Permit> _permits = new(StringComparer.Ordinal);
    private readonly HashSet<string> _consumedApprovals = new(StringComparer.Ordinal);
    private readonly HashSet<string> _claimedExecutions = new(StringComparer.Ordinal);
    private bool _relayLost;

    public PermitIssuer(PolicyEngine policyEngine, RSA? issuerKey = null)
    {
        _policyEngine = policyEngine;
        _issuerKey = issuerKey ?? RSA.Create(2048);
    }

    public Permit Issue(ActionRequest request, AuthorizationManifest manifest, string workerRef, ApprovalRecord? approval = null, TimeSpan? lifetime = null)
    {
        var now = AuthoritativeClock.UtcNow;
        lock (_gate)
        {
            if (_relayLost) throw new InvalidOperationException("permit issuer is stopped after relay loss; fresh authorization requires a new issuer");
        }
        var policy = _policyEngine.Evaluate(request, manifest, approval);
        if (policy.Decision != PolicyDecision.Allow) throw new InvalidOperationException("cannot issue a permit: " + policy.Reason);
        var actionHash = Canonicalization.ActionHash(request);
        if (actionHash != policy.ActionHash || policy.ManifestHash != Canonicalization.AuthorizationHash(manifest) || policy.ScopeHash != Canonicalization.ScopeHash(manifest.Scope)) throw new InvalidOperationException("policy result does not bind to the current request and manifest");
        var requestedLifetime = lifetime ?? TimeSpan.FromMinutes(5);
        var approvalExpiry = approval?.ExpiresAt ?? manifest.TimeWindow.ExpiresAt;
        var expiresAt = new[] { now.Add(requestedLifetime), manifest.TimeWindow.ExpiresAt, approvalExpiry }.Min();
        if (expiresAt <= now) throw new InvalidOperationException("permit lifetime is not positive within the authorization window");
        var permit = new Permit
        {
            PermitId = "permit_" + Guid.NewGuid().ToString("N"),
            RunId = request.RunId,
            ActionId = request.ActionId,
            ActionHash = actionHash,
            ManifestHash = policy.ManifestHash,
            CanonicalizationRef = "canonical-action-json-v1",
            TargetRef = request.TargetRef,
            ScopeRef = request.ScopeRef,
            ScopeHash = policy.ScopeHash,
            PolicyRef = policy.PolicyRef,
            PolicyVersion = policy.PolicyVersion,
            WorkerRef = workerRef,
            CapabilityRef = request.CapabilityRef,
            AuthorizationRef = request.AuthorizationRef,
            CredentialRef = request.CredentialRef,
            ApprovalRef = approval?.ApprovalRef ?? "policy-approval-" + request.ActionId,
            ApprovalHash = approval is null ? "policy-approval-" + request.ActionId : Canonicalization.ApprovalHash(approval),
            RiskClass = request.RiskClass,
            MethodologyRefs = request.MethodologyRefs,
            IssuerRef = "phase2-permit-issuer",
            IssuerSignatureBase64 = string.Empty,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()
        };
        permit.IssuerSignatureBase64 = Convert.ToBase64String(_issuerKey.SignData(Encoding.UTF8.GetBytes(Canonicalization.PermitPayload(permit)), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        lock (_gate)
        {
            if (_relayLost) throw new InvalidOperationException("relay loss occurred while issuing permit");
            if (approval is not null && !_consumedApprovals.Add(Canonicalization.ApprovalHash(approval))) throw new InvalidOperationException("approval has already been consumed");
            _permits.Add(permit.PermitId, permit);
        }
        return permit;
    }

    public bool TryConsume(Permit permit, ActionRequest request, AuthorizationManifest manifest, string workerRef, ApprovalRecord? approval = null)
    {
        var now = AuthoritativeClock.UtcNow;
        lock (_gate)
        {
            if (_relayLost || !_permits.TryGetValue(permit.PermitId, out var stored) || !ReferenceEquals(stored, permit)) return false;
            if (stored.ExpiresAt <= now)
            {
                stored.ConsumptionState = PermitConsumptionState.Expired;
                return false;
            }
            if (now < stored.IssuedAt || stored.ConsumptionState != PermitConsumptionState.Unused || stored.WorkerRef != workerRef) return false;
            if ((stored.RiskClass == RiskClass.R3 || approval is not null) && (approval is null || approval.ApprovalRef != stored.ApprovalRef || Canonicalization.ApprovalHash(approval) != stored.ApprovalHash)) return false;
            if (!Verify(stored) || stored.ActionHash != Canonicalization.ActionHash(request) || stored.ManifestHash != Canonicalization.AuthorizationHash(manifest)) return false;
            var currentPolicy = _policyEngine.Evaluate(request, manifest, approval);
            if (currentPolicy.Decision != PolicyDecision.Allow || currentPolicy.ActionHash != stored.ActionHash || currentPolicy.ScopeHash != stored.ScopeHash || currentPolicy.PolicyVersion != stored.PolicyVersion || currentPolicy.CapabilityRef != stored.CapabilityRef || currentPolicy.AuthorizationRef != stored.AuthorizationRef) return false;
            stored.ConsumptionState = PermitConsumptionState.Consumed;
            stored.ConsumedAt = now;
            return true;
        }
    }

    public bool ValidateConsumed(Permit permit, ActionRequest request, AuthorizationManifest manifest, string workerRef, ApprovalRecord? approval = null)
    {
        var now = AuthoritativeClock.UtcNow;
        lock (_gate)
        {
            return ValidateConsumedLocked(permit, request, manifest, workerRef, approval, null, now);
        }
    }

    public bool TryClaimConsumed(Permit permit, ActionRequest request, AuthorizationManifest manifest, string workerRef, PolicyResult expectedPolicy, ApprovalRecord? approval = null)
    {
        var now = AuthoritativeClock.UtcNow;
        lock (_gate)
        {
            if (_claimedExecutions.Contains(permit.PermitId) || !ValidateConsumedLocked(permit, request, manifest, workerRef, approval, expectedPolicy, now)) return false;
            _claimedExecutions.Add(permit.PermitId);
            return true;
        }
    }

    private bool ValidateConsumedLocked(Permit permit, ActionRequest request, AuthorizationManifest manifest, string workerRef, ApprovalRecord? approval, PolicyResult? expectedPolicy, DateTimeOffset now)
    {
        if (_relayLost || !_permits.TryGetValue(permit.PermitId, out var stored) || !ReferenceEquals(stored, permit) || stored.ConsumptionState != PermitConsumptionState.Consumed) return false;
        if (stored.ExpiresAt <= now || stored.IssuedAt > now || stored.WorkerRef != workerRef) return false;
        if (!Verify(stored) || stored.ActionHash != Canonicalization.ActionHash(request) || stored.ManifestHash != Canonicalization.AuthorizationHash(manifest) || stored.RunId != request.RunId || stored.ActionId != request.ActionId || stored.TargetRef != request.TargetRef || stored.ScopeRef != request.ScopeRef || stored.AuthorizationRef != request.AuthorizationRef || stored.CapabilityRef != request.CapabilityRef || stored.RiskClass != request.RiskClass || stored.MethodologyRefs is null || request.MethodologyRefs is null || !stored.MethodologyRefs.SequenceEqual(request.MethodologyRefs, StringComparer.Ordinal)) return false;
        var expectedApprovalHash = approval is null ? "policy-approval-" + request.ActionId : Canonicalization.ApprovalHash(approval);
        if (stored.ApprovalHash != expectedApprovalHash) return false;
        var currentPolicy = _policyEngine.Evaluate(request, manifest, approval);
        if (currentPolicy.Decision != PolicyDecision.Allow || currentPolicy.ActionHash != stored.ActionHash || currentPolicy.ManifestHash != stored.ManifestHash || currentPolicy.ScopeHash != stored.ScopeHash || currentPolicy.PolicyRef != stored.PolicyRef || currentPolicy.PolicyVersion != stored.PolicyVersion || currentPolicy.AuthorizationRef != stored.AuthorizationRef || currentPolicy.CapabilityRef != stored.CapabilityRef || currentPolicy.RiskClass != stored.RiskClass || !currentPolicy.MethodologyRefs.SequenceEqual(stored.MethodologyRefs, StringComparer.Ordinal)) return false;
        return expectedPolicy is null || (expectedPolicy.Decision == currentPolicy.Decision && expectedPolicy.ActionHash == currentPolicy.ActionHash && expectedPolicy.ManifestHash == currentPolicy.ManifestHash && expectedPolicy.ScopeHash == currentPolicy.ScopeHash && expectedPolicy.PolicyRef == currentPolicy.PolicyRef && expectedPolicy.PolicyVersion == currentPolicy.PolicyVersion && expectedPolicy.AuthorizationRef == currentPolicy.AuthorizationRef && expectedPolicy.CapabilityRef == currentPolicy.CapabilityRef && expectedPolicy.RiskClass == currentPolicy.RiskClass && expectedPolicy.MethodologyRefs.SequenceEqual(currentPolicy.MethodologyRefs, StringComparer.Ordinal));
    }

    public void HandleRelayLoss()
    {
        lock (_gate)
        {
            _relayLost = true;
            foreach (var permit in _permits.Values.Where(item => item.ConsumptionState == PermitConsumptionState.Unused)) permit.ConsumptionState = PermitConsumptionState.Revoked;
        }
    }

    public void RevokeAll()
    {
        lock (_gate)
        {
            foreach (var permit in _permits.Values.Where(item => item.ConsumptionState == PermitConsumptionState.Unused)) permit.ConsumptionState = PermitConsumptionState.Revoked;
        }
    }

    public bool Verify(Permit permit)
    {
        try { return _issuerKey.VerifyData(Encoding.UTF8.GetBytes(Canonicalization.PermitPayload(permit)), Convert.FromBase64String(permit.IssuerSignatureBase64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }

    public void Dispose() => _issuerKey.Dispose();
}
