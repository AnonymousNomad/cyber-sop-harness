using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CyberSopHarness.Core;

public static class Canonicalization
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string ActionPayload(ActionRequest request)
    {
        var orderedArguments = (request.Arguments ?? new Dictionary<string, string>())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var payload = new
        {
            request.Type,
            request.RunId,
            request.ActionId,
            request.ParentEventId,
            request.Phase,
            request.TargetRef,
            request.CapabilityRef,
            Arguments = orderedArguments,
            request.Purpose,
            request.Hypothesis,
            request.ExpectedObservation,
            RiskClass = request.RiskClass.ToString(),
            request.ScopeRef,
            request.AuthorizationRef,
            MethodologyRefs = (request.MethodologyRefs ?? Array.Empty<string>()).ToArray(),
            request.ApprovalRef,
            request.CredentialRef,
            ResolvedAddresses = (request.ResolvedAddresses ?? Array.Empty<string>()).ToArray()
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string ActionHash(ActionRequest request) => Sha256Hex(ActionPayload(request));

    public static string AuthorizationPayload(AuthorizationManifest manifest)
    {
        var payload = new
        {
            manifest.ManifestVersion,
            manifest.EngagementId,
            EngagementMode = manifest.EngagementMode.ToString(),
            Owner = manifest.Authorization.Owner,
            Operator = manifest.Authorization.Operator,
            ArtifactRef = manifest.Authorization.ArtifactRef,
            ThirdPartyRefs = manifest.ThirdPartyRefs.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Allow = manifest.Scope.Allow.Order(StringComparer.Ordinal).ToArray(),
            Deny = manifest.Scope.Deny.Order(StringComparer.Ordinal).ToArray(),
            manifest.Scope.WildcardPolicy,
            manifest.Scope.RedirectPolicy,
            manifest.Scope.ThirdPartyPolicy,
            StartsAt = manifest.TimeWindow.StartsAt.ToUniversalTime().ToString("O"),
            ExpiresAt = manifest.TimeWindow.ExpiresAt.ToUniversalTime().ToString("O"),
            manifest.TimeWindow.TimeZone,
            ExcludedWindows = manifest.TimeWindow.ExcludedWindows
                .OrderBy(window => window.StartsAt)
                .Select(window => new { StartsAt = window.StartsAt.ToUniversalTime().ToString("O"), ExpiresAt = window.ExpiresAt.ToUniversalTime().ToString("O"), window.Reason })
                .ToArray(),
            AllowedMethods = manifest.Methods.Allowed.Order(StringComparer.Ordinal).ToArray(),
            ProhibitedMethods = manifest.Methods.Prohibited.Order(StringComparer.Ordinal).ToArray(),
            AssetDefault = manifest.AssetCriticality.Default,
            AssetTargets = manifest.AssetCriticality.Targets.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
            DataClassification = manifest.DataHandling.Classification,
            DataRedaction = manifest.DataHandling.Redaction,
            DataRetention = manifest.DataHandling.Retention,
            EscalationContacts = manifest.EscalationContacts.OrderBy(contact => contact.Name, StringComparer.Ordinal).ToArray(),
            CredentialAllowedRefs = manifest.CredentialPolicy.AllowedRefs.Order(StringComparer.Ordinal).ToArray(),
            CredentialAutomaticUse = manifest.CredentialPolicy.AutomaticUse,
            CredentialExpiryPolicy = manifest.CredentialPolicy.ExpiryPolicy,
            manifest.RateLimits,
            manifest.Cleanup,
            StopConditions = manifest.StopConditions.Order(StringComparer.Ordinal).ToArray()
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string ScopePayload(ScopeDefinition scope)
    {
        var payload = new
        {
            Allow = scope.Allow.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Deny = scope.Deny.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            scope.WildcardPolicy,
            scope.RedirectPolicy,
            scope.ThirdPartyPolicy
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string ScopeHash(ScopeDefinition scope) => Sha256Hex(ScopePayload(scope));

    public static string AuthorizationHash(AuthorizationManifest manifest) => Sha256Hex(AuthorizationPayload(manifest));

    public static string ApprovalPayload(ApprovalRecord approval) => string.Join("|", approval.ApprovalRef, approval.RunId, approval.ActionId, approval.ActionHash, approval.ManifestHash, approval.TargetRef, approval.CapabilityRef, approval.RiskClass, approval.ApproverRef, approval.ExpiresAt.ToUniversalTime().ToString("O"), approval.Rationale, approval.Nonce);

    public static string ApprovalHash(ApprovalRecord approval) => Sha256Hex(ApprovalPayload(approval));

    public static string PermitPayload(Permit permit)
    {
        var payload = new
        {
            permit.PermitId,
            permit.RunId,
            permit.ActionId,
            permit.ActionHash,
            permit.ManifestHash,
            permit.CanonicalizationRef,
            permit.TargetRef,
            permit.ScopeRef,
            permit.ScopeHash,
            permit.PolicyRef,
            permit.PolicyVersion,
            permit.WorkerRef,
            permit.CapabilityRef,
            permit.AuthorizationRef,
            permit.CredentialRef,
            permit.ApprovalRef,
            permit.ApprovalHash,
            RiskClass = permit.RiskClass.ToString(),
            MethodologyRefs = permit.MethodologyRefs.Order(StringComparer.Ordinal).ToArray(),
            permit.IssuerRef,
            IssuedAt = permit.IssuedAt.ToUniversalTime().ToString("O"),
            ExpiresAt = permit.ExpiresAt.ToUniversalTime().ToString("O"),
            permit.Nonce
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string Sha256Hex(string value) => Sha256Hex(Encoding.UTF8.GetBytes(value));

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
