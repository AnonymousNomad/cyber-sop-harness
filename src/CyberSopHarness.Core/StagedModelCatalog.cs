using System.Text.Json;

namespace CyberSopHarness.Core;

public sealed record StagedModelManifest(
    string ModelRef,
    string ModelRevision,
    string ModelFile,
    long ModelBytes,
    string ModelSha256,
    string Architecture,
    int ContextSize,
    string RuntimeRef,
    string RuntimeCommit,
    string RuntimeBinary,
    string RuntimeSha256,
    string RuntimeVersion,
    string LicenseNotice,
    string RuntimeLicense,
    string ChatTemplate,
    string ChatTemplateSha256,
    string ExpectedServerModel,
    long WorkingSetBytes,
    string LaunchMode,
    string LicenseReview);

public static class StagedModelCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task<Dictionary<string, ModelRuntimeManifest>> LoadAsync(string modelsDirectory, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ModelRuntimeManifest>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(modelsDirectory) || !Directory.Exists(modelsDirectory)) return result;
        foreach (var modelDirectory in Directory.EnumerateDirectories(modelsDirectory).Order(StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(modelDirectory, "MODEL-RUNTIME-MANIFEST.json");
            if (!File.Exists(manifestPath)) continue;
            await using var stream = File.OpenRead(manifestPath);
            var staged = await JsonSerializer.DeserializeAsync<StagedModelManifest>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("staged manifest is empty: " + manifestPath);
            string Resolve(string relative) => Path.GetFullPath(Path.Combine(modelDirectory, relative));
            var licensePath = Resolve(staged.RuntimeLicense);
            if (!File.Exists(licensePath)) throw new InvalidOperationException("staged manifest license is missing: " + licensePath);
            var manifest = new ModelRuntimeManifest(
                staged.ModelRef,
                Resolve(staged.ModelFile),
                staged.ModelSha256,
                staged.ModelRevision,
                Resolve(staged.RuntimeBinary),
                staged.RuntimeSha256,
                staged.RuntimeVersion,
                staged.Architecture,
                licensePath,
                Canonicalization.Sha256Hex(await File.ReadAllBytesAsync(licensePath, cancellationToken)),
                Resolve(staged.ChatTemplate),
                staged.ChatTemplateSha256,
                staged.ContextSize,
                staged.ModelBytes,
                staged.WorkingSetBytes > 0 ? staged.WorkingSetBytes : staged.ModelBytes * 3,
                staged.ModelBytes,
                staged.ExpectedServerModel);
            result[Path.GetFileName(modelDirectory)] = manifest;
        }
        return result;
    }
}
