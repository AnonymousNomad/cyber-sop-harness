using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CyberSopHarness.Core;

public sealed record ProductIdentity(string ProductRef, string ProductVersion, string BuildHash, string ReleaseKeyRef);

public sealed record ProvenanceStamp(
    string ProductRef,
    string ProductVersion,
    string BuildHash,
    string ReleaseKeyRef,
    string RunId,
    string ActionId,
    string AuthorizationHash,
    string ScopeHash,
    string PolicyRef,
    string PolicyVersion,
    string EngagementMode,
    string ProviderRef,
    string ModelRef,
    string ModelVersion,
    string ToolRef,
    string ToolVersion,
    string EvidenceEventId,
    string EvidenceHash,
    string RawSha256,
    string? RedactedSha256,
    DateTimeOffset IssuedAt,
    string SignatureBase64);

public sealed record ReleaseFileEntry(string RelativePath, long Bytes, string Sha256);

public sealed record SignedReleaseManifest(
    string ProductRef,
    string ProductVersion,
    string BuildHash,
    string ReleaseKeyRef,
    IReadOnlyList<ReleaseFileEntry> Files,
    string SignatureBase64);

public sealed class ProvenanceAuthority : IDisposable
{
    private readonly RSA _key;
    private readonly IReadOnlyList<string> _retiredPublicKeys;

    public ProvenanceAuthority(ProductIdentity identity, RSA? key = null)
        : this(identity, key ?? RSA.Create(2048), Array.Empty<string>())
    {
    }

    public ProvenanceAuthority(ProductIdentity identity, RSA key, IReadOnlyList<string> retiredPublicKeys)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        if (!ProviderProposalValidator.IsSha256(identity.BuildHash)) throw new ArgumentException("build hash must be SHA-256", nameof(identity));
        if (identity.ReleaseKeyRef != ProvenanceKeyCustody.Fingerprint(key)) throw new ArgumentException("identity release key reference does not match the signing key", nameof(key));
        _retiredPublicKeys = retiredPublicKeys ?? Array.Empty<string>();
    }

    public ProductIdentity Identity { get; }
    public string PublicKeyPem => ProvenanceKeyCustody.ExportPublicKeyPem(_key);

    public ProvenanceStamp Issue(EvidenceEvent evidence, AuthorizationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(manifest);
        var unsigned = new ProvenanceStamp(
            Identity.ProductRef,
            Identity.ProductVersion,
            Identity.BuildHash,
            Identity.ReleaseKeyRef,
            evidence.RunId,
            evidence.ActionId,
            Canonicalization.AuthorizationHash(manifest),
            Canonicalization.ScopeHash(manifest.Scope),
            evidence.PolicyRef,
            evidence.PolicyVersion,
            manifest.EngagementMode.ToString(),
            evidence.Provider.Descriptor.ProviderRef,
            evidence.Provider.Descriptor.ModelRef,
            evidence.Provider.Descriptor.ModelVersion,
            evidence.ToolRef,
            evidence.ToolVersion,
            evidence.ResultEventId,
            evidence.EventHash,
            evidence.RawSha256,
            evidence.RedactedSha256,
            AuthoritativeClock.UtcNow,
            string.Empty);
        var signature = _key.SignData(Encoding.UTF8.GetBytes(Payload(unsigned)), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
    }

    public bool Verify(ProvenanceStamp stamp, EvidenceEvent evidence, AuthorizationManifest manifest)
    {
        if (stamp.ProductRef != Identity.ProductRef || stamp.ProductVersion != Identity.ProductVersion || stamp.BuildHash != Identity.BuildHash) return false;
        if (!FieldsMatch(stamp, evidence, manifest)) return false;
        if (stamp.ReleaseKeyRef == Identity.ReleaseKeyRef && VerifyStampSignature(stamp, PublicKeyPem)) return true;
        foreach (var retired in _retiredPublicKeys)
        {
            if (ProvenanceKeyCustody.FingerprintOfPem(retired) == stamp.ReleaseKeyRef && VerifyStampSignature(stamp, retired)) return true;
        }
        return false;
    }

    public static bool VerifyStamp(ProvenanceStamp stamp, EvidenceEvent evidence, AuthorizationManifest manifest, string publicKeyPem)
    {
        if (!FieldsMatch(stamp, evidence, manifest)) return false;
        return VerifyStampSignature(stamp, publicKeyPem);
    }

    private static bool FieldsMatch(ProvenanceStamp stamp, EvidenceEvent evidence, AuthorizationManifest manifest)
    {
        try
        {
            if (stamp.RunId != evidence.RunId || stamp.ActionId != evidence.ActionId || stamp.EvidenceEventId != evidence.ResultEventId || stamp.EvidenceHash != evidence.EventHash || stamp.RawSha256 != evidence.RawSha256 || stamp.RedactedSha256 != evidence.RedactedSha256) return false;
            if (stamp.AuthorizationHash != Canonicalization.AuthorizationHash(manifest) || stamp.ScopeHash != Canonicalization.ScopeHash(manifest.Scope) || stamp.EngagementMode != manifest.EngagementMode.ToString()) return false;
            if (stamp.PolicyRef != evidence.PolicyRef || stamp.PolicyVersion != evidence.PolicyVersion || stamp.ToolRef != evidence.ToolRef || stamp.ToolVersion != evidence.ToolVersion || stamp.ProviderRef != evidence.Provider.Descriptor.ProviderRef || stamp.ModelRef != evidence.Provider.Descriptor.ModelRef || stamp.ModelVersion != evidence.Provider.Descriptor.ModelVersion) return false;
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    private static bool VerifyStampSignature(ProvenanceStamp stamp, string publicKeyPem)
    {
        try
        {
            using var publicKey = RSA.Create();
            publicKey.ImportFromPem(publicKeyPem);
            return publicKey.VerifyData(Encoding.UTF8.GetBytes(Payload(stamp)), Convert.FromBase64String(stamp.SignatureBase64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }

    public SignedReleaseManifest IssueReleaseManifest(string productVersion, IReadOnlyList<ReleaseFileEntry> files)
    {
        if (files.Count == 0 || files.Any(file => string.IsNullOrWhiteSpace(file.RelativePath) || file.Bytes < 0 || !ProviderProposalValidator.IsSha256(file.Sha256))) throw new ArgumentException("release file manifest is invalid", nameof(files));
        var unsigned = new SignedReleaseManifest(Identity.ProductRef, productVersion, Identity.BuildHash, Identity.ReleaseKeyRef, files.ToArray(), string.Empty);
        var signature = _key.SignData(Encoding.UTF8.GetBytes(ReleasePayload(unsigned)), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
    }

    public bool VerifyReleaseManifest(SignedReleaseManifest manifest)
    {
        if (manifest.ProductRef != Identity.ProductRef || manifest.BuildHash != Identity.BuildHash || manifest.Files.Count == 0) return false;
        if (manifest.ReleaseKeyRef == Identity.ReleaseKeyRef && VerifyReleaseSignature(manifest, PublicKeyPem)) return true;
        foreach (var retired in _retiredPublicKeys)
        {
            if (ProvenanceKeyCustody.FingerprintOfPem(retired) == manifest.ReleaseKeyRef && VerifyReleaseSignature(manifest, retired)) return true;
        }
        return false;
    }

    internal static bool VerifyReleaseSignature(SignedReleaseManifest manifest, string publicKeyPem)
    {
        try
        {
            using var publicKey = RSA.Create();
            publicKey.ImportFromPem(publicKeyPem);
            return publicKey.VerifyData(Encoding.UTF8.GetBytes(ReleasePayload(manifest)), Convert.FromBase64String(manifest.SignatureBase64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }

    public static string Render(ProvenanceStamp stamp, bool verified)
    {
        var status = verified ? "VERIFIED" : "UNVERIFIED - DO NOT RELY ON THIS RESULT";
        return string.Join(Environment.NewLine,
            "CYBER-SOP-HARNESS PROVENANCE",
            "STATUS: " + status,
            "PRODUCT: " + stamp.ProductRef + " " + stamp.ProductVersion,
            "RUN: " + stamp.RunId,
            "ACTION: " + stamp.ActionId,
            "AUTH: sha256:" + stamp.AuthorizationHash,
            "EVIDENCE: sha256:" + stamp.EvidenceHash,
            "RAW: sha256:" + stamp.RawSha256,
            "SIGNATURE_KEY: " + stamp.ReleaseKeyRef);
    }

    public static string ToJson(ProvenanceStamp stamp) => JsonSerializer.Serialize(stamp, new JsonSerializerOptions { WriteIndented = true });

    internal static string Payload(ProvenanceStamp stamp) => string.Join("|", stamp.ProductRef, stamp.ProductVersion, stamp.BuildHash, stamp.ReleaseKeyRef, stamp.RunId, stamp.ActionId, stamp.AuthorizationHash, stamp.ScopeHash, stamp.PolicyRef, stamp.PolicyVersion, stamp.EngagementMode, stamp.ProviderRef, stamp.ModelRef, stamp.ModelVersion, stamp.ToolRef, stamp.ToolVersion, stamp.EvidenceEventId, stamp.EvidenceHash, stamp.RawSha256, stamp.RedactedSha256, stamp.IssuedAt.ToUniversalTime().ToString("O"));
    internal static string ReleasePayload(SignedReleaseManifest manifest) => string.Join("|", manifest.ProductRef, manifest.ProductVersion, manifest.BuildHash, manifest.ReleaseKeyRef, string.Join(";", manifest.Files.Select(file => string.Join(",", file.RelativePath, file.Bytes, file.Sha256))));

    public void Dispose() => _key.Dispose();
}
