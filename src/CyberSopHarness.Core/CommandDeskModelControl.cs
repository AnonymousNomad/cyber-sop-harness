namespace CyberSopHarness.Core;

public sealed class CommandDeskModelControl
{
    private readonly ModelProviderSelectionStore _selectionStore;

    public CommandDeskModelControl(
        IReadOnlyDictionary<string, ModelRuntimeManifest> manifests,
        ModelProviderSelectionStore selectionStore)
    {
        Manifests = manifests;
        _selectionStore = selectionStore;
    }

    public IReadOnlyDictionary<string, ModelRuntimeManifest> Manifests { get; }

    public CommandDeskResult Status(ModelProviderSelection? selection, LocalModelRuntime? runtime)
    {
        if (Manifests.Count == 0) return CommandDeskResult.Warning("no staged model/runtime manifests found", "stage models/<name>/MODEL-RUNTIME-MANIFEST.json");
        var selected = selection?.Kind == ModelProviderKind.VerifiedLocal ? selection.ProviderRef : null;
        var details = Manifests.Select(item =>
        {
            var manifest = item.Value;
            var marker = item.Key.Equals(selected, StringComparison.Ordinal) ? "selected" : "available";
            return $"{item.Key}: {marker}; model={manifest.ModelRef}; revision={manifest.ModelRevision}; context={manifest.ContextSize}; threads={manifest.ThreadCount}";
        }).ToList();
        if (runtime is not null)
        {
            var status = runtime.Status;
            details.Add(status.Ready && status.Identity is not null
                ? $"runtime=ready pid={status.ProcessId} endpoint={status.Endpoint} model={status.Identity.ModelId}"
                : $"runtime=stopped error={status.Error ?? "none"}");
        }
        return CommandDeskResult.Info("staged models inspected", details.ToArray());
    }

    public async Task<CommandDeskResult> PinAsync(string name, bool acknowledgeLicense, CancellationToken cancellationToken)
    {
        if (!acknowledgeLicense) return CommandDeskResult.UsageError("model pin requires --ack-license yes after reviewing the license", "pinning records a verified-local provider selection");
        var resolution = await ResolveAsync(name, cancellationToken);
        if (!resolution.IsValid || resolution.Manifest is null) return resolution.Result!;
        var manifest = resolution.Manifest;
        var validation = await ModelRuntimeValidator.ValidateAsync(manifest, cancellationToken);
        if (!validation.IsValid) return CommandDeskResult.Failure("pinned artifacts failed verification", validation.Errors.ToArray());
        var resources = DeviceResourceGate.Check(manifest, manifest.ModelPath);
        if (!resources.IsValid) return CommandDeskResult.Failure("device failed the model resource gate", resources.Errors.ToArray());
        var selection = new ModelProviderSelection(
            $"desk-{manifest.ModelRef}",
            ModelProviderKind.VerifiedLocal,
            name,
            manifest.ModelRef,
            "http://127.0.0.1:18080",
            manifest.ModelPath,
            null,
            false,
            true);
        await _selectionStore.SaveAsync(selection, cancellationToken);
        return CommandDeskResult.Success(
            $"pinned {name} as verified local provider",
            $"model={manifest.ModelRef}",
            $"revision={manifest.ModelRevision}",
            $"context={manifest.ContextSize}",
            $"threads={manifest.ThreadCount}",
            $"memory_budget={manifest.MaxWorkingSetBytes}",
            $"memory_available={resources.AvailableMemoryBytes}");
    }

    public async Task<(bool IsValid, ModelRuntimeManifest? Manifest, CommandDeskResult? Result)> ResolveAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return (false, null, CommandDeskResult.UsageError("model name is required", "run model status to list staged names"));
        if (!Manifests.TryGetValue(name, out var manifest) || manifest is null) return (false, null, CommandDeskResult.Failure($"no staged model named '{name}'"));
        if (!File.Exists(manifest.ChatTemplatePath))
        {
            return (false, null, CommandDeskResult.Failure("chat template is missing", manifest.ChatTemplatePath));
        }
        await Task.CompletedTask;
        return (true, manifest, null);
    }

}
