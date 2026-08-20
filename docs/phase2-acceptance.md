# Phase 2 Acceptance Matrix

Status: local-fixture acceptance passed; independent review complete; authorized/live release blocked
Date: 2026-08-17

| Test | Behavior | Evidence | Result |
|---|---|---|---|
| P2-001 | Signed valid manifests validate; invalid signatures fail. | `manifest signature and validation` | PASS |
| P2-002 | Exact scope, wildcard, CIDR, deny-list, resolved-address, hard-deny, mapped-address, and redirect rules behave correctly. | `scope and redirect decisions` | PASS |
| P2-003 | Capability registration, malformed-action rejection, authorization binding, risk classification, out-of-scope blocking, R3 approvals, and R4 denial behave correctly. | `policy risk and approval decisions` | PASS |
| P2-004 | Permits are signed, action-bound, single-use, expiry-aware, and replay-resistant. | `permit signature, consumption, expiry, and replay` | PASS |
| P2-005 | Credentials are encrypted behind handles and revocable without exposing plaintext through vault metadata. | `credential vault encryption and revocation` | PASS |
| P2-006 | Per-target and global request/concurrency limits deny excess actions without dispatch. | `rate limiting and concurrency` | PASS |
| P2-007 | Signed fixture attestations are rejected for authorized mode; uncontained workers are rejected; valid fixture workers require consumed permits. | `worker containment and permit enforcement` | PASS |
| P2-008 | Relay-loss/direct-stop paths latch permit issuance, revoke permits, attempt graceful and forced stop, and run cleanup. | `stop all active workers` | PASS |
| P2-009 | Rollback actions execute in reverse order and are idempotent. | `rollback order and idempotence` | PASS |
| P2-010 | Windows Job Object creation, configured limits, suspended assignment, and explicit termination work on this host. | `Windows Job Object setup and termination` | PASS |

## Commands

```text
dotnet build tests\Phase2.Tests\Phase2.Tests.csproj --configuration Release -m:1
dotnet run --project tests\Phase2.Tests\Phase2.Tests.csproj --configuration Release --no-build
# If project evaluation hits a host-level MSBuild failure:
dotnet tests\Phase2.Tests\bin\Release\net10.0\Phase2.Tests.dll
```

Observed test output ended with:

```text
phase2_tests=passed count=10
```

## Scope Limitations

This phase uses local fixture workers and a bounded Windows Job Object utility. `WorkerSupervisor` intentionally blocks authorized/live dispatch because no trusted provider adapter currently binds a worker identity to an external containment boundary. It does not contact live targets, install external security tools, use real credentials, validate cloud-provider isolation, validate Linux runtime containment, or prove mobile security.

## Independent Review

Final independent review: no blockers for the documented local-fixture boundary. Authorized/live release remains blocked because in-process fixture handlers do not provide a hard-stop guarantee and no trusted external provider adapter is registered. The Job Object utility is tested on Windows for setup and termination, but descendant, resource-limit, non-Windows, and egress enforcement remain unverified.
