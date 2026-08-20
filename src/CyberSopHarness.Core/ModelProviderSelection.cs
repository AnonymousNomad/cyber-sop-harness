namespace CyberSopHarness.Core;

using System.Text.Json;

public enum ModelProviderKind
{
    VerifiedLocal,
    UserLocal,
    ExternalApi
}

public sealed record ModelProviderSelection(
    string SelectionId,
    ModelProviderKind Kind,
    string ProviderRef,
    string ModelRef,
    string Endpoint,
    string? ModelPath,
    string? SecretHandleRef,
    bool ExternalEgressAllowed,
    bool LicenseAcknowledged);

public static class ModelProviderSelectionValidator
{
    public static ValidationResult Validate(ModelProviderSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(selection.SelectionId) || string.IsNullOrWhiteSpace(selection.ProviderRef) || string.IsNullOrWhiteSpace(selection.ModelRef)) errors.Add("provider selection identity is incomplete");
        if (!selection.LicenseAcknowledged) errors.Add("model/license terms must be acknowledged");
        switch (selection.Kind)
        {
            case ModelProviderKind.VerifiedLocal:
                if (selection.ExternalEgressAllowed) errors.Add("verified local selection cannot enable external egress");
                if (string.IsNullOrWhiteSpace(selection.ModelPath) || !string.IsNullOrWhiteSpace(selection.SecretHandleRef)) errors.Add("verified local selection has invalid path or secret state");
                break;
            case ModelProviderKind.UserLocal:
                if (selection.ExternalEgressAllowed || (string.IsNullOrWhiteSpace(selection.ModelPath) && string.IsNullOrWhiteSpace(selection.Endpoint)) || !string.IsNullOrWhiteSpace(selection.SecretHandleRef)) errors.Add("user-local selection has invalid endpoint, path, or secret state");
                break;
            case ModelProviderKind.ExternalApi:
                if (!selection.ExternalEgressAllowed || string.IsNullOrWhiteSpace(selection.Endpoint) || string.IsNullOrWhiteSpace(selection.SecretHandleRef)) errors.Add("external API selection requires explicit egress and a secret handle");
                break;
            default:
                errors.Add("provider selection kind is invalid");
                break;
        }
        return errors.Count == 0 ? ValidationResult.Valid() : new(false, errors);
    }
}

public sealed class ModelProviderSelectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly string _path;

    public ModelProviderSelectionStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new ArgumentException("selection path must be absolute", nameof(path));
        _path = path;
    }

    public async Task SaveAsync(ModelProviderSelection selection, CancellationToken cancellationToken)
    {
        var validation = ModelProviderSelectionValidator.Validate(selection);
        if (!validation.IsValid) throw new InvalidOperationException("provider selection is invalid: " + string.Join("; ", validation.Errors));
        var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("selection directory is missing");
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(selection, JsonOptions), cancellationToken);
        File.Move(temporaryPath, _path, true);
    }

    public async Task<ModelProviderSelection?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return null;
        await using var stream = File.OpenRead(_path);
        var selection = await JsonSerializer.DeserializeAsync<ModelProviderSelection>(stream, JsonOptions, cancellationToken);
        if (selection is null) throw new InvalidOperationException("provider selection file is empty");
        var validation = ModelProviderSelectionValidator.Validate(selection);
        if (!validation.IsValid) throw new InvalidOperationException("stored provider selection is invalid: " + string.Join("; ", validation.Errors));
        return selection;
    }
}
