using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CyberSopHarness.Core;

public enum ProvenanceKeyRole
{
    RuntimeEvidence,
    Release
}

public static class ProvenanceKeyCustody
{
    public static string Fingerprint(RSA key) => Canonicalization.Sha256Hex(key.ExportSubjectPublicKeyInfo());

    public static string ExportPublicKeyPem(RSA key) => key.ExportSubjectPublicKeyInfoPem();

    public static string FingerprintOfPem(string publicKeyPem)
    {
        using var key = RSA.Create();
        key.ImportFromPem(publicKeyPem);
        return Fingerprint(key);
    }
}

public sealed class ProvenanceKeyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _directory;
    private readonly ISecretProtector _protector;
    private readonly string _applicationEntropy;

    public ProvenanceKeyStore(string directory, ISecretProtector protector, string applicationEntropy)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) throw new ArgumentException("key directory must be an absolute path", nameof(directory));
        if (!protector.IsAvailable) throw new PlatformNotSupportedException("no usable secret protector is available on this platform");
        _directory = directory;
        _protector = protector;
        _applicationEntropy = applicationEntropy;
        Directory.CreateDirectory(directory);
    }

    public RSA CreateOrLoad(ProvenanceKeyRole role)
    {
        var path = KeyPath(role);
        if (File.Exists(path))
        {
            var protectedBytes = File.ReadAllBytes(path);
            try
            {
                var privateBytes = _protector.Unprotect(protectedBytes, Context(role));
                try
                {
                    var key = RSA.Create(2048);
                    key.ImportPkcs8PrivateKey(privateBytes, out _);
                    return key;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(privateBytes);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
        var generated = RSA.Create(2048);
        Persist(role, generated);
        return generated;
    }

    public RSA Rotate(ProvenanceKeyRole role)
    {
        var current = File.Exists(KeyPath(role)) ? CreateOrLoad(role) : null;
        if (current is not null)
        {
            AppendRetiredPublicKey(role, current);
            current.Dispose();
        }
        var fresh = RSA.Create(2048);
        Persist(role, fresh);
        return fresh;
    }

    public IReadOnlyList<string> RetiredPublicKeys(ProvenanceKeyRole role)
    {
        var path = RetiredPath(role);
        if (!File.Exists(path)) return Array.Empty<string>();
        return File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(WrapPem)
            .ToArray();
    }

    private void Persist(ProvenanceKeyRole role, RSA key)
    {
        var privateBytes = key.ExportPkcs8PrivateKey();
        try
        {
            var protectedBytes = _protector.Protect(privateBytes, Context(role));
            var path = KeyPath(role);
            var temporaryPath = path + ".tmp";
            File.WriteAllBytes(temporaryPath, protectedBytes);
            using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Flush(true);
            }
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateBytes);
        }
    }

    private void AppendRetiredPublicKey(ProvenanceKeyRole role, RSA key)
    {
        var path = RetiredPath(role);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var der = key.ExportSubjectPublicKeyInfo();
        try
        {
            File.AppendAllText(path, Convert.ToBase64String(der) + Environment.NewLine);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(der);
        }
    }

    private static string WrapPem(string base64Line) => "-----BEGIN PUBLIC KEY-----\n" + base64Line + "\n-----END PUBLIC KEY-----";

    private string KeyPath(ProvenanceKeyRole role) => Path.Combine(_directory, role.ToString().ToLowerInvariant() + ".key");

    private string RetiredPath(ProvenanceKeyRole role) => Path.Combine(_directory, "retired-" + role.ToString().ToLowerInvariant() + ".pem");

    private string Context(ProvenanceKeyRole role) => _applicationEntropy + "|provenance-key|" + role.ToString().ToLowerInvariant();
}

public sealed class ReleaseSigningAuthority : IDisposable
{
    private readonly RSA _key;
    private readonly IReadOnlyList<string> _retiredPublicKeys;

    public ReleaseSigningAuthority(ProductIdentity identity, RSA key, IReadOnlyList<string>? retiredPublicKeys = null)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        if (identity.ReleaseKeyRef != ProvenanceKeyCustody.Fingerprint(key)) throw new ArgumentException("identity release key reference does not match the signing key", nameof(key));
        _retiredPublicKeys = retiredPublicKeys ?? Array.Empty<string>();
    }

    public ProductIdentity Identity { get; }

    public string PublicKeyPem => ProvenanceKeyCustody.ExportPublicKeyPem(_key);

    public SignedReleaseManifest Issue(string productVersion, IReadOnlyList<ReleaseFileEntry> files)
    {
        if (files.Count == 0 || files.Any(file => string.IsNullOrWhiteSpace(file.RelativePath) || file.Bytes < 0 || !ProviderProposalValidator.IsSha256(file.Sha256))) throw new ArgumentException("release file manifest is invalid", nameof(files));
        var unsigned = new SignedReleaseManifest(Identity.ProductRef, productVersion, Identity.BuildHash, Identity.ReleaseKeyRef, files.ToArray(), string.Empty);
        var signature = _key.SignData(Encoding.UTF8.GetBytes(ProvenanceAuthority.ReleasePayload(unsigned)), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
    }

    public bool Verify(SignedReleaseManifest manifest)
    {
        if (manifest.ProductRef != Identity.ProductRef || manifest.BuildHash != Identity.BuildHash || manifest.Files.Count == 0) return false;
        if (manifest.ReleaseKeyRef == Identity.ReleaseKeyRef && ProvenanceAuthority.VerifyReleaseSignature(manifest, PublicKeyPem)) return true;
        foreach (var retired in _retiredPublicKeys)
        {
            if (ProvenanceKeyCustody.FingerprintOfPem(retired) == manifest.ReleaseKeyRef && ProvenanceAuthority.VerifyReleaseSignature(manifest, retired)) return true;
        }
        return false;
    }

    public static bool Verify(SignedReleaseManifest manifest, string publicKeyPem) => ProvenanceAuthority.VerifyReleaseSignature(manifest, publicKeyPem);

    public void Dispose() => _key.Dispose();
}