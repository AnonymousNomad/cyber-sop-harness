# Implementation Roadmap

Status: mapped; Phase 1 complete; Phase 2 local-fixture gate passed; Phase 3B in progress
Date: 2026-08-19

## Skill Count

There are twenty-three skills. Governance, evaluation/release, terminal control-plane, command-desk UI/workflow, edge-model capacity, LFM2.5 fine-tune operations, and bounty workflow rules are reusable across phases.

| Phase | Skills |
|---|---|
| Global | `cyber-project-governance`, `cyber-evaluation-reporting-release` |
| Terminal runtime | `cyber-terminal-control-plane`, `cyber-edge-model-capacity`, `cyber-lfm25-finetune-operations`, `cyber-command-desk-parrot-foundation`, `cyber-command-desk-status-system`, `cyber-command-desk-workflow-views` |
| Phase 1: Foundation and architecture | `cyber-cross-platform-architecture` |
| Phase 2: Authorization and containment | `cyber-authorization-scope-policy`, `cyber-safe-execution-containment` |
| Phase 3: Portable runtime and evidence | `cyber-model-tool-interoperability`, `cyber-state-evidence-replay`, `cyber-local-model-runtime`, `cyber-model-provider-selection`, `cyber-model-packaging-supply-chain`, `cyber-durable-evidence-persistence`, `cyber-provenance-key-custody`, `cyber-secret-custody` |
| Phase 4: Methodology and SOP engine | `cyber-methodology-as-code`, `cyber-web-api-business-logic`, `cyber-bounty-sop-terminal-workflow` |
| Phase 5: Mobile and assurance | `cyber-mobile-control-plane` |

## Phase 1: Foundation and Architecture

Status: Complete for artifact and contract scope. No runtime security control or live target operation is claimed.

Deliverables:

- Project requirements and source lock
- APTS/WSTG/ASVS/NIST/CISA traceability matrix
- Threat model
- Trust-boundary diagram
- Cross-platform architecture decision
- Data contracts and initial state model
- Local fixture plan

Forbidden:

- Live target testing
- Real credentials
- Arbitrary tool execution
- Production deployment

Gate:

- Architecture is internally consistent.
- Every security requirement maps to a component and test.
- Mobile, gateway, policy, worker, and evidence boundaries are explicit.
- Phase 1 artifacts are reviewed independently.

## Phase 2: Authorization and Containment

Status: Local-fixture gate passed; authorized/live provider execution remains blocked.

Deliverables:

- Authorization manifest
- Scope evaluator
- Rules-of-engagement schema
- Risk classifications
- Approval records
- One-use action permits
- Credential handles
- Worker sandbox
- Rate limits
- Kill switch
- Cleanup and rollback

Gate:

- Out-of-scope actions never reach workers.
- Expired/modified permits fail.
- Sandbox setup failure blocks execution.
- Kill works without the model or phone.
- Secrets do not appear in logs or prompts.

## Phase 3: Portable Runtime and Evidence

Status: In progress; implementation restricted to deterministic local fixtures.

Deliverables:

- Provider adapter contract
- Tool capability manifest
- Typed action envelope
- Workflow state machine
- Append-only event chain
- Raw/redacted evidence artifacts
- Replay package
- Independent verifier
- Finding lifecycle

Gate:

- Model/provider swaps preserve policy behavior.
- Tool swaps preserve evidence semantics.
- Fabricated tool output cannot advance state.
- Hash mismatches freeze execution.
- Known lab findings reproduce independently.

### Phase 3B: Local Model Runtime and Provider Selection

Status: In progress; Q4_K_M real-model load and end-to-end synthetic integration pass; durable journaling, secret custody, key custody, and consent-gated external provider implemented; CLI wizard shell operational; Q5 blocked, redistribution approval pending.

Decision: Execute this as a Phase 3 workstream before Phase 4. It is not a mobile phase and it must not enable authorized/live execution.

Deliverables:

- Verified model/runtime manifest and license notices
- Pinned llama.cpp runtime discovery/start/health/identity/warmup/stop
- Optional local-model/user-model/external-API selection flow
- Offline-by-default provider policy and secret-safe API configuration
- Model file/runtime hash verification and resource preflight
- Local fake-model acceptance, then real local load only after artifact approval
- Durable append-only evidence journal with crash recovery and artifact hash verification
- DPAPI/Credential-Manager-gated secret custody with rotation and clearing
- Fingerprint-bound, role-separated signing keys with rotation and offline verification

Gate:

- WhiteRabbitNeo redistribution/derivative permission is explicitly resolved.
- Model/runtime hashes and licenses verify offline.
- Local server binds loopback, disables tools/agent/MCP, passes health/identity/warmup, and shuts down cleanly.
- Q3/Q4 resource/latency measurements pass on the target hardware.
- Provider swaps preserve policy decisions and no API key/prompt/target data leaks.
- No model output bypasses typed policy, permits, tool broker, evidence, or provenance.

## Phase 4: Methodology and SOP Engine

Deliverables:

- Versioned methodology registry
- Procedure compiler
- Coverage ledger
- Web/API bug-bounty profile
- Business-logic workflow model
- Attack-graph evidence model
- Reporting profile

Gate:

- Invalid procedures fail compilation.
- Every procedure has an oracle and evidence requirements.
- Local fixtures cover true positives and false positives.
- Business-logic coverage reports unknown context honestly.
- No procedure authorizes an action outside policy.

## Phase 5: Mobile and Assurance

Deliverables:

- iPhone control client
- Android control client
- Device enrollment/revocation
- Action-bound approvals
- Evidence viewing
- Reporting/disclosure flow
- APTS conformance evidence
- Portability tests
- Security regression suite
- Release manifest

Gate:

- Approval replay fails.
- Revoked devices cannot approve.
- Lost mobile connectivity does not disable desktop safety.
- Reports contain only verified evidence.
- Model/tool/provider changes trigger regression tests.
- Release package includes `agent_notes.Md`.

## Phase Transition Rule

Do not begin a later phase when any gate is failed, skipped, or supported only by a model claim. Fix the root cause, rerun the verification battery, and append the result to `agent_notes.Md`.

## Realistic Timeline to Production Gate

Based on current progress and collaborator feedback:

| Phase | Estimated Duration | Dependencies |
|---|---|---|
| Phase 3 completion (methodology engine + more adapters) | 2-3 weeks | None; in progress |
| Phase 4 (mobile control + live target containment) | 4-6 weeks | Phase 3 gate |
| Independent verification + threat-model review | 2-3 weeks | Phase 4 gate |
| **Total to production gate** | **8-12 weeks** | Assuming no blockers |

Key assumptions:
- No blockers from redistribution approval (currently pending)
- Live-target testing requires authorized engagement (not scheduled)
- Mobile control plane is UI work, not security-critical
- Threat-model review is external, not self-assessed

The gap between "development preview" and "production gate" is real and significant. The 44 tests prove contracts work under deterministic conditions. Live-target testing will expose hallucination-induced payloads, malformed responses, and timing issues that fixtures cannot simulate.
