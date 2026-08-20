# Local Fixture Plan

Status: Phase 1 planning

## Purpose

Provide deterministic, owned test environments for policy, worker, evidence, replay, and web/API procedure tests without contacting external targets.

## Fixture Classes

| Fixture | Purpose | Required behavior |
|---|---|---|
| Scope fixture | Target and redirect policy | Includes allowed, excluded, ambiguous, third-party, and redirect destinations |
| Tool fixture | Adapter contract | Emits success, timeout, partial, malformed, and nonzero results |
| Evidence fixture | Hash and redaction | Contains safe synthetic tokens and deterministic output |
| Injection fixture | Untrusted content handling | Returns instruction-like content that must never become authority |
| Finding fixture | Verification lifecycle | Contains reproducible, false-positive, and non-reproducible candidates |
| Workflow fixture | Business logic | Models roles, states, valid transitions, reordered steps, replay, and concurrency cases |
| Mobile fixture | Approval security | Tests expiry, replay, revocation, clock skew, duplicate approval, and disconnect |

## Data Rules

- Use synthetic identities, tokens, and records.
- Never use production credentials.
- Never use real customer data.
- Keep fixture targets local or isolated.
- Record fixture version and hash in test evidence.
- Destroy temporary worker state after each test.

## Phase 1 Limitation

Phase 1 defines fixtures but does not implement the worker, policy engine, or test harness. Implementation begins only after the Phase 1 directive is issued and the acceptance plan is approved.
