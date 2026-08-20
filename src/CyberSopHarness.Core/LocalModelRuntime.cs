using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace CyberSopHarness.Core;

public sealed record ModelRuntimeManifest(
    string ModelRef,
    string ModelPath,
    string ModelSha256,
    string ModelRevision,
    string RuntimePath,
    string RuntimeSha256,
    string RuntimeVersion,
    string ExpectedArchitecture,
    string LicensePath,
    string LicenseSha256,
    string ChatTemplatePath,
    string ChatTemplateSha256,
    int ContextSize,
    long MaxModelBytes,
    long MaxWorkingSetBytes,
    long MaxVramBytes,
    string ExpectedServerModel);

public sealed record ModelRuntimeValidation(bool IsValid, IReadOnlyList<string> Errors);

public sealed record ModelRuntimeIdentity(string ModelId, string RuntimeVersion, string ModelRevision);

public sealed record ModelRuntimeStatus(bool Ready, int? ProcessId, Uri? Endpoint, ModelRuntimeIdentity? Identity, string? Error);

public sealed class LocalModelRuntime : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly TimeSpan _readinessTimeout;
    private readonly object _gate = new();
    private Process? _process;
    private ModelRuntimeManifest? _manifest;
    private int _port;
    private ModelRuntimeStatus _status = new(false, null, null, null, null);

    public LocalModelRuntime(HttpClient? httpClient = null, TimeSpan? readinessTimeout = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _readinessTimeout = readinessTimeout ?? TimeSpan.FromSeconds(180);
    }

    public ModelRuntimeStatus Status
    {
        get { lock (_gate) return _status; }
    }

    public async Task<ModelRuntimeStatus> StartAsync(ModelRuntimeManifest manifest, int port, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var validation = await ModelRuntimeValidator.ValidateAsync(manifest, cancellationToken);
        if (!validation.IsValid) throw new InvalidOperationException("model runtime manifest is invalid: " + string.Join("; ", validation.Errors));
        await StopAsync();
        var startInfo = new ProcessStartInfo
        {
            FileName = manifest.RuntimePath,
            WorkingDirectory = Path.GetDirectoryName(manifest.RuntimePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add(manifest.ModelPath);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--offline");
        startInfo.ArgumentList.Add("--no-webui");
        startInfo.ArgumentList.Add("--no-agent");
        startInfo.ArgumentList.Add("--chat-template-file");
        startInfo.ArgumentList.Add(manifest.ChatTemplatePath);
        startInfo.ArgumentList.Add("--alias");
        startInfo.ArgumentList.Add(manifest.ExpectedServerModel);
        startInfo.ArgumentList.Add("--ctx-size");
        startInfo.ArgumentList.Add(manifest.ContextSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--n-gpu-layers");
        startInfo.ArgumentList.Add("0");
        startInfo.ArgumentList.Add("--no-repack");
        startInfo.ArgumentList.Add("--threads");
        startInfo.ArgumentList.Add("6");
        startInfo.ArgumentList.Add("--n-predict");
        startInfo.ArgumentList.Add("8");
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("model runtime process did not start");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        lock (_gate)
        {
            _process = process;
            _manifest = manifest;
            _port = port;
            _status = new(false, process.Id, new Uri($"http://127.0.0.1:{port}/"), null, null);
        }
        try
        {
            var endpoint = new Uri($"http://127.0.0.1:{port}/");
            var identity = await WaitUntilReadyAsync(endpoint, manifest, cancellationToken);
            lock (_gate) _status = new(true, process.Id, endpoint, identity, null);
            return Status;
        }
        catch (Exception exception)
        {
            lock (_gate) _status = new(false, process.Id, new Uri($"http://127.0.0.1:{port}/"), null, exception.Message);
            await StopAsync();
            throw;
        }
    }

    public async Task StopAsync()
    {
        Process? process;
        lock (_gate)
        {
            process = _process;
            _process = null;
            _manifest = null;
            _status = new(false, null, null, null, null);
        }
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (InvalidOperationException) { }
        catch (TimeoutException) { }
        finally
        {
            process.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _http.Dispose();
    }

    private async Task<ModelRuntimeIdentity> WaitUntilReadyAsync(Uri endpoint, ModelRuntimeManifest manifest, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(_readinessTimeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var health = await _http.GetAsync(new Uri(endpoint, "health"), cancellationToken);
                if (health.IsSuccessStatusCode)
                {
                    var identity = await ReadIdentityAsync(new Uri(endpoint, "v1/models"), manifest, cancellationToken);
                    await WarmupAsync(endpoint, manifest, cancellationToken);
                    return identity;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(250, cancellationToken);
        }
        throw new TimeoutException("model runtime health/readiness timeout");
    }

    private async Task<ModelRuntimeIdentity> ReadIdentityAsync(Uri endpoint, ModelRuntimeManifest manifest, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var modelId = document.RootElement.GetProperty("data")[0].GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(modelId) || !string.Equals(modelId, manifest.ExpectedServerModel, StringComparison.Ordinal)) throw new InvalidOperationException("model runtime identity does not match manifest");
        return new(modelId, manifest.RuntimeVersion, manifest.ModelRevision);
    }

    private async Task WarmupAsync(Uri endpoint, ModelRuntimeManifest manifest, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = manifest.ExpectedServerModel,
            messages = new[] { new { role = "system", content = "You are a local proposal generator. Do not call tools or authorize actions." }, new { role = "user", content = "Return exactly the word READY." } },
            max_tokens = 8,
            temperature = 0,
            stream = false
        };
        using var response = await _http.PostAsJsonAsync(new Uri(endpoint, "v1/chat/completions"), payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) throw new InvalidOperationException("model warmup returned no choices");
    }
}

public static class ModelRuntimeValidator
{
    public static async Task<ModelRuntimeValidation> ValidateAsync(ModelRuntimeManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(manifest.ModelRef) || string.IsNullOrWhiteSpace(manifest.ModelRevision) || string.IsNullOrWhiteSpace(manifest.RuntimeVersion) || string.IsNullOrWhiteSpace(manifest.ExpectedArchitecture) || string.IsNullOrWhiteSpace(manifest.ExpectedServerModel)) errors.Add("runtime identity is incomplete");
        if (!IsSha256(manifest.ModelSha256) || !IsSha256(manifest.RuntimeSha256) || !IsSha256(manifest.LicenseSha256)) errors.Add("runtime/model/license hashes are invalid");
        if (!IsSha256(manifest.ChatTemplateSha256)) errors.Add("chat template hash is invalid");
        if (manifest.ContextSize <= 0 || manifest.MaxModelBytes <= 0 || manifest.MaxWorkingSetBytes <= 0 || manifest.MaxVramBytes <= 0) errors.Add("runtime resource limits are invalid");
        await CheckFileAsync(manifest.ModelPath, manifest.ModelSha256, manifest.MaxModelBytes, "model", errors, cancellationToken);
        await CheckFileAsync(manifest.RuntimePath, manifest.RuntimeSha256, 512L * 1024L * 1024L, "runtime", errors, cancellationToken);
        await CheckFileAsync(manifest.LicensePath, manifest.LicenseSha256, 16L * 1024L * 1024L, "license", errors, cancellationToken);
        await CheckFileAsync(manifest.ChatTemplatePath, manifest.ChatTemplateSha256, 16L * 1024L * 1024L, "chat template", errors, cancellationToken);
        return errors.Count == 0 ? new(true, Array.Empty<string>()) : new(false, errors);
    }

    private static async Task CheckFileAsync(string path, string expectedHash, long maxBytes, string label, List<string> errors, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))
        {
            errors.Add(label + " file is missing or not absolute");
            return;
        }
        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > maxBytes) errors.Add(label + " file size is outside the manifest limit");
        if (!IsSha256(expectedHash)) return;
        await using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        if (!string.Equals(hash, expectedHash, StringComparison.Ordinal)) errors.Add(label + " SHA-256 does not match the manifest");
    }

    private static bool IsSha256(string value) => value is not null && value.Length == 64 && value.All(char.IsAsciiHexDigit);
}

public static class ModelRuntimeManifestLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task<ModelRuntimeManifest> LoadAndValidateAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new ArgumentException("runtime manifest path must be absolute", nameof(path));
        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<ModelRuntimeManifest>(stream, JsonOptions, cancellationToken) ?? throw new InvalidOperationException("runtime manifest is empty");
        var validation = await ModelRuntimeValidator.ValidateAsync(manifest, cancellationToken);
        if (!validation.IsValid) throw new InvalidOperationException("runtime manifest is invalid: " + string.Join("; ", validation.Errors));
        return manifest;
    }
}
