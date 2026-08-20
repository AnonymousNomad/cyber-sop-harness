# Phase 1 Data Contract Rules

Status: planning contract
Date: 2026-08-17

## Canonical Request Hash

`action_hash` is SHA-256 over the UTF-8 bytes of the canonical action request.

Canonicalization rules:

1. Parse a valid `action-request.schema.json` object.
2. Remove transport-only fields that are not part of the requested action, including approval and permit references.
3. Serialize JSON with lexicographically sorted object keys, no insignificant whitespace, UTF-8 encoding, and stable numeric representation.
4. Preserve array order because array order may affect the requested operation.
5. Require opaque references for credentials, raw target values, and sensitive artifacts.
6. Store the canonicalization rule identifier as `canonical-action-json-v1`.
7. Compute lowercase hexadecimal SHA-256.

The permit's `action_hash` must equal the hash of the action request presented to policy. A worker must reject a permit when the recomputed hash differs.

## Permit Binding

A valid permit binds:

- Engagement/run
- Action request
- Canonical action hash
- Target reference
- Scope reference and scope hash
- Authorization reference
- Policy reference and policy version
- Capability reference
- Methodology references
- Risk class
- Approval reference
- Worker identity
- Issuer identity and signature
- Expiry
- Nonce
- One-use consumption state

The worker must atomically transition a permit from `UNUSED` to `CONSUMED` before executing. A consumed, revoked, or expired permit cannot be replayed.

## Result Binding

A result event references the action request, permit, authorization, scope, capability, policy, worker, tool version, artifacts, and event-chain hashes.

Successful or partial execution requires:

- `policy_decision: ALLOW`
- A non-null permit reference
- A valid action hash relationship in the permit
- A raw artifact reference
- A redacted artifact reference
- At least one observation reference
- A cleanup result

Blocked results require `policy_decision: BLOCK` and must not be interpreted as observations.

## Cross-Record Validation

JSON Schema validates each record independently. The policy/runtime layer must enforce cross-record equality and lifecycle invariants:

1. Permit `action_hash` equals the canonical hash of the referenced action request.
2. Permit authorization, scope, scope hash, policy, policy version, capability, risk, and methodology references equal the corresponding action/policy values.
3. Permit `worker_ref` equals the worker that presents it.
4. Result `action_request_ref`, `permit_ref`, `worker_ref`, authorization, scope, capability, and policy references resolve to existing records in the same run.
5. Result `policy_decision: ALLOW` requires an unexpired `UNUSED` permit, atomically consumed before execution.
6. Result `status: BLOCKED` cannot contain observation references or a consumed permit.
7. R3/R4 records require an approval reference that resolves to the action and risk class.
8. A consumed, revoked, or expired permit cannot be replayed.

These are runtime invariants, not claims that standalone JSON Schema can compare records. Phase 3 must implement and test them with cross-record contract tests.

## Sensitive Data

Raw artifacts are stored by opaque reference. Model-visible artifacts are redacted derivatives. The model must never receive secrets merely because a raw artifact exists.
