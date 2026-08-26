# Edge SOP Methodology Compiler

## What To Do
Define a JSON-based SOP (Standard Operating Procedure) format that represents security testing methodologies as directed acyclic graphs (DAGs). Each node is a step with tool, parameters, preconditions, and approval gates. Compile and validate SOPs at startup. Detect cycles, missing dependencies, and invalid tool references.

## Why
Bug bounty work follows repeatable methodologies. Encoding them as executable graphs enables: automated coverage tracking, dependency-aware step ordering, approval gates for dangerous operations, and replay of past engagements.

## SOP Format
```json
{
  "id": "recon-basic",
  "name": "Basic Reconnaissance",
  "steps": [
    {
      "id": "dns-resolve",
      "name": "DNS Resolution",
      "tool": "dns.reverse",
      "riskLevel": "R1",
      "params": { "target": "{{TARGET_DOMAIN}}" },
      "dependsOn": [],
      "approvalRequired": false
    },
    {
      "id": "port-scan",
      "name": "Port Scan",
      "tool": "nmap.scan",
      "riskLevel": "R2",
      "params": { "target": "{{TARGET_IP}}", "topPorts": 1000 },
      "dependsOn": ["dns-resolve"],
      "approvalRequired": true
    }
  ]
}
```

## Code Guidance
- Validate: all `dependsOn` references exist, no cycles via DFS, tool names match registry
- Template variables (`{{VAR}}`) resolved from engagement manifest at execution time
- Topological sort produces execution order
- `approvalRequired: true` steps pause for operator confirmation

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Cycle in SOP graph | Infinite execution loop | DFS cycle detection at compile time |
| Missing tool reference | Runtime crash | Validate tool names against adapter registry |
| SOP modified mid-engagement | Inconsistent execution | Lock SOPs during active engagements |
| Template variable not resolved | Tool receives literal `{{VAR}}` | Resolve all variables before dispatch |

## Dependencies
- Tool adapter registry (for validation)
- Engagement manifest (for variable resolution)

## Pitfalls
- Keep initial SOPs to ≤15 nodes for usability
- Conditional branching (if port 443 open → do HTTPS tests) requires a more complex format; defer to v2
- SOP definitions should be immutable during active engagements
