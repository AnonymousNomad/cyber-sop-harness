---
name: cyber-policy-expressiveness
description: Extends the Cyber SOP Harness policy engine beyond basic allow/deny into composable rules, conditional logic, rate-aware decisions, data-class constraints, and compliance-mapped policies. Use when basic scope matching is insufficient for operational or regulatory requirements.
---

# Cyber Policy Expressiveness

## What

Extend `PolicyEngine` to support composite conditions, per-argument validation, time-window sub-policies, credential-class restrictions, and evidence-format requirements — while keeping every decision deterministic and auditable.

## Why

Current policy evaluates: authorization match → capability exists → target in scope → risk class check. This handles "can this action run?" but not "should this action run given current context?" Real operations need conditional rules like "allow R2 actions only between 09:00–17:00 UTC", "block if resolved IP changed since last check", or "require approval if response body exceeds 1 MB".

## How

### 1. Composite Policy Conditions

Add a `PolicyCondition` abstraction that composes:

```csharp
public abstract record PolicyCondition
{
    public abstract PolicyDecision Evaluate(PolicyContext context);
}

public sealed record AllOf(params PolicyCondition[] Conditions) : PolicyCondition { ... }
public sealed record AnyOf(params PolicyCondition[] Conditions) : PolicyCondition { ... }
public sealed record NotOf(PolicyCondition Inner) : PolicyCondition { ... }
public sealed record TimeWindow(DateTimeOffset Start, DateTimeOffset End, string TimeZone) : PolicyCondition { ... }
public sealed record ResolvedAddressChanged(string PreviousAddress) : PolicyCondition { ... }
public sealed record ResponseSizeLimit(long MaxBytes) : PolicyCondition { ... }
public sealed record CredentialClassRequired(string RequiredClass) : PolicyCondition { ... }
```

### 2. Policy Context Enrichment

The current `PolicyEngine.Evaluate()` receives `(ActionRequest, AuthorizationManifest, ApprovalRecord?)`. Extend to:

```csharp
public sealed record PolicyContext(
    ActionRequest Request,
    AuthorizationManifest Manifest,
    ApprovalRecord? Approval,
    IReadOnlyDictionary<string, string> RuntimeState,
    DateTimeOffset EvaluatedAt,
    string? PreviousTargetHash,
    long? PreviousResponseSize)
```

This allows policies that reference execution history without breaking the existing interface (add an overload).

### 3. Policy Pack Format

Define a JSON schema for declarative policy packs:

```json
{
  "policyPack": "web-bounty-basic",
  "version": "1.0",
  "rules": [
    {
      "id": "business-hours-only",
      "condition": {
        "type": "timeWindow",
        "start": "09:00",
        "end": "17:00",
        "timezone": "UTC"
      },
      "effect": "requireApproval",
      "appliesTo": { "riskClasses": ["R2", "R3"] }
    },
    {
      "id": "dns-change-block",
      "condition": {
        "type": "resolvedAddressChanged",
        "comparison": "previous"
      },
      "effect": "block",
      "reason": "DNS resolution changed mid-engagement; possible rebinding"
    }
  ]
}
```

### 4. Compliance Evidence Mapping

Map policy decisions to compliance frameworks:

| Framework | Requirement | Policy Rule |
|---|---|---|
| SOC 2 CC6.1 | Logical access controls | Authorization manifest required |
| SOC 2 CC6.3 | Least privilege | Capability-level targeting |
| SOC 2 CC7.2 | Anomaly detection | DNS change block rule |
| FedRAMP AC-3 | Access enforcement | Policy engine allow/deny |
| FedRAMP AU-2 | Audit events | Every decision creates audit event |
| OWASP ASVS 4.1 | Access control | Scope evaluator + capability registry |

Each policy pack should declare which compliance controls it satisfies so evidence can be exported in the correct format.

## Threat Matrix

| Threat | Vector | Mitigation |
|---|---|---|
| Policy bypass via composition | Attacker finds logical gap between AllOf/AnyOf conditions | Property-based testing on condition combinations; default-deny on parse failure |
| Time-of-check/time-of-use | Condition evaluates true but state changes before dispatch | Re-evaluate all conditions inside broker immediately before adapter invocation |
| Policy injection | Malicious engagement manifest contains crafted condition JSON | Schema-validate with strict mode; reject unknown fields; bound nesting depth |
| Clock skew | TimeWindow uses local clock that differs from authoritative clock | Use `AuthoritativeClock.UtcNow` exclusively; never `DateTimeOffset.Now` |
| Compliance misrepresentation | Policy pack claims SOC 2 coverage it does not provide | Require independent review of compliance mappings before publication |

## Dependencies

- `CyberSopHarness.Core.PolicyEngine` — base evaluation logic
- `CyberSopHarness.Core.Clock` — AuthoritativeClock for deterministic timestamps
- `CyberSopHarness.Core.Capabilities` — CapabilityRegistry and CapabilityManifest
- `System.Text.Json` — policy pack parsing with source-generated serializers

## Pitfalls

- Making conditions async: breaks deterministic replay; keep evaluation synchronous
- Storing mutable state in conditions: makes same input produce different outputs across runs
- Not versioning policy packs: changing rules invalidates historical evidence interpretation
- Overloading `PolicyDecision` enum: adding new values breaks exhaustive switch statements downstream
- Not logging why composite conditions failed: operator sees "blocked" with no explanation
- Forgetting to evaluate conditions at both pre-flight AND dispatch time: race condition between check and use
- Using string comparison for enums: case-sensitivity bugs that only appear on Linux

## Debug Guide

If a policy blocks unexpectedly:
1. Check each condition independently with a simplified context
2. Log the full `PolicyContext` (minus secrets) when any condition fails
3. Verify `AuthoritativeClock.UtcNow` is within expected bounds
4. Compare `PolicyResult.Reason` against expected condition failure messages
5. Test the same policy pack with a minimal action request

## Acceptance Criteria

- All new policy conditions are deterministic (same inputs → same output)
- Every condition has positive and negative test cases
- Composite conditions (AllOf/AnyOf/NotOf) are property-tested for logical completeness
- Policy pack JSON schema rejects malformed input with clear error messages
- Compliance mappings are documented and reviewed by someone who understands the framework
