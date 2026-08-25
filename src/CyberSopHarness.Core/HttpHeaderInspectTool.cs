using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace CyberSopHarness.Core;

public sealed class HttpHeaderInspectTool : IContainedNetworkToolAdapter, IAsyncDisposable
{
    private static readonly string[] RedactedHeaders = ["authorization", "cookie", "set-cookie", "proxy-authorization"];
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public HttpHeaderInspectTool(string toolRef, string toolVersion, HttpClient? client = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolVersion);
        ToolRef = toolRef;
        ToolVersion = toolVersion;
        _client = client ?? CreateClient();
        _ownsClient = client is null;
    }

    public HttpHeaderInspectTool(string toolRef, string toolVersion, IReadOnlyList<string> resolvedAddresses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolVersion);
        ArgumentNullException.ThrowIfNull(resolvedAddresses);
        ToolRef = toolRef;
        ToolVersion = toolVersion;
        _client = CreateClient(resolvedAddresses);
        _ownsClient = true;
    }

    public string ToolRef { get; }

    public string ToolVersion { get; }

    public async Task<ToolAdapterResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NetworkToolGuard.RequireAuthorizedNetworkAction(context, CapabilityRef);
        var target = context.Envelope.Request.TargetRef;
        if (!Uri.TryCreate(target, UriKind.Absolute, out var targetUri) || targetUri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("HTTP inspection requires an absolute HTTP(S) target");
        if (!string.IsNullOrEmpty(targetUri.UserInfo)) throw new InvalidOperationException("HTTP inspection rejects userinfo");
        if (targetUri.PathAndQuery.Length > 2048) throw new InvalidOperationException("HTTP target path exceeds 2048 characters");
        var method = context.Envelope.Request.Arguments.TryGetValue("method", out var requestedMethod)
            ? requestedMethod.ToUpperInvariant()
            : "GET";
        if (method is not ("GET" or "HEAD")) throw new InvalidOperationException("HTTP inspection permits only GET or HEAD");
        using var request = new HttpRequestMessage(new HttpMethod(method), targetUri);
        request.Headers.UserAgent.ParseAdd("CyberSopHarness-HeaderInspect/1.0");
        request.Version = targetUri.Scheme == "https" ? HttpVersion.Version11 : HttpVersion.Version11;
        var started = DateTimeOffset.UtcNow;
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var limit = Math.Min(context.Capability.MaxOutputBytes, 64L * 1024L);
        var buffer = new byte[8192];
        long bodyBytes = 0;
        var truncated = false;
        using var hashedBody = new MemoryStream();
        while (bodyBytes < limit)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, limit - bodyBytes)), cancellationToken);
            if (read == 0) break;
            bodyBytes += read;
            hashedBody.Write(buffer, 0, read);
        }
        while (await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, 65536)), cancellationToken) > 0)
        {
            bodyBytes++;
            truncated = true;
            if (bodyBytes >= 1024L * 1024L * 16L) break;
        }
        var headers = response.Headers.Concat(response.Content.Headers)
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                header => header.Key,
                header => RedactedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase)
                    ? new[] { "[REDACTED]" }
                    : header.SelectMany(entry => entry.Value).SelectMany(value => value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var observation = JsonSerializer.Serialize(new
        {
            method,
            status = (int)response.StatusCode,
            elapsed_ms = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds,
            headers,
            body_bytes_observed = bodyBytes,
            body_hash_prefix = Convert.ToHexString(SHA256.HashData(hashedBody.ToArray()))[..24].ToLowerInvariant(),
            body_truncated_after_limit = truncated,
            redirects_followed = 0,
            cookies_stored = false
        }, JsonOptions);
        return new(
            ToolResultStatus.Success,
            0,
            System.Text.Encoding.UTF8.GetBytes(observation),
            ["http.headers"],
            Array.Empty<string>(),
            "PENDING");
    }

    public Task<string> CleanupAsync(ToolExecutionContext context, ToolAdapterResult result, CancellationToken cancellationToken) =>
        Task.FromResult("CLEANUP_OK|" + context.Envelope.ActionHash);

    public ValueTask DisposeAsync()
    {
        if (_ownsClient) _client.Dispose();
        return ValueTask.CompletedTask;
    }

    public const string CapabilityRef = "http.headers.inspect";

    private static JsonSerializerOptions JsonOptions { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static SocketsHttpHandler CreateHandler(IReadOnlyList<string> resolvedAddresses)
    {
        var allowed = resolvedAddresses.Select(address => IPAddress.Parse(address)).ToArray();
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            Proxy = null,
            MaxResponseHeadersLength = 64,
            MaxConnectionsPerServer = 1,
            EnableMultipleHttp2Connections = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = async (context, cancellationToken) =>
            {
                if (!IPAddress.TryParse(context.DnsEndPoint.Host, out var requested))
                {
                    requested = allowed.FirstOrDefault(address =>
                        address.AddressFamily == AddressFamily.InterNetworkV6 == (context.DnsEndPoint.AddressFamily == AddressFamily.InterNetworkV6))
                        ?? throw new IOException("resolved address family does not match target");
                }
                if (!allowed.Any(address => address.Equals(requested))) throw new IOException("connection address is not operator-resolved");
                var socket = new Socket(requested.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(requested, context.DnsEndPoint.Port, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
    }

    private static HttpClient CreateClient() => CreateClient(Array.Empty<string>());

    private static HttpClient CreateClient(IReadOnlyList<string> resolvedAddresses)
    {
        var handler = CreateHandler(resolvedAddresses);
        return new(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }
}

public static class NetworkToolGuard
{
    public static void RequireAuthorizedNetworkAction(ToolExecutionContext context, string expectedCapability)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCapability);
        if (context.Manifest.EngagementMode != EngagementMode.Authorized) throw new InvalidOperationException("network tools are unavailable in fixture mode");
        if (context.Policy.Decision != PolicyDecision.Allow || !string.Equals(context.Capability.CapabilityRef, expectedCapability, StringComparison.Ordinal))
            throw new InvalidOperationException("network execution was not authorized for this capability");
        if (!IsTargetAllowed(context.Envelope.Request.TargetRef, context.Capability.NetworkDestinations))
            throw new InvalidOperationException("target origin is outside the tool allowlist");
    }

    public static bool IsTargetAllowed(string target, IReadOnlyList<string> origins)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var targetUri)) return false;
        foreach (var entry in origins)
        {
            if (!Uri.TryCreate(entry, UriKind.Absolute, out var origin)) continue;
            if (targetUri.Scheme.Equals(origin.Scheme, StringComparison.OrdinalIgnoreCase)
                && targetUri.Host.Equals(origin.Host, StringComparison.OrdinalIgnoreCase)
                && targetUri.EffectivePort(origin) == origin.Port)
                return true;
        }
        return false;
    }

    private static int EffectivePort(this Uri uri, Uri origin) => uri.IsDefaultPort ? origin.Port : uri.Port;
}
