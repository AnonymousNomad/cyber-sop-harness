# Phase 1 Risk Register

Status: active
Date: 2026-08-17

| ID | Risk | Likelihood | Impact | Owner | Treatment | Status |
|---|---|---:|---:|---|---|---|
| RISK-001 | Source snapshots and content hashes are not yet stored. | Medium | High | Project owner | Capture immutable source snapshots and hashes before release/conformance claims. | Open |
| RISK-002 | Authorization language may be interpreted differently across jurisdictions. | Medium | Critical | Human owner/legal reviewer | Require engagement-specific authorization and legal review; never infer authority. | Open |
| RISK-003 | Model interpretation can be wrong even when result hashes match. | High | High | Runtime/verifier owner | Independent verifier and finding state separation. | Mitigated in design; untested |
| RISK-004 | Target content can contain indirect prompt injection. | High | High | Policy/runtime owner | Untrusted-data boundary, external action gate, adversarial fixtures. | Mitigated in design; untested |
| RISK-005 | Tool adapter can have undeclared side effects. | Medium | Critical | Worker/broker owner | Typed capability manifests, sandbox, permit, adapter tests. | Mitigated in design; untested |
| RISK-006 | Mobile device compromise or approval replay. | Medium | High | Mobile owner | Device enrollment, action binding, expiry, revocation, PC-side safety. | Planned |
| RISK-007 | Relay outage or network partition leaves stale work. | Medium | High | Gateway owner | Deny new remote approvals, revoke relay-dependent permits, stop every active worker, preserve evidence, discard queued work, and require fresh permits. | Designed; untested |
| RISK-008 | Cross-engagement data leakage. | Low | Critical | Evidence/runtime owner | Per-run namespaces, credential isolation, provider routing, redaction. | Planned |
| RISK-009 | Business-logic workflow is incomplete or wrong. | High | Medium | Methodology owner | Human/context-supplied workflow model and explicit unknown coverage. | Planned |
| RISK-010 | Dependency/model update changes behavior. | Medium | High | Release owner | Version pinning, SBOM, regression suite, update audit. | Planned |
| RISK-011 | Host or VM escape compromises execution. | Low | Critical | Worker owner | VM isolation for high-risk work, host hardening, independent kill, later adversarial tests. | Unverified |
| RISK-012 | Audit record is incomplete or exposes sensitive data. | Medium | High | Audit owner | Append-only record, redaction, hashes, release inclusion gate. | Mitigated in design; untested |

## Risk Acceptance

No risk in this register is accepted for live-target operation during Phase 1. Open risks require a documented owner and a later verification or mitigation event.
