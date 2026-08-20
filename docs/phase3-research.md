# Phase 3 Research and Implementation Contract

Date: 2026-08-17
Status: local-fixture implementation accepted; live-provider gate remains open

## Scope

Phase 3 implements the portable runtime and evidence boundary: model/provider metadata, normalized action envelopes, typed tool capability registrations, host-controlled workflow state, append-only evidence events, raw/redacted artifacts, replay validation, independent verification, finding lifecycle, and report gating.

Phase 3 remains local-only. It does not contact live targets, install external security tools, use real credentials, enable unrestricted shell execution, or enable authorized dispatch. Deterministic fake model providers and fake tool adapters are the only execution fixtures.

## Research Controls

- Model output is an untrusted proposal and cannot authorize itself or set host state.
- Provider swaps must preserve policy decisions for identical action requests.
- Tool adapters must validate typed actions, declare capabilities, capture exact output, expose cleanup, and never silently retry dangerous work.
- Every result records the action, tool identity/version, policy decision, permit, timestamps, raw and redacted artifact references, hashes, observations, and cleanup result.
- Raw output is hashed as bytes. Redaction produces a separate artifact and hash; a summary is never used as the raw hash.
- State transitions are host-controlled and require their corresponding immutable event, permit, verifier, or report-policy evidence.
- Hash-chain failure freezes the run to `STOPPED`.
- Findings remain separate from execution state and cannot become `REPORTABLE` without independent verification and an allowed report-policy decision.
- Replay is labeled `REPLAYABLE`, `PARTIALLY_REPLAYABLE`, or `NON_REPLAYABLE` based on the availability and integrity of the request, tool, version, fixture, environment, and evidence.

## Implementation Decisions

- Reuse the Phase 2 policy, permit, capability, and canonicalization primitives; do not duplicate authorization logic.
- Add a typed Phase 3 runtime in the existing .NET 10 core with no new package dependency.
- Keep the tool registry frozen before dispatch and reject unknown capabilities before adapter invocation.
- Require a consumed Phase 2 permit and an `ALLOW` policy result before a tool adapter can run.
- Record blocked and failed adapter attempts as controlled evidence events; they cannot advance to successful observation.
- Use in-memory stores for this phase, with immutable snapshots and explicit serialization-ready records. Durable storage belongs to a later runtime/release phase.

## Phase 3 Threat Responses

| Threat | Required response |
|---|---|
| Provider/model substitution changes policy | Same action produces the same policy decision and hashes are recorded |
| Unknown or undeclared tool | Block before adapter invocation |
| Fabricated tool output | No result event, no state advancement |
| Missing event | Transition is rejected |
| Hash mismatch | Run freezes to `STOPPED` |
| Tool/provider failure | Controlled error/unknown result, never success |
| Tool timeout | `TIMEOUT` result, never successful observation |
| False interpretation | Finding remains `UNKNOWN` or `REJECTED` |
| Unverified finding in report | Report policy rejects it |
| Replay incompleteness | Replayability is downgraded and limitations are explicit |

## Phase 3 Gate

Phase 3 is complete only when unit, integration, failure-path, and adversarial tests demonstrate provider parity, typed tool blocking, normalized results, hash-linked evidence, state invariants, replay classification, independent fixture verification, finding transitions, and report exclusion. Authorized/live execution remains fail-closed until a later trusted provider/containment gate.
