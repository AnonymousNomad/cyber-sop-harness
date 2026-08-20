# Phase 2 Research and Implementation Contract

Date: 2026-08-17
Status: approved for implementation

## Scope

Phase 2 implements authorization, scope enforcement, rules-of-engagement validation, risk decisions, approvals, one-use permits, credential handles, rate limiting, worker containment, kill behavior, relay-loss behavior, cleanup, and rollback contracts.

Phase 2 does not contact live targets, install third-party security tools, use real credentials, or enable unrestricted shell execution.

## Exact Source Controls

### Scope and authorization

- APTS-SE-001: machine-parseable RoE and pre-test validation
- APTS-SE-002: IP/CIDR and reserved-space awareness
- APTS-SE-003: domain, wildcard, ownership, and third-party handling
- APTS-SE-004: temporal boundary and timezone handling
- APTS-SE-005: asset criticality classification
- APTS-SE-006: pre-action scope validation
- APTS-SE-009: hard deny lists and critical asset protection
- APTS-SE-012: DNS rebinding prevention
- APTS-SE-015: scope-decision auditability
- APTS-SE-019: rate limiting and production impact controls
- APTS-SE-023: credential and secret lifecycle governance

### Safety and containment

- APTS-SC-001: impact/CIA classification
- APTS-SC-004: rate, bandwidth, and payload constraints
- APTS-SC-006: automated-to-approval-to-prohibited escalation
- APTS-SC-009: independent kill switch
- APTS-SC-010: health monitoring and halt behavior
- APTS-SC-011: condition-based termination
- APTS-SC-012: network circuit breaker
- APTS-SC-014: reversible-action tracking and rollback
- APTS-SC-015: post-test integrity validation
- APTS-SC-016: evidence preservation and cleanup
- APTS-SC-017: external watchdog
- APTS-SC-018: incident containment and recovery
- APTS-SC-019: execution sandbox integrity
- APTS-SC-020: external action allowlist

### Human and manipulation controls

- APTS-HO-001: approval gates
- APTS-HO-003: timeout and default-safe behavior
- APTS-HO-006: graceful pause and state preservation
- APTS-HO-008: kill and state dump
- APTS-HO-010: human decision before irreversible actions
- APTS-HO-012: impact threshold escalation
- APTS-HO-013: confidence-based escalation
- APTS-HO-014: legal/compliance escalation
- APTS-MR-001: instruction boundary
- APTS-MR-007: redirect policy
- APTS-MR-008: DNS/network redirect prevention
- APTS-MR-009: SSRF prevention in testing
- APTS-MR-011: out-of-band communication prevention
- APTS-MR-012: immutable scope architecture
- APTS-MR-018: model input/output boundary
- APTS-MR-019: discovered credential protection
- APTS-MR-023: agent runtime treated as untrusted

## Platform Sources

- CISA VDP Template: explicit authorization, scope, prohibited methods, stopping on sensitive data, and third-party permission.
- NIST SP 800-207: no implicit trust based on network location.
- Windows Job Objects: process-tree management, limits, and termination.
- Windows Sandbox: disposable hypervisor-isolated execution; networking is disabled for untrusted fixture work.
- Linux seccomp: system-call surface reduction, not a complete sandbox by itself.
- Linux Landlock: unprivileged filesystem/network/IPC restriction layer.

## Implementation Decision

Use .NET 10 and only platform/runtime libraries in Phase 2 because `dotnet --version` returned `10.0.300`, while Python, Rust, Cargo, and Docker were unavailable on the host. Keep the policy core independent from UI and provider code.

The worker interface is capability-based. Phase 2 exposes a local fixture worker and tests a bounded Windows Job Object provider utility. The supervisor intentionally refuses authorized dispatch until that provider is connected through a trusted adapter with process identity and network-boundary evidence. No arbitrary shell adapter is registered.

## Phase 2 Threat Responses

| Threat | Required response |
|---|---|
| Missing/invalid authority | Reject before permit issuance |
| Out-of-scope target/redirect | Block before worker invocation |
| R3 action without approval | Reject before permit issuance |
| R4 action | Deny by default |
| Expired/modified permit | Reject and audit |
| Permit replay | Reject after atomic consumption |
| Credential exposure | Store encrypted handle; never return to model |
| Rate exhaustion | Deny or back off without sending action |
| Relay loss | Revoke permits, stop all workers, preserve evidence, require fresh authorization |
| Worker/sandbox failure | Fail closed; no fallback to uncontained execution |

## Phase 2 Gate

Phase 2 is complete only when unit, integration, failure-path, and adversarial tests demonstrate these responses using local fixtures and fake workers. Passing schemas alone is insufficient. Authorized/live dispatch remains fail-closed until the trusted provider adapter gate is implemented in a later phase.
