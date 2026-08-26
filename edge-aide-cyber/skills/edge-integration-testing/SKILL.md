# Edge Integration Testing

## What To Do
Write end-to-end integration tests that verify the full governance pipeline: command -> policy -> permit -> tool -> sanitize -> evidence -> UI update.

## Why
Unit tests verify modules in isolation. Integration tests verify the security guarantees hold when modules interact. A policy engine that passes unit tests but fails integration is a false sense of security.

## Code Guidance
```javascript
// tests/integration-governance.test.mjs
describe('governance integration', () => {
  it('blocks tool action without engagement', async () => {
    // Start server without engagement.json
    // Send /tool dns.reverse example.com
    // Verify DENY response + evidence record
  });

  it('allows tool action with valid engagement and permit', async () => {
    // Load engagement.json
    // Send /tool dns.reverse target.com
    // Verify permit issued, tool executed, evidence recorded
  });
});
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Integration tests require network | CI flakiness | Use mock adapters for CI |
| Tests leak real target data | Privacy breach | Use localhost/fixture targets only |

## Dependencies
- All source modules, node:test runner

## Pitfalls
- Tests should be deterministic — no network, no timing
- Mock external tools (nmap, nuclei) in CI