namespace CyberSopHarness.Core;

public sealed class RuntimeSession : IAsyncDisposable
{
    private readonly ModelProviderSelection _selection;
    private readonly IModelProviderAdapter _provider;
    private readonly LocalModelRuntime? _runtime;

    public RuntimeSession(ModelProviderSelection selection, IModelProviderAdapter provider, LocalModelRuntime? runtime)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _runtime = runtime;
    }

    public ModelProviderSelection Selection => _selection;
    public IModelProviderAdapter Provider => _provider;
    public bool HasLocalRuntime => _runtime is not null;
    public int? LocalProcessId => _runtime?.Status.ProcessId;

    public async ValueTask DisposeAsync()
    {
        if (_runtime is not null) await _runtime.StopAsync();
        if (_provider is IDisposable disposable) disposable.Dispose();
    }
}

public sealed class HarnessBootstrapper
{
    private readonly ModelProviderSelectionStore _selectionStore;
    private readonly PersistentSecretStore _secrets;
    private readonly IReadOnlyDictionary<string, ModelRuntimeManifest> _manifests;
    private readonly IReadOnlyDictionary<string, ExternalEgressConsent> _consents;

    public HarnessBootstrapper(
        ModelProviderSelectionStore selectionStore,
        PersistentSecretStore secrets,
        IReadOnlyDictionary<string, ModelRuntimeManifest> manifests,
        IReadOnlyDictionary<string, ExternalEgressConsent> consents)
    {
        _selectionStore = selectionStore ?? throw new ArgumentNullException(nameof(selectionStore));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _manifests = manifests ?? throw new ArgumentNullException(nameof(manifests));
        _consents = consents ?? throw new ArgumentNullException(nameof(consents));
    }

    public async Task<RuntimeSession> StartAsync(int port, CancellationToken cancellationToken)
    {
        var selection = await _selectionStore.LoadAsync(cancellationToken)
            ?? throw new InvalidOperationException("no provider selection; run setup first");
        switch (selection.Kind)
        {
            case ModelProviderKind.VerifiedLocal:
                return await StartManagedAsync(selection, port, cancellationToken);
            case ModelProviderKind.UserLocal when !string.IsNullOrWhiteSpace(selection.ModelPath):
                return await StartManagedAsync(selection, port, cancellationToken);
            case ModelProviderKind.UserLocal:
                return StartLoopback(selection);
            case ModelProviderKind.ExternalApi:
                return StartExternal(selection);
            default:
                throw new InvalidOperationException("provider selection kind is unsupported");
        }
    }

    private async Task<RuntimeSession> StartManagedAsync(ModelProviderSelection selection, int port, CancellationToken cancellationToken)
    {
        if (selection.ExternalEgressAllowed) throw new InvalidOperationException("managed local selection cannot enable external egress");
        if (!_manifests.TryGetValue(selection.ProviderRef, out var manifest) || manifest is null) throw new InvalidOperationException("no runtime manifest for the selected provider: " + selection.ProviderRef);
        var validation = await ModelRuntimeValidator.ValidateAsync(manifest, cancellationToken);
        if (!validation.IsValid) throw new InvalidOperationException("selected model runtime is invalid: " + string.Join("; ", validation.Errors));
        var runtime = new LocalModelRuntime(readinessTimeout: TimeSpan.FromSeconds(420));
        ModelRuntimeStatus status;
        try
        {
            status = await runtime.StartAsync(manifest, port, cancellationToken);
        }
        catch (TimeoutException)
        {
            await runtime.StopAsync();
            await runtime.DisposeAsync();
            throw new InvalidOperationException("selected model runtime did not become ready: health/readiness timeout");
        }
        catch
        {
            await runtime.StopAsync();
            await runtime.DisposeAsync();
            throw;
        }
        if (!status.Ready || status.Identity is null)
        {
            await runtime.StopAsync();
            await runtime.DisposeAsync();
            throw new InvalidOperationException("selected model runtime did not become ready: " + (status.Error ?? "unknown"));
        }
        var descriptor = new ProviderDescriptor(selection.ProviderRef, manifest.ModelRef, manifest.RuntimeVersion, Canonicalization.Sha256Hex(manifest.ModelPath + "|" + manifest.RuntimePath), "local-only", "none", "typed");
        return new RuntimeSession(selection, new LocalModelProviderAdapter(runtime, descriptor), runtime);
    }

    private RuntimeSession StartLoopback(ModelProviderSelection selection)
    {
        if (string.IsNullOrWhiteSpace(selection.Endpoint)) throw new InvalidOperationException("user-local selection has no endpoint or model path");
        var uri = new Uri(selection.Endpoint);
        if (!EndpointGuard.IsLoopback(uri)) throw new InvalidOperationException("user-local endpoint must be loopback");
        var descriptor = new ProviderDescriptor(selection.ProviderRef, selection.ModelRef, "loopback", Canonicalization.Sha256Hex(selection.Endpoint), "local-only", "none", "typed");
        return new RuntimeSession(selection, new LoopbackEndpointProviderAdapter(uri, descriptor), null);
    }

    private RuntimeSession StartExternal(ModelProviderSelection selection)
    {
        if (string.IsNullOrWhiteSpace(selection.Endpoint)) throw new InvalidOperationException("external selection has no endpoint");
        if (!_consents.TryGetValue(selection.ProviderRef, out var consent) || consent is null) throw new InvalidOperationException("external provider has no recorded consent");
        if (!_secrets.Exists(selection.ProviderRef)) throw new InvalidOperationException("external provider has no stored secret");
        var uri = new Uri(selection.Endpoint);
        var descriptor = new ProviderDescriptor(selection.ProviderRef, selection.ModelRef, "remote", Canonicalization.Sha256Hex(selection.Endpoint), "remote", "remote retention", "typed");
        return new RuntimeSession(selection, new ExternalApiProviderAdapter(uri, descriptor, _secrets, selection.ProviderRef, consent), null);
    }
}