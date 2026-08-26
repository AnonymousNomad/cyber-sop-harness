# Edge Advanced Reconnaissance Module

## What To Do
Build a structured recon pipeline that chains subdomain enumeration, DNS resolution, HTTP probing, and technology fingerprinting into a single governed workflow.

## Why
Recon is the longest phase of bug bounty. Automating the chain (subfinder -> dns.resolve -> httpx -> nuclei) with governance at each step ensures coverage, evidence, and scope compliance.

## Code Guidance
```javascript
// src/tools/recon-pipeline.mjs
export class ReconPipeline {
  async run(domain, { subfinder, dnsAdapter, httpx, nuclei }) {
    const subs = await subfinder.execute({ domain });
    const resolved = await Promise.all(subs.data.subdomains.map(s => dnsAdapter.execute({ target: s })));
    const liveHosts = await httpx.execute({ targets: resolved.flat().map(r => r.data?.records?.a).flat().filter(Boolean) });
    const vulns = await nuclei.execute({ target: domain });
    return { subdomains: subs.count, liveHosts: liveHosts.count, findings: vulns.data.findings };
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Subdomain enum hits out-of-scope | Legal violation | Scope check before each target |
| Pipeline runs indefinitely | Resource exhaustion | Per-step timeouts + total budget |
| DNS放大攻击 via large enum | Network abuse flagged | Rate limit all DNS queries |

## Dependencies
- subfinder, dns.reverse, httpx.probe, nuclei.scan adapters

## Pitfalls
- Subdomain lists can be huge (10k+) — process in batches
- Some subdomains respond with wildcard DNS — filter false positives
- nuclei templates need regular updates