# Edge Scope Evaluator

## What To Do
Implement precise target-scope matching for CIDR ranges (IPv4), domain wildcards, and URL prefixes. Every tool adapter calls this independently as defense-in-depth, even though the policy engine already checked.

## Why
Scope violations are the most serious operational failure in bug bounty work. Testing an out-of-scope target can violate legal agreements. Double-checking in both the policy engine and each adapter prevents a bug in one layer from causing a breach.

## Code Guidance
```javascript
import { isIPv4 } from 'node:net';

function ipToLong(ip) {
  return ip.split('.').reduce((acc, octet) => (acc << 8) + parseInt(octet, 10), 0) >>> 0;
}

export function cidrContains(cidr, ip) {
  if (!isIPv4(ip)) return false;
  const [network, bitsStr] = cidr.split('/');
  const prefixLen = parseInt(bitsStr, 10);
  if (prefixLen < 0 || prefixLen > 32) return false;
  if (!isIPv4(network)) return false;

  const mask = prefixLen === 0 ? 0 : (0xFFFFFFFF << (32 - prefixLen)) >>> 0;
  return (ipToLong(ip) & mask) === (ipToLong(network) & mask);
}

export function domainMatches(pattern, hostname) {
  // Exact match
  if (pattern === hostname) return true;
  // Wildcard: *.example.com matches sub.example.com but NOT example.com
  if (pattern.startsWith('*.')) {
    const suffix = pattern.slice(2); // "example.com"
    return hostname.endsWith('.' + suffix) && hostname.length > suffix.length + 1;
  }
  return false;
}

export function urlInScope(urlBase, urlTest) {
  try {
    const base = new URL(urlBase);
    const test = new URL(urlTest);
    if (base.hostname !== test.hostname && !domainMatches(base.hostname, test.hostname)) return false;
    return test.pathname.startsWith(base.pathname);
  } catch { return false; }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| CIDR off-by-one includes/excludes wrong host | Legal breach or missed finding | Extensive unit tests with boundary IPs |
| Rebinding attack: DNS changes between resolve and connect | Hit wrong server | Pin resolved IP in permit; adapter connects to pinned IP |
| Unicode domain confusion (homograph attack) | Test wrong domain | Normalize with punycode; compare ASCII forms |
| IPv6 targets bypass IPv4-only checks | Out-of-scope access | Explicitly reject or separately handle IPv6 |

## Dependencies
- Node.js built-in `net` module for `isIPv4`

## Pitfalls & Bugs
- JS bitwise operators convert operands to signed 32-bit integers. Use `>>> 0` to force unsigned interpretation.
- `0.0.0.0/0` matches everything including localhost; explicitly exclude loopback unless intended.
- Domain comparisons are case-sensitive in the code above; lowercase both inputs before comparison.
- A wildcard pattern `*.example.com` does NOT match `example.com` itself. Add both patterns if both should be in scope.
- URL normalization: `https://example.com/path/../admin` resolves differently than expected; normalize before comparing.
