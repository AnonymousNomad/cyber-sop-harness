using System.Security.Cryptography;
using System.Text;

namespace CyberSopHarness.Core;

public static class ManifestValidation
{
    public static ValidationResult Validate(AuthorizationManifest manifest, AuthorizationTrustStore trustStore)
    {
        var now = AuthoritativeClock.UtcNow;
        var errors = new List<string>();
        if (manifest.ManifestVersion != "1.0") errors.Add("manifest_version must be 1.0");
        if (string.IsNullOrWhiteSpace(manifest.EngagementId)) errors.Add("engagement_id is required");
        if (manifest.Scope.Allow.Count == 0) errors.Add("scope.allow must contain at least one target");
        if (manifest.Scope.WildcardPolicy is not ("exact-only" or "single-level" or "recursive")) errors.Add("unsupported wildcard policy");
        if (manifest.Scope.RedirectPolicy is not ("same-origin" or "allowlisted" or "block")) errors.Add("unsupported redirect policy");
        if (manifest.Scope.ThirdPartyPolicy is not ("explicit-permission" or "block")) errors.Add("unsupported third-party policy");
        if (manifest.TimeWindow.StartsAt >= manifest.TimeWindow.ExpiresAt) errors.Add("time window is not ordered");
        if (now < manifest.TimeWindow.StartsAt || now > manifest.TimeWindow.ExpiresAt) errors.Add("manifest is outside its time window");
        if (string.IsNullOrWhiteSpace(manifest.TimeWindow.TimeZone)) errors.Add("timezone is required");
        if (manifest.TimeWindow.ExcludedWindows.Any(window => window.StartsAt >= window.ExpiresAt)) errors.Add("excluded time window is not ordered");
        if (manifest.TimeWindow.ExcludedWindows.Any(window => now >= window.StartsAt && now <= window.ExpiresAt)) errors.Add("current time is inside an excluded window");
        if (manifest.Methods.Allowed.Intersect(manifest.Methods.Prohibited, StringComparer.OrdinalIgnoreCase).Any()) errors.Add("method is both allowed and prohibited");
        if (manifest.EscalationContacts.Count == 0) errors.Add("at least one escalation contact is required");
        if (manifest.CredentialPolicy.AutomaticUse) errors.Add("automatic credential use is prohibited");
        if (!double.IsFinite(manifest.RateLimits.RequestsPerSecond) || manifest.RateLimits.RequestsPerSecond <= 0 || manifest.RateLimits.Concurrency <= 0 || manifest.RateLimits.PayloadBytes <= 0) errors.Add("rate limits must be finite and positive");
        if (!manifest.Cleanup.Required || string.IsNullOrWhiteSpace(manifest.Cleanup.Owner) || string.IsNullOrWhiteSpace(manifest.Cleanup.ProcedureRef)) errors.Add("cleanup contract is incomplete");
        if (manifest.StopConditions.Count == 0) errors.Add("stop_conditions must not be empty");
        if (string.IsNullOrWhiteSpace(manifest.Authorization.Owner) || string.IsNullOrWhiteSpace(manifest.Authorization.Operator) || string.IsNullOrWhiteSpace(manifest.Authorization.ArtifactRef)) errors.Add("authorization proof metadata is incomplete");
        if (!AuthorizationVerifier.Verify(manifest, trustStore)) errors.Add("authorization signature is invalid or not issued by a trusted authority");
        return errors.Count == 0 ? ValidationResult.Valid() : new(false, errors);
    }
}

public static class AuthorizationSigner
{
    public static AuthorizationProof Sign(AuthorizationManifest manifest, RSA key)
    {
        var payload = Canonicalization.AuthorizationPayload(manifest);
        var signature = key.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return new AuthorizationProof(
            manifest.Authorization.Owner,
            manifest.Authorization.Operator,
            manifest.Authorization.ArtifactRef,
            Convert.ToBase64String(signature),
            key.ExportRSAPublicKeyPem(),
            payload);
    }
}

public static class AuthorizationVerifier
{
    public static bool Verify(AuthorizationManifest manifest, AuthorizationTrustStore trustStore)
    {
        try
        {
            var proof = manifest.Authorization;
            if (string.IsNullOrWhiteSpace(proof.SignatureBase64)) return false;
            var expected = Canonicalization.AuthorizationPayload(manifest);
            if (!string.Equals(expected, proof.SignedPayload, StringComparison.Ordinal)) return false;
            if (!trustStore.TryGet(proof.Owner, out var trustedKeyPem)) return false;
            using var key = RSA.Create();
            key.ImportFromPem(trustedKeyPem);
            return key.VerifyData(Encoding.UTF8.GetBytes(proof.SignedPayload), Convert.FromBase64String(proof.SignatureBase64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public sealed class AuthorizationTrustStore
{
    private readonly Dictionary<string, string> _keys = new(StringComparer.Ordinal);
    private bool _frozen;

    public void Register(string authorityRef, RSA publicKey)
    {
        if (_frozen) throw new InvalidOperationException("authorization trust store is frozen");
        if (string.IsNullOrWhiteSpace(authorityRef)) throw new ArgumentException("authority reference is required", nameof(authorityRef));
        if (!_keys.TryAdd(authorityRef, publicKey.ExportRSAPublicKeyPem())) throw new InvalidOperationException("authority is already registered");
    }

    public void Freeze() => _frozen = true;

    public bool IsFrozen => _frozen;

    public bool TryGet(string authorityRef, out string publicKeyPem) => _keys.TryGetValue(authorityRef, out publicKeyPem!);
}

public static class ApprovalSigner
{
    public static ApprovalRecord Sign(ApprovalRecord approval, RSA key)
    {
        var payload = Canonicalization.ApprovalPayload(approval);
        var signature = key.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return approval with { SignatureBase64 = Convert.ToBase64String(signature), SignedPayload = payload };
    }
}

public static class ApprovalVerifier
{
    public static bool Verify(ApprovalRecord approval, AuthorizationTrustStore trustStore)
    {
        try
        {
            if (!trustStore.TryGet(approval.ApproverRef, out var publicKeyPem)) return false;
            if (!string.Equals(Canonicalization.ApprovalPayload(approval), approval.SignedPayload, StringComparison.Ordinal)) return false;
            using var key = RSA.Create();
            key.ImportFromPem(publicKeyPem);
            return key.VerifyData(Encoding.UTF8.GetBytes(approval.SignedPayload), Convert.FromBase64String(approval.SignatureBase64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
