# Edge OpSec Workflow Integration

## What To Do
Integrate OpSec controls into every tool adapter and command. The operator should never need to manually manage anonymity — it should be automatic and enforced.

## Why
Manual OpSec steps get skipped under pressure. Automatic integration ensures every tool call is routed through Tor/VPN without operator intervention.

## Code Guidance
```javascript
// Wrap all adapters with OpSec layer
function wrapWithOpSec(adapter, opsecManager) {
  return {
    ...adapter,
    async execute(params) {
      await opsecManager.shapedDelay();
      const fetchOpts = opsecManager.getFetchOptions();
      return adapter.execute({ ...params, _fetchOpts: fetchOpts });
    },
  };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Operator disables OpSec manually | IP exposed | Require confirmation + evidence record |
| OpSec layer adds too much latency | Operator bypasses it | Configurable per-operation levels |

## Dependencies
- OpSecManager module, all tool adapters

## Pitfalls
- Some CLI tools bypass Node.js agent layer — need proxychains wrapper
- Tor adds 1-3s latency per request — factor into timeouts