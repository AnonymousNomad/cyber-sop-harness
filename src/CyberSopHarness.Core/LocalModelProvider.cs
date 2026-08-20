using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CyberSopHarness.Core;

public static class ActionProposalParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    static ActionProposalParser()
    {
        Options.Converters.Add(new JsonStringEnumConverter());
    }

    public static bool TryParse(string content, out ActionRequest? action, out string reason)
    {
        action = null;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(content) || !content.TrimStart().StartsWith('{') || !content.TrimEnd().EndsWith('}'))
        {
            reason = "provider output must be one JSON object";
            return false;
        }
        try
        {
            action = JsonSerializer.Deserialize<ActionRequest>(content, Options);
            var validation = ActionRequestValidator.Validate(action);
            if (!validation.IsValid)
            {
                reason = string.Join("; ", validation.Errors);
                action = null;
                return false;
            }
            return true;
        }
        catch (JsonException exception)
        {
            reason = "provider output JSON is invalid: " + exception.Message;
            return false;
        }
    }
}

public static class ProposalTextNormalizer
{
    public static string StripOuterCodeFence(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return content;
        var lines = trimmed.Split('\n');
        var closer = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                closer = i;
                break;
            }
        }
        if (closer < 0) return content;
        for (var i = closer + 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i])) return content;
        }
        return string.Join('\n', lines.Skip(1).Take(closer - 1)).Trim();
    }
}

public sealed class LocalModelProviderAdapter : IModelProviderAdapter, IDisposable
{
    private readonly LocalModelRuntime _runtime;
    private readonly HttpClient _http;
    private readonly ProviderDescriptor _descriptor;

    public LocalModelProviderAdapter(LocalModelRuntime runtime, ProviderDescriptor descriptor, HttpClient? httpClient = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public ProviderDescriptor Descriptor => _descriptor;

    public async Task<ProviderProposal> ProposeAsync(string prompt, AuthorizationManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(manifest);
        var started = Stopwatch.GetTimestamp();
        var status = _runtime.Status;
        if (!status.Ready || status.Endpoint is null || status.Identity is null) return Failure(ProviderFailureClass.Unavailable, string.Empty, started);
        var payload = new
        {
            model = status.Identity.ModelId,
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
            using var response = await _http.PostAsJsonAsync(new Uri(status.Endpoint, "v1/chat/completions"), payload, cancellationToken);
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
            return Failure(ProviderFailureClass.Timeout, string.Empty, started);
        }
        catch (HttpRequestException)
        {
            return Failure(ProviderFailureClass.Unavailable, string.Empty, started);
        }
        catch (JsonException)
        {
            return Failure(ProviderFailureClass.InvalidOutput, string.Empty, started);
        }
    }

    public void Dispose() => _http.Dispose();

    private ProviderProposal Failure(ProviderFailureClass failure, string output, long started) => new(_descriptor, new ActionRequest(), Canonicalization.Sha256Hex(output), Stopwatch.GetElapsedTime(started), 0, failure);
}
