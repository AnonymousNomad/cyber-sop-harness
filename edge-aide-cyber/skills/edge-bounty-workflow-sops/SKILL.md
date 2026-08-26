# Edge Bounty Workflow SOPs

## What To Do
Define pre-built SOPs for common bug bounty phases: reconnaissance, subdomain enumeration, vulnerability scanning, and report writing. Each SOP maps to the tool adapters available on edge.

## Why
Bug bounty hunters follow similar workflows. Pre-built SOPs give them a starting point that's already scoped, permitted, and evidenced. They can customize targets but shouldn't need to design methodology from scratch.

## Code Guidance
```javascript
// Example: Reconnaissance SOP
export const RECON_SOP = {
  id: 'recon-basic',
  name: 'Basic Reconnaissance',
  description: 'Initial target reconnaissance for bug bounty engagement',
  scope: 'Must be run with a valid engagement manifest loaded',
  steps: [
    {
      id: 'dns-resolve',
      name: 'DNS Resolution',
      description: 'Resolve target domain to IP addresses',
      tool: 'dns.reverse',
      riskLevel: 'R1',
      params: { target: '{{TARGET_DOMAIN}}' },
      dependsOn: [],
      approvalRequired: false,
    },
    {
      id: 'http-headers',
      name: 'HTTP Header Inspection',
      description: 'Inspect HTTP response headers for security headers',
      tool: 'http.headers',
      riskLevel: 'R1',
      params: { url: 'https://{{TARGET_DOMAIN}}' },
      dependsOn: ['dns-resolve'],
      approvalRequired: false,
    },
    {
      id: 'port-scan-top',
      name: 'Top Port Scan',
      description: 'Scan top 1000 ports on resolved IPs',
      tool: 'nmap.scan',
      riskLevel: 'R2',
      params: { target: '{{TARGET_IP}}', flags: ['-sT'], topPorts: 1000 },
      dependsOn: ['dns-resolve'],
      approvalRequired: true,
    },
  ],
};
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Template variable not replaced ({{TARGET_DOMAIN}} literal sent to tool) | Tool receives garbage input | Validate all params are resolved before dispatching |
| SOP used without engagement manifest | Out-of-scope testing | Compiler requires manifest context at load time |
| Steps executed out of order bypass safety checks | Port scan before DNS confirms target is real | Enforce topological order from compiler output |

## Dependencies
- All referenced tools must be registered in the adapter registry

## Pitfalls & Bugs
- Template variables (`{{TARGET_DOMAIN}}`) need a resolution step before execution; implement a resolver that pulls from the engagement manifest.
- Some SOPs may need conditional branching (if port 443 open, do HTTPS tests); this requires a more complex format than simple DAGs.
- Keep initial SOPs simple (<15 steps). Complex multi-phase engagements should be broken into separate linked SOPs.
