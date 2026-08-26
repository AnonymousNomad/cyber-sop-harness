# Edge DNS Leak Prevention

## What To Do
Ensure all DNS resolution goes through Tor or encrypted channels (DoH/DoT) to prevent DNS leaks that could expose the operator's real IP or location to DNS servers.

## Why
DNS leaks are the most common way anonymous operations are deanonymized. Even with Tor, if the system resolver sends DNS queries to the ISP's DNS server, the target domain being looked up is visible to the ISP.

## Code Guidance

```javascript
// src/opsec/dns-guard.mjs
import { SocksProxyAgent } from 'socks-proxy-agent';

export class DNSGuard {
  #mode; // 'tor' | 'doh' | 'dot' | 'system'
  #dohUrl = 'https://dns.google/dns-query';
  #agent;

  constructor({ mode = 'doh', torSocksPort = 9050, agent = null }) {
    this.#mode = mode;
    this.#agent = agent;
  }

  async resolve(hostname, type = 'A') {
    switch (this.#mode) {
      case 'tor':
        return this.#resolveViaTor(hostname, type);
      case 'doh':
        return this.#resolveDoH(hostname, type);
      case 'dot':
        return this.#resolveDoT(hostname, type);
      default:
        return this.#resolveSystem(hostname, type);
    }
  }

  async #resolveViaTor(hostname, type) {
    // SOCKS5h routes DNS through Tor
    const agent = this.#agent || new SocksProxyAgent('socks5h://127.0.0.1:9050');
    const res = await fetch(`${this.#dohUrl}?name=${hostname}&type=${type}`, {
      headers: { 'Accept': 'application/dns-json' },
      agent,
      signal: AbortSignal.timeout(10000),
    });
    const data = await res.json();
    return data.Answer?.map(a => ({ type: a.type, data: a.data, ttl: a.TTL })) || [];
  }

  async #resolveDoH(hostname, type) {
    const res = await fetch(`${this.#dohUrl}?name=${hostname}&type=${type}`, {
      headers: { 'Accept': 'application/dns-json' },
      signal: AbortSignal.timeout(5000),
    });
    const data = await res.json();
    return data.Answer?.map(a => ({ type: a.type, data: a.data, ttl: a.TTL })) || [];
  }

  async #resolveDoT(hostname, type) {
    // DNS over TLS via Node.js native TLS
    const { connect } = await import('node:tls');
    return new Promise((resolve, reject) => {
      const sock = connect(853, '8.8.8.8', { servername: 'dns.google' });
      // Simplified — full DoT requires DNS wire format encoding
      sock.on('error', reject);
      sock.on('secureConnect', () => {
        sock.end();
        resolve([]); // Placeholder — implement DNS wire format
      });
    });
  }

  async #resolveSystem(hostname, type) {
    const dns = await import('node:dns/promises');
    const lookup = type === 'A' ? 'resolve4' :
                   type === 'AAAA' ? 'resolve6' :
                   type === 'MX' ? 'resolveMx' : 'resolve4';
    return dns[lookup](hostname);
  }

  get mode() { return this.#mode; }
}
```

## Threat Matrix

| Threat | Impact | Mitigation |
|---|---|---|
| System DNS resolver used accidentally | ISP sees all queried domains | Override DNS in all tool adapters |
| DoH endpoint blocked by network | DNS resolution fails | Fallback to Tor SOCKS5h |
| DNS cache poisoning | Wrong IP returned, missed vulns | Fresh resolution per operation |
| IPv6 DNS leak | Real interface IPv6 visible | Disable IPv6 or route through Tor |

## Dependencies
- Tor SOCKS5 proxy (for DNS-over-Tor)
- Or: DNS-over-HTTPS endpoint (Google, Cloudflare, Quad9)
- `node:dns/promises` (built-in)

## Pitfalls & Bugs
- `dns.resolve4()` always uses system resolver — never use directly for anonymous ops
- DoH adds latency (~50-200ms per query) — cache results for short TTLs
- Android may have custom DNS settings that override Node.js resolver
- Some TLDs have slow DNS propagation — wait for TTL expiry
- Quad9 (9.9.9.9) blocks malware domains — may interfere with recon of malicious infrastructure
