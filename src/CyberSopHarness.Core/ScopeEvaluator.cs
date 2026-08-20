using System.Net;

namespace CyberSopHarness.Core;

public sealed class ScopeEvaluator
{
    private static readonly HashSet<string> MetadataHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "metadata.google.internal",
        "instance-data"
    };

    private readonly AuthorizationManifest _manifest;

    public ScopeEvaluator(AuthorizationManifest manifest)
    {
        _manifest = manifest;
    }

    public ScopeDecision Evaluate(string target, IReadOnlyList<string>? resolvedAddresses = null)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var targetUri) && targetUri.Scheme is not ("http" or "https")) return new(false, string.Empty, "target scheme is not allowed");
        var host = CanonicalHost(target);
        if (string.IsNullOrWhiteSpace(host)) return new(false, string.Empty, "target has no canonical host");
        if (IsHardDenied(host)) return new(false, host, "hard deny target");
        if (_manifest.Scope.Deny.Any(entry => MatchesEntry(host, entry))) return new(false, host, "target matches deny list");
        if (!_manifest.Scope.Allow.Any(entry => MatchesEntry(host, entry))) return new(false, host, "target is not in allow list");
        var isDeclaredThirdParty = _manifest.ThirdPartyRefs.Any(entry => MatchesEntry(host, entry));
        if (isDeclaredThirdParty && _manifest.Scope.ThirdPartyPolicy == "block") return new(false, host, "third-party target is blocked by policy");
        if (_manifest.EngagementMode == EngagementMode.Authorized && !IPAddress.TryParse(host, out _) && (resolvedAddresses is null || resolvedAddresses.Count == 0)) return new(false, host, "hostname requires resolved-address evidence before action");
        if (resolvedAddresses is not null)
        {
            foreach (var resolved in resolvedAddresses)
            {
                if (!IPAddress.TryParse(resolved, out var address) || IsHardDenied(address.ToString())) return new(false, host, "resolved address is denied or invalid");
                if (_manifest.Scope.Deny.Any(entry => IPInCidr(address.ToString(), entry) || string.Equals(NormalizeIp(entry), NormalizeIp(address.ToString()), StringComparison.OrdinalIgnoreCase))) return new(false, host, "resolved address matches deny list");
                if (_manifest.EngagementMode == EngagementMode.Authorized && !_manifest.Scope.Allow.Any(entry => IPInCidr(address.ToString(), entry) || string.Equals(NormalizeIp(entry), NormalizeIp(address.ToString()), StringComparison.OrdinalIgnoreCase))) return new(false, host, "resolved address is not explicitly in scope");
            }
        }
        return new(true, host, "target is allowlisted");
    }

    public ScopeDecision EvaluateRedirect(string original, string destination)
    {
        var originalHost = CanonicalHost(original);
        var destinationDecision = Evaluate(destination);
        if (!destinationDecision.Allowed) return destinationDecision with { Reason = "redirect blocked: " + destinationDecision.Reason };
        if (_manifest.Scope.RedirectPolicy == "block") return new(false, destinationDecision.CanonicalTarget, "redirects are blocked by policy");
        if (_manifest.Scope.RedirectPolicy == "same-origin")
        {
            if (!Uri.TryCreate(original, UriKind.Absolute, out var originalUri) || !Uri.TryCreate(destination, UriKind.Absolute, out var destinationUri)) return new(false, destinationDecision.CanonicalTarget, "same-origin redirect requires absolute HTTP(S) URLs");
            if (!string.Equals(originalUri.Scheme, destinationUri.Scheme, StringComparison.OrdinalIgnoreCase) || !string.Equals(originalUri.Host, destinationUri.Host, StringComparison.OrdinalIgnoreCase) || EffectivePort(originalUri) != EffectivePort(destinationUri)) return new(false, destinationDecision.CanonicalTarget, "redirect is not same-origin");
        }
        return destinationDecision with { Reason = "redirect is allowed" };
    }

    private static int EffectivePort(Uri uri) => uri.Port >= 0 ? uri.Port : uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;

    public static string CanonicalHost(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host)) return NormalizeIp(uri.Host.TrimEnd('.'));
        return NormalizeIp(target.Trim().TrimEnd('.'));
    }

    private bool IsHardDenied(string host)
    {
        if (MetadataHosts.Contains(host) || host is "169.254.169.254" or "fd00:ec2::254") return true;
        if (!IPAddress.TryParse(host, out var ip)) return _manifest.EngagementMode == EngagementMode.Authorized && host == "localhost";
        if (IsMetadataIp(ip)) return true;
        if (_manifest.EngagementMode == EngagementMode.Fixture) return IsPrivateOrReserved(ip) && !IPAddress.IsLoopback(ip);
        return IsPrivateOrReserved(ip);
    }

    private static bool IsMetadataIp(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.ToString() is "169.254.169.254" or "fd00:ec2::254";
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 0 || bytes[0] == 10 || bytes[0] == 127 || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) || (bytes[0] == 169 && bytes[1] == 254) || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168) || bytes[0] >= 224;
        }
        return ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6Multicast || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (bytes[0] & 0xfe) == 0xfc || (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80);
    }

    private bool MatchesEntry(string host, string entry)
    {
        var normalized = NormalizeIp(entry.Trim().TrimEnd('.'));
        if (normalized.Contains('/')) return IPInCidr(host, normalized);
        if (!normalized.StartsWith("*.", StringComparison.Ordinal)) return string.Equals(host, normalized, StringComparison.OrdinalIgnoreCase);
        var suffix = normalized[1..];
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
        var prefix = host[..^suffix.Length];
        return _manifest.Scope.WildcardPolicy switch
        {
            "recursive" => prefix.Length > 0,
            "single-level" => prefix.Trim('.').Length > 0 && !prefix.Trim('.').Contains('.'),
            _ => false
        };
    }

    private static bool IPInCidr(string host, string cidr)
    {
        if (!IPAddress.TryParse(host, out var address)) return false;
        var parts = cidr.Split('/', 2);
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefix)) return false;
        var mappedNetwork = network.IsIPv4MappedToIPv6;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (mappedNetwork)
        {
            if (prefix < 96) return false;
            prefix -= 96;
            network = network.MapToIPv4();
        }
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (addressBytes.Length != networkBytes.Length || prefix < 0 || prefix > addressBytes.Length * 8) return false;
        var fullBytes = prefix / 8;
        var remainingBits = prefix % 8;
        for (var index = 0; index < fullBytes; index++) if (addressBytes[index] != networkBytes[index]) return false;
        if (remainingBits == 0) return true;
        var mask = (byte)(0xff << (8 - remainingBits));
        return (addressBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }

    private static string NormalizeIp(string value)
    {
        if (!IPAddress.TryParse(value, out var address)) return value;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return address.ToString().ToLowerInvariant();
    }
}
