# Phase 1 Acceptance Plan

Phase 1 is a documentation, contract, and architecture phase.

## Required Checks

1. `P1-001`: All required files exist.
2. `P1-002`: Every `SKILL.md` has valid YAML frontmatter with `name` and `description`.
3. `P1-003`: The engagement manifest schema includes APTS-SE-001 authority, scope, time, method, criticality, data, escalation, credential, rate, and cleanup fields.
4. `P1-004`: The action contract and policy design represent target, scope, permit, risk, and approval relationships; the permit schema is valid.
5. `P1-005`: The model cannot authorize actions or set host state.
6. `P1-006`: Capability manifests are required for tools.
7. `P1-007`: Containment, egress, resource, kill, and relay-failure requirements are documented.
8. `P1-008`: Result-event schema contains policy, artifact, hash, timestamp, tool, target, chain, and cleanup fields.
9. `P1-009`: Every requirement maps to an exact source reference, component, and test reference.
10. `P1-010`: Threat-model assets, boundaries, threats, controls, and residual risks are present.
11. `P1-011`: Architecture decision and state model record all trust boundaries and invariants.
12. `P1-012`: Local fixture plan contains no real credentials or target data.
13. `P1-013`: `agent_notes.Md` records setup, failures, fixes, acceptance, and independent review.
14. `P1-014`: Standards lock contains versioned sources, reference patterns, exact APTS refs, and explicit content-hash status.
15. `P1-015`: State model contains execution states, finding states, finding transitions, and policy/verification invariants.
16. `P1-016`: Data-contract document defines cross-record equality, permit consumption, result conditions, and runtime validation boundaries.

## Forbidden During Phase 1

- Live target network requests
- Real credentials
- Security-tool execution
- Production deployment
- Downloading target data
- Claiming APTS conformance

## Acceptance Evidence

This file records the acceptance procedure. The observed pass/fail result is recorded in `docs/acceptance-matrix.md` and `agent_notes.Md` after execution.
