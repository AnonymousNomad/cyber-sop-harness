# Phase 3 Acceptance Matrix

Status: local-fixture acceptance passed; independent review complete; live-provider release blocked
Date: 2026-08-17

| Test | Required behavior | Evidence | Result |
|---|---|---|---|
| P3-001 | Two model/provider fixtures produce equivalent normalized actions and identical policy decisions. | Provider parity test | PASS |
| P3-002 | Provider metadata records model, version, configuration, retention, tool mode, latency, token usage, and failure class. | Provider metadata test | PASS |
| P3-003 | Frozen tool registry rejects unknown capabilities and untrusted adapter markers before invocation. | Registry and unknown-tool tests | PASS |
| P3-004 | Tool results are normalized and raw/redacted byte artifacts receive independent SHA-256 hashes. | Tool broker/evidence test | PASS |
| P3-005 | Blocked policy, missing/expired/replayed permits, malformed provider envelopes, mutable registries, and adapter failures cannot produce successful observation. | Failure-path and adversarial tests | PASS |
| P3-006 | Missing or mismatched evidence events prevent workflow transitions; host alone controls state. | State invariant and cross-run tests | PASS |
| P3-007 | Tampered event or artifact hash freezes the run to `STOPPED`. | Integrity test | PASS |
| P3-008 | Replay classification uses frozen content-hash identities and independent fixture verification reproduces the known fixture. | Replay/verifier test | PASS |
| P3-009 | Finding lifecycle binds action/evidence/verifier/report identifiers and report policy excludes unverified findings. | Finding/report test | PASS |
| P3-010 | Existing Phase 2 suite remains green and no live target/tool/credential path is invoked. | Regression and safety test | PASS |

## Commands

```text
dotnet build tests\Phase2.Tests\Phase2.Tests.csproj --configuration Release -m:1
dotnet build tests\Phase3.Tests\Phase3.Tests.csproj --configuration Release -m:1
dotnet "tests\Phase2.Tests\bin\Release\net10.0\Phase2.Tests.dll"
dotnet "tests\Phase3.Tests\bin\Release\net10.0\Phase3.Tests.dll"
```

## Explicit Non-Goals

- No live targets or real customer data.
- No real credentials.
- No external security tools or unrestricted shell adapter.
- No mobile control client.
- No claim of trusted authorized worker/provider integration.

## Observed Results

- Phase 2 regression: `phase2_tests=passed count=10`.
- Phase 3 fixture suite: `phase3_tests=passed count=8`.
- Release builds: `0 Warning(s)`, `0 Error(s)`.

## Independent Review

No blocker or high finding remains for the exact deterministic fixture path. Live-provider release remains blocked by the absence of trusted provider identity, OS-level containment/hard stop, egress enforcement, durable evidence, and independently protected cleanup/replay authority.
