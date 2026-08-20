using System.Net;
using System.Text.Json;

namespace CyberSopHarness.Core;

public static class ExternalEndpointValidator
{
    public static bool TryValidate(string? value, out Uri? endpoint, out string reason)
    {
        endpoint = null;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            reason = "endpoint is empty";
            return false;
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            reason = "endpoint must be an absolute URI";
            return false;
        }
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            reason = "endpoint must use https (or http on loopback)";
            return false;
        }
        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !EndpointGuard.IsLoopback(uri))
        {
            reason = "http endpoints are allowed only on loopback; use https for remote endpoints";
            return false;
        }
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            reason = "credentials must not be embedded in the endpoint URL; store them via secret set";
            return false;
        }
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            reason = "endpoint must not contain a query or fragment";
            return false;
        }
        endpoint = uri;
        return true;
    }
}

public sealed class ExternalEndpointStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ExternalEndpointStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new ArgumentException("endpoint store path must be absolute", nameof(path));
        _path = path;
    }

    public async Task<Uri?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return null;
        try
        {
            await using var stream = File.OpenRead(_path);
            var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var value = document.RootElement.TryGetProperty("endpoint", out var element) ? element.GetString() : null;
            return ExternalEndpointValidator.TryValidate(value, out var endpoint, out _) ? endpoint : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async Task SaveAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        if (!ExternalEndpointValidator.TryValidate(endpoint.ToString(), out _, out var reason)) throw new ArgumentException(reason, nameof(endpoint));
        var json = JsonSerializer.Serialize(new { endpoint = endpoint.ToString() }, JsonOptions);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(_path, json, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}