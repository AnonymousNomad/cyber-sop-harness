using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace CyberSopHarness.Core;

public sealed class DnsReverseLookupTool : IContainedNetworkToolAdapter
{
    public const string CapabilityRef = "dns.reverse.lookup";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string ToolRef => "dns-reverse-lookup";
    public string ToolVersion => "1.0";

    public async Task<ToolAdapterResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NetworkToolGuard.RequireAuthorizedNetworkAction(context, CapabilityRef);

        var target = context.Envelope.Request.TargetRef;
        if (!IPAddress.TryParse(target, out var address))
        {
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && IPAddress.TryParse(uri.Host, out address))
            {
                // Extracted IP from URL
            }
            else
            {
                throw new InvalidOperationException("DNS reverse lookup requires an IPv4 or IPv6 address");
            }
        }

        if (IsPrivateOrReserved(address!))
            throw new InvalidOperationException("DNS reverse lookup blocks private and reserved addresses");

        var arpaName = BuildArpaName(address!);
        if (arpaName is null)
            throw new InvalidOperationException("cannot build reverse-DNS name for address family");

        var started = DateTimeOffset.UtcNow;
        var hostnames = await ResolveAsync(arpaName, cancellationToken);
        var elapsed = DateTimeOffset.UtcNow - started;

        var observation = JsonSerializer.Serialize(new
        {
            query = arpaName,
            resolved_addresses = new[] { address!.ToString() },
            hostnames,
            hostname_count = hostnames.Length,
            elapsed_ms = (long)elapsed.TotalMilliseconds,
            address_family = address.AddressFamily == AddressFamily.InterNetwork ? "ipv4" : "ipv6"
        }, JsonOptions);

        return new(
            ToolResultStatus.Success,
            0,
            System.Text.Encoding.UTF8.GetBytes(observation),
            ["dns.reverse"],
            Array.Empty<string>(),
            "PENDING");
    }

    public Task<string> CleanupAsync(ToolExecutionContext context, ToolAdapterResult result, CancellationToken cancellationToken) =>
        Task.FromResult("CLEANUP_OK|" + context.Envelope.ActionHash);

    private static async Task<string[]> ResolveAsync(string arpaName, CancellationToken cancellationToken)
    {
        try
        {
            var entries = await Dns.GetHostEntryAsync(arpaName, cancellationToken);
            return entries.Aliases.Length > 0 ? entries.Aliases.ToArray() : [entries.HostName];
        }
        catch (SocketException)
        {
            return [];
        }
    }

    private static string? BuildArpaName(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return $"{bytes[3]}.{bytes[2]}.{bytes[1]}.{bytes[0]}.in-addr.arpa";
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            var nibbles = new char[bytes.Length * 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                nibbles[i * 2] = "0123456789abcdef"[bytes[i] & 0xF];
                nibbles[i * 2 + 1] = "0123456789abcdef"[bytes[i] >> 4];
            }
            Array.Reverse(nibbles);
            return string.Join(".", nibbles) + ".ip6.arpa";
        }
        return null;
    }

    internal static bool IsPrivateOrReserved(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 || bytes[0] == 127
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] == 0 || bytes[0] >= 224;
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal
                || address.Equals(IPAddress.IPv6Loopback)
                || address.Equals(IPAddress.IPv6None);
        }
        return true;
    }
}
