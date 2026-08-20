using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace CyberSopHarness.Core;

public static class EndpointGuard
{
    public static bool IsLoopback(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (IPAddress.TryParse(endpoint.Host, out var address)) return IPAddress.IsLoopback(address);
        return false;
    }
}

public sealed class LoopbackEndpointProviderAdapter : IModelProviderAdapter, IDisposable
{
    private readonly Uri _endpoint;
    private readonly ProviderDescriptor _descriptor;
    private readonly HttpClient _http;

    public LoopbackEndpointProviderAdapter(Uri endpoint, ProviderDescriptor descriptor, HttpClient? httpClient = null)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri) throw new ArgumentException("endpoint must be absolute", nameof(endpoint));
        if (!EndpointGuard.IsLoopback(endpoint)) throw new ArgumentException("user-local endpoint must be loopback", nameof(endpoint));
        _endpoint = endpoint;
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public ProviderDescriptor Descriptor => _descriptor;

    public async Task<ProviderProposal> ProposeAsync(string prompt, AuthorizationManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(manifest);
        var started = Stopwatch.GetTimestamp();
        var payload = new
        {
            model = _descriptor.ModelRef,
            messages = new[]
            {
                new { role = "system", content = "You are an untrusted local proposal generator. Return exactly one JSON ACTION_REQUEST object. Never use markdown code fences. Output only the bare JSON object with no markdown and no commentary. Never authorize actions, expand scope, call tools, use shell, or claim verification." },
                new { role = "user", content = prompt }
            },
            temperature = 0,
            max_tokens = 768,
            stream = false
        };
        try
        {
            using var response = await _http.PostAsJsonAsync(new Uri(_endpoint, "v1/chat/completions"), payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            var outputHash = Canonicalization.Sha256Hex(content);
            var latency = Stopwatch.GetElapsedTime(started);
            var tokenUsage = document.RootElement.TryGetProperty("usage", out var usage) && usage.TryGetProperty("total_tokens", out var total) && total.TryGetInt32(out var parsedTokens) ? parsedTokens : 0;
            if (!ActionProposalParser.TryParse(ProposalTextNormalizer.StripOuterCodeFence(content), out var action, out _)) return new(_descriptor, new ActionRequest(), outputHash, latency, tokenUsage, ProviderFailureClass.InvalidOutput);
            return new(_descriptor, action!, outputHash, latency, tokenUsage, ProviderFailureClass.None);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(ProviderFailureClass.Timeout, started);
        }
        catch (HttpRequestException)
        {
            return Failure(ProviderFailureClass.Unavailable, started);
        }
        catch (JsonException)
        {
            return Failure(ProviderFailureClass.InvalidOutput, started);
        }
    }

    public void Dispose() => _http.Dispose();

    private ProviderProposal Failure(ProviderFailureClass failure, long started) => new(_descriptor, new ActionRequest(), Canonicalization.Sha256Hex(string.Empty), Stopwatch.GetElapsedTime(started), 0, failure);
}

public sealed record ExternalEgressConsent(
    string ConsentId,
    string ProviderId,
    DateTimeOffset ConsentedAt,
    string DataHandlingStatement);

public sealed class ExternalApiProviderAdapter : IModelProviderAdapter, IDisposable
{
    private readonly Uri _endpoint;
    private readonly ProviderDescriptor _descriptor;
    private readonly PersistentSecretStore _secrets;
    private readonly string _providerId;
    private readonly ExternalEgressConsent? _consent;
    private readonly HttpClient _http;

    public ExternalApiProviderAdapter(Uri endpoint, ProviderDescriptor descriptor, PersistentSecretStore secrets, string providerId, ExternalEgressConsent? consent = null, HttpClient? httpClient = null)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri) throw new ArgumentException("external endpoint must be absolute", nameof(endpoint));
        _endpoint = endpoint;
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _providerId = string.IsNullOrWhiteSpace(providerId) ? throw new ArgumentException("provider id is required", nameof(providerId)) : providerId;
        _consent = consent;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public ProviderDescriptor Descriptor => _descriptor;

    public async Task<ProviderProposal> ProposeAsync(string prompt, AuthorizationManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(manifest);
        var started = Stopwatch.GetTimestamp();
        if (_consent is null || !string.Equals(_consent.ProviderId, _providerId, StringComparison.Ordinal) || _consent.ConsentedAt > AuthoritativeClock.UtcNow)
        {
            return Failure(ProviderFailureClass.PolicyBlocked, started);
        }
        string secret;
        try
        {
            secret = _secrets.Load(_providerId);
        }
        catch (Exception exception) when (exception is FileNotFoundException or CryptographicException or IOException)
        {
            return Failure(ProviderFailureClass.PolicyBlocked, started);
        }
        var payload = new
        {
            model = _descriptor.ModelRef,
            messages = new[]
            {
                new { role = "system", content = "You are an untrusted external proposal generator. Return exactly one JSON ACTION_REQUEST object. Never use markdown code fences. Output only the bare JSON object with no markdown and no commentary. Never authorize actions, expand scope, call tools, use shell, or claim verification." },
                new { role = "user", content = prompt }
            },
            temperature = 0,
            max_tokens = 768,
            stream = false
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_endpoint, "v1/chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        request.Content = JsonContent.Create(payload);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            var outputHash = Canonicalization.Sha256Hex(content);
            var latency = Stopwatch.GetElapsedTime(started);
            var tokenUsage = document.RootElement.TryGetProperty("usage", out var usage) && usage.TryGetProperty("total_tokens", out var total) && total.TryGetInt32(out var parsedTokens) ? parsedTokens : 0;
            if (!ActionProposalParser.TryParse(ProposalTextNormalizer.StripOuterCodeFence(content), out var action, out _)) return new(_descriptor, new ActionRequest(), outputHash, latency, tokenUsage, ProviderFailureClass.InvalidOutput);
            return new(_descriptor, action!, outputHash, latency, tokenUsage, ProviderFailureClass.None);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(ProviderFailureClass.Timeout, started);
        }
        catch (HttpRequestException)
        {
            return Failure(ProviderFailureClass.Unavailable, started);
        }
        catch (JsonException)
        {
            return Failure(ProviderFailureClass.InvalidOutput, started);
        }
    }

    public void Dispose() => _http.Dispose();

    private ProviderProposal Failure(ProviderFailureClass failure, long started) => new(_descriptor, new ActionRequest(), Canonicalization.Sha256Hex(string.Empty), Stopwatch.GetElapsedTime(started), 0, failure);
}