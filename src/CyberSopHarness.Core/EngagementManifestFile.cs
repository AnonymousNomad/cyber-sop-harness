using System.Security.Cryptography;
using System.Text.Json;

namespace CyberSopHarness.Core;

public static class EngagementManifestFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    public static async Task<AuthorizationManifest> LoadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("engagement manifest path must be absolute", nameof(path));
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var manifest = await JsonSerializer.DeserializeAsync<AuthorizationManifest>(stream, Options, cancellationToken)
            ?? throw new InvalidOperationException("engagement manifest is empty");
        return manifest;
    }

    public static ValidationResult Validate(AuthorizationManifest manifest, string trustedOwnerPublicKeyPem)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedOwnerPublicKeyPem);
        var trustStore = new AuthorizationTrustStore();
        try
        {
            using var publicKey = RSA.Create();
            publicKey.ImportFromPem(trustedOwnerPublicKeyPem);
            trustStore.Register(manifest.Authorization.Owner, publicKey);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or FormatException)
        {
            return new(false, ["trusted owner public key is invalid"]);
        }
        trustStore.Freeze();
        return ManifestValidation.Validate(manifest, trustStore);
    }
}
