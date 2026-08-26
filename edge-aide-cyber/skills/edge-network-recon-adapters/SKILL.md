# Edge Network Reconnaissance Adapters

## What To Do
Wrap DNS reverse lookup, whois, and nmap port scanning as typed adapters. These are read-only reconnaissance operations suitable for authorized bug bounty engagements.

## Why
Reconnaissance is the first phase of any engagement and produces structured data that feeds into vulnerability assessment. Wrapping these as governed adapters means they're scoped, permitted, evidenced, and sanitized.

## Code Guidance
```javascript
import dns from 'node:dns/promises';

// DNS Reverse Lookup — pure Node.js, no external binary needed
export function createDnsReverseAdapter() {
  return {
    name: 'dns.reverse',
    capability: 'network.recon',
    riskLevel: 'R1',
    async execute(params) {
      const records = {};
      records.a = await dns.resolve4(params.target).catch(() => []);
      records.aaaa = await dns.resolve6(params.target).catch(() => []);
      records.mx = await dns.resolveMx(params.target).catch(() => []);
      records.txt = await dns.resolveTxt(params.target).catch(() => []);
      records.ns = await dns.resolveNs(params.target).catch(() => []);
      return { ok: true, data: records };
    },
  };
}

// Nmap wrapper — requires nmap binary
export function createNmapAdapter(nmapPath) {
  return {
    name: 'nmap.scan',
    capability: 'network.recon',
    riskLevel: 'R2',
    async execute(params, permit, scopeEval) {
      // Only allow safe scan types on edge devices
      const allowedFlags = ['-sT', '-sV', '--top-ports'];
      const args = ['--', ...allowedFlags.filter(f => params.flags?.includes(f))];
      if (params.topPorts) args.push('--top-ports', String(params.topPorts));
      args.push(params.target);

      const result = await execFileAsync(nmapPath, args, { timeout: 60000 });
      return { ok: true, data: { stdout: result.stdout.slice(0, 32768), target: params.target } };
    },
  };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Nmap scan against out-of-scope host | Legal violation | Scope evaluator checks independently before execution |
| Aggressive scan flags crash edge device | DoS on own device | Whitelist only safe flag combinations |
| DNS rebinding during resolution | Connect to wrong server | Document limitation; pin resolved IPs for subsequent connections |
| Large nmap output fills RAM | OOM kill on edge device | Truncate at 32 KiB |
| Whois reveals registrant PII | Privacy violation | Redact personal information from output |

## Dependencies
- Node.js built-in `dns/promises` module (no external dependency)
- nmap binary installed in Termux (`pkg install nmap`)
- Optional: whois binary

## Pitfalls & Bugs
- Android's DNS resolver may differ from the system's /etc/resolv.conf; results can be inconsistent.
- nmap on Android/Termux may not support all scan types (no raw sockets without root).
- `dns.resolve*()` methods bypass /etc/hosts and query DNS servers directly. Use `dns.lookup()` for system resolver behavior.
- nmap XML output is easier to parse than grep-able text but requires an XML parser library.
- The `--` separator before user arguments prevents option injection like `-oG /path/payload`.
