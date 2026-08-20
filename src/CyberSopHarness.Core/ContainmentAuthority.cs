using System.Security.Cryptography;
using System.Text;

namespace CyberSopHarness.Core;

public sealed class ContainmentAuthority : IDisposable
{
    private readonly RSA _key;
    private readonly string _authorityRef;

    public ContainmentAuthority(string authorityRef = "phase2-containment-authority", RSA? key = null)
    {
        _authorityRef = authorityRef;
        _key = key ?? RSA.Create(2048);
    }

    public ContainmentAttestation IssueFixture(string workerRef, string boundaryHash) => Issue(workerRef, "phase2-fixture-worker", boundaryHash, false, "fixture", "unprivileged", false);

    internal ContainmentAttestation IssueExternal(string workerRef, string providerRef, string boundaryHash, string privilegeLevel = "unprivileged")
    {
        if (providerRef != "windows-job-object") throw new InvalidOperationException("unregistered external containment provider");
        return Issue(workerRef, providerRef, boundaryHash, true, "external", privilegeLevel, true);
    }

    public bool Verify(ContainmentAttestation attestation, string expectedWorkerRef, EngagementMode mode)
    {
        try
        {
            if (attestation.WorkerRef != expectedWorkerRef || attestation.BoundaryHash.Length != 64 || !attestation.BoundaryHash.All(char.IsAsciiHexDigit)) return false;
            if (mode == EngagementMode.Fixture && (attestation.Mode != "fixture" || attestation.ExternalEnforcement || attestation.HardStopGuaranteed)) return false;
            if (mode == EngagementMode.Authorized && (!attestation.ExternalEnforcement || attestation.Mode != "external" || attestation.ProviderRef != "windows-job-object" || !attestation.HardStopGuaranteed)) return false;
            using var publicKey = RSA.Create();
            publicKey.ImportFromPem(_key.ExportRSAPublicKeyPem());
            return publicKey.VerifyData(Encoding.UTF8.GetBytes(Payload(attestation)), Convert.FromBase64String(attestation.SignatureBase64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private ContainmentAttestation Issue(string workerRef, string providerRef, string boundaryHash, bool externalEnforcement, string mode, string privilegeLevel, bool hardStopGuaranteed)
    {
        var unsigned = new ContainmentAttestation(workerRef, providerRef, boundaryHash, externalEnforcement, mode, privilegeLevel, hardStopGuaranteed, string.Empty);
        var signature = _key.SignData(Encoding.UTF8.GetBytes(Payload(unsigned)), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
    }

    private string Payload(ContainmentAttestation attestation) => string.Join("|", _authorityRef, attestation.WorkerRef, attestation.ProviderRef, attestation.BoundaryHash, attestation.ExternalEnforcement, attestation.Mode, attestation.PrivilegeLevel, attestation.HardStopGuaranteed);

    public void Dispose() => _key.Dispose();
}
