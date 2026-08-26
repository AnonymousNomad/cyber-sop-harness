# Edge Policy Engine (JavaScript)

## What To Do
Port the Cyber SOP Harness policy engine to pure JavaScript. Evaluate action requests against the engagement manifest: check scope, risk level, operator authorization, tool capability, and time validity. Return ALLOW, DENY, or APPROVAL_REQUIRED with structured reasons.

## Why
This is the core safety layer. The model proposes actions; this engine independently decides whether they're authorized. No code path may execute a tool without passing through here first. Fail-closed means any error results in denial.

## Code Guidance
```javascript
export function createPolicyEngine(manifest) {
  if (!manifest || typeof manifest !== 'object') {
    throw new Error('engagement manifest required');
  }

  return {
    evaluate(actionRequest) {
      try {
        return evaluateStrict(actionRequest);
      } catch (err) {
        return { decision: 'DENY', reason: 'POLICY_ERROR', detail: err.message };
      }
    }
  };

  function evaluateStrict(req) {
    // 1. Manifest must be valid and not expired
    const now = Date.now();
    if (new Date(manifest.expiresAt) < now) {
      return { decision: 'DENY', reason: 'MANIFEST_EXPIRED' };
    }

    // 2. Target must be in scope
    const scopeCheck = isTargetInScope(req.target);
    if (!scopeCheck.allowed) {
      return { decision: 'DENY', reason: 'OUT_OF_SCOPE', detail: scopeCheck.reason };
    }

    // 3. Risk level must be authorized
    if (!manifest.authorizedRiskLevels.includes(req.riskLevel)) {
      return { decision: req.riskLevel === 'R3' ? 'APPROVAL_REQUIRED' : 'DENY',
               reason: 'RISK_LEVEL_UNAUTHORIZED' };
    }

    // 4. Tool must be in allowed capabilities
    if (!manifest.allowedCapabilities.includes(req.tool)) {
      return { decision: 'DENY', reason: 'CAPABILITY_NOT_AUTHORIZED', detail: req.tool };
    }

    return { decision: 'ALLOW', permitRequired: true };
  }

  function isTargetInScope(target) {
    for (const rule of manifest.scope) {
      if (rule.type === 'cidr' && cidrMatch(target, rule.value)) return { allowed: true };
      if (rule.type === 'domain' && domainMatch(target, rule.value)) return { allowed: true };
      if (rule.type === 'url_prefix' && target.startsWith(rule.value)) return { allowed: true };
    }
    return { allowed: false, reason: `no scope rule matches ${target}` };
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Policy engine bypassed via direct tool call | Unauthorized execution | Tool adapters independently verify permit |
| Scope evaluation logic error | Out-of-scope target accessed | Write extensive unit tests for CIDR/domain matching |
| Manifest tampering after load | Elevated privileges | Store manifest hash; verify before each policy check |
| Race condition between check and execution | TOCTOU bypass | Permit issuance and consumption are atomic operations |
| Error swallowed silently | Unsafe default behavior | All exceptions produce DENY; never propagate upward |

## Dependencies
- zod (^3.x) for manifest schema validation

## Pitfalls & Bugs
- CIDR matching requires bit manipulation of IP addresses; JS bitwise ops work on 32-bit integers only. For IPv6, use BigInt or a library.
- Domain wildcards (`*.example.com`) should NOT match `example.com` itself unless explicitly listed.
- URL prefix matching can be fooled by path traversal (`/api/../../admin`). Normalize URLs before comparison.
- Manifest expiry should use UTC timestamps; local timezone differences on mobile devices can cause premature expiry.
- The engine must never throw — always catch and return DENY.
