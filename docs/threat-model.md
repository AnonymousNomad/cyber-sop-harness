# Phase 1 Threat Model

Status: architecture baseline
Date: 2026-08-17

## System Boundary

The system includes the mobile control client, desktop gateway, model/provider adapter, policy engine, tool broker, sandboxed worker, evidence store, methodology registry, verifier, and reporting pipeline.

The system does not trust the target, target responses, third-party providers, model output, tool output, mobile network, or unpinned dependencies.

## Assets

| Asset | Security property | Primary protection |
|---|---|---|
| Authorization manifest | Integrity and authenticity | Signed/versioned manifest; external policy validation |
| Scope and ROE | Integrity and confidentiality | Immutable policy version; action-bound checks |
| Provider credentials | Confidentiality and least privilege | Gateway-only secret handles; short expiry |
| Tool worker | Isolation and availability | Sandbox, resource limits, egress policy, kill path |
| Raw evidence | Integrity and confidentiality | Encrypted store, hashes, access control |
| Audit chain | Integrity and availability | Append-only storage and independent verification |
| Model context | Confidentiality and instruction integrity | Redaction and trusted/untrusted data separation |
| Mobile approval identity | Authenticity and revocation | Device keys, step-up auth, expiry, nonce |
| Methodology procedures | Integrity and provenance | Version locks, review, procedure schema |
| Finding records | Accuracy and provenance | Independent verifier and evidence references |

## Threats and Planned Controls

| ID | Threat | Failure effect | Planned control | Residual risk |
|---|---|---|---|---|
| THR-001 | Operator supplies incomplete or invalid authority | Unauthorized target interaction | Required authorization manifest and fail-closed policy | Legal interpretation still requires human owner/legal review |
| THR-002 | Model invents a tool call or result | False finding or unsafe follow-on action | Typed action envelope, result events, hash acknowledgement, state machine | A matching hash does not prove semantic interpretation |
| THR-003 | Target page or tool output contains indirect prompt injection | Model changes objective or requests unsafe action | Untrusted-data labeling, external policy, no authority from content | Novel injection may evade model-side detectors; host gate remains required |
| THR-004 | Redirect or shared infrastructure leaves scope | Third-party impact or legal exposure | Pre-action canonical target and redirect checks | Ownership ambiguity requires human escalation |
| THR-005 | Tool adapter has undeclared side effect | State change, data access, or escape | Capability manifest, worker containment, adapter tests | Tool bugs require defense in depth and VM isolation |
| THR-006 | Worker process escapes limits | Host compromise or uncontrolled testing | Job Objects/VMs, namespaces, seccomp, Landlock, cgroups | Host kernel/VM vulnerabilities remain residual risk |
| THR-007 | Mobile device is lost, compromised, or replays approval | Unauthorized action approval | Device enrollment, action-bound signatures, expiry, revocation | Compromised operator identity still requires operational response |
| THR-008 | Provider receives sensitive engagement data | Confidentiality or regulatory exposure | Gateway routing, redaction, provider data policy, local-model option | Redaction errors require testing and review |
| THR-009 | Evidence is edited or selectively omitted | Untrustworthy report | Append-only events, hashes, independent verifier | Storage compromise requires external backup and access controls |
| THR-010 | Engagement state leaks across runs | Cross-tenant or cross-customer disclosure | Run namespaces, credential isolation, context isolation | Shared host vulnerabilities remain residual risk |
| THR-011 | Dependency or model supply chain is poisoned | Unsafe or deceptive execution | Pinned versions, provenance, SBOM, review, regression tests | New supply-chain attacks require continuous monitoring |
| THR-012 | Business workflow is misunderstood | Missed or false business-logic finding | Human/context-supplied workflow model and explicit unknown coverage | Human domain knowledge remains necessary |

## Trust Boundaries

1. Mobile client to gateway: authenticated device and operator boundary.
2. Gateway to policy engine: authorization and permit boundary.
3. Model to policy engine: untrusted proposal boundary.
4. Target content to model: untrusted data boundary.
5. Broker to worker: capability and sandbox boundary.
6. Worker to evidence store: append-only evidence boundary.
7. Provider to engagement: data-residency and disclosure boundary.
8. Engagement to engagement: isolation boundary.

## Exclusions

Phase 1 does not evaluate real target availability, exploit success, production safety, provider security, mobile malware resistance, or host kernel escape resistance. Those require later controlled tests and are not represented as completed.
