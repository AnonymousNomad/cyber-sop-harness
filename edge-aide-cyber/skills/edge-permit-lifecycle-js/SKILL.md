# Edge Permit Lifecycle

## What To Do
Implement one-use, TTL-bounded permits that authorize a single tool execution against a specific target by a specific operator. Permits are issued after policy approval and consumed atomically at execution time.

## Why
Even if the model or UI is compromised, it cannot chain actions because each permit is single-use. The TTL ensures stolen permits expire quickly. Binding to target+tool+operator prevents permit reuse across different contexts.

## Code Guidance
```javascript
import { createHash, randomBytes } from 'node:crypto';

export function createPermitIssuer(ttlMs = 30000) {
  const activePermits = new Map();

  return {
    issue(request, policyDecision) {
      if (policyDecision.decision !== 'ALLOW') {
        throw new Error(`cannot issue permit for decision: ${policyDecision.decision}`);
      }
      const id = randomBytes(16).toString('hex');
      const permit = {
        id,
        tool: request.tool,
        target: request.target,
        operator: request.operatorId,
        riskLevel: request.riskLevel,
        issuedAt: Date.now(),
        expiresAt: Date.now() + ttlMs,
        used: false,
      };
      activePermits.set(id, permit);
      return { ...permit, token: createHash('sha256').update(id + JSON.stringify(permit)).digest('hex') };
    },

    consume(permitId, toolName, target, operatorId) {
      const permit = activePermits.get(permitId);
      if (!permit) return { ok: false, reason: 'PERMIT_NOT_FOUND' };
      if (permit.used) return { ok: false, reason: 'PERMIT_ALREADY_USED' };
      if (Date.now() > permit.expiresAt) {
        activePermits.delete(permitId);
        return { ok: false, reason: 'PERMIT_EXPIRED' };
      }
      if (permit.tool !== toolName) return { ok: false, reason: 'TOOL_MISMATCH' };
      if (permit.target !== target) return { ok: false, reason: 'TARGET_MISMATCH' };
      if (permit.operator !== operatorId) return { ok: false, reason: 'OPERATOR_MISMATCH' };

      permit.used = true;
      activePermits.delete(permitId);
      return { ok: true, permit };
    },

    sweepExpired() {
      const now = Date.now();
      for (const [id, p] of activePermits) {
        if (now > p.expiresAt) activePermits.delete(id);
      }
    },

    get activeCount() { return activePermits.size; },
  };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Permit reuse | Chained unauthorized actions | `used` flag set synchronously before returning |
| Permit theft (XSS steals ID from WS message) | Attacker executes as operator | Short TTL (30s default); bind to operator identity |
| Race condition on consume | Double execution | Map.set/delete is atomic in single-threaded Node.js |
| Memory leak from unclaimed permits | RAM exhaustion on edge device | Periodic sweep every 60s |
| Timing side channel reveals permit validity | Information disclosure | Return same error structure regardless of failure reason |

## Dependencies
- Node.js built-in `crypto`

## Pitfalls & Bugs
- JavaScript's single-threaded event loop makes Map operations atomic, but if you later add Worker threads, this breaks.
- `Date.now()` uses wall clock which can jump backward on NTP sync; consider `performance.now()` for TTL calculations.
- Permit IDs in transit over WebSocket should be encrypted if non-loopback transport is ever added.
- The sweep timer should be cleaned up on shutdown to prevent the process from hanging.
