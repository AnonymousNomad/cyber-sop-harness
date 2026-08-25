# Project Audit Record

Project: Cyber SOP Harness
Audit file: `agent_notes.Md`
Created: 2026-08-17
Status: Phase 2 in progress
Release requirement: This file must be packaged with every release.

## Audit Purpose

This file is the complete, append-only audit record for the project. It records research, sources, decisions, assumptions, rejected alternatives, files, skills, phase gates, commands, verification evidence, failures, fixes, approvals, and release contents from project start through final release.

It is not a private reasoning transcript. It must contain auditable facts, concise decision rationale, evidence references, and security-relevant events without exposing secrets or sensitive target data.

## Operating Rules

1. Append entries; do not rewrite historical entries.
2. Every entry has a date, event type, actor, intent, result, evidence, risks, and next action.
3. Record observed results separately from expectations.
4. Record failed and blocked work, not only successful work.
5. Never store API keys, passwords, session tokens, personal data, or raw customer data here.
6. Store sensitive evidence by encrypted reference and hash, never inline.
7. Every phase completion must cite its verification commands and observed outputs.
8. Every release manifest must include this file and its hash.
9. If this file is missing, malformed, or inconsistent with the release manifest, release is blocked.

## Initial Project State

The project was created at `E:\cyber-sop-harness` after the user explicitly authorized creation of the project directory and required audit file.

At initialization:

- No application implementation exists.
- No security tool has been executed by this project.
- No live target has been contacted.
- No target credentials are stored.
- No target data is stored.
- Phase 1 has not started.
- Research and planning artifacts are present.
- Ten governing skills are present under `skills/`.

## Decision Log

### AUDIT-0001: Project audit required

Date: 2026-08-17
Event: User required a complete transparency audit packaged with releases.
Decision: Use this file as the append-only project audit and release artifact.
Reason: A cybersecurity tool must expose how it was researched, designed, built, tested, constrained, and released.
Evidence: User instruction in the project conversation.
Risk: Audit omissions would undermine transparency and release trust.
Next: Append every project event and include this file in release manifests.

### RESEARCH-0001: Product thesis refined

Date: 2026-08-17
Event: Market and standards research completed.
Decision: Build a portable execution-governance and conformance layer, not another generic autonomous pentester.
Reason: XBOW, Astra, Horizon3, Pentera, Hadrian, Strix, CAI, PentestGPT, and HexStrike already provide substantial execution or orchestration capabilities. OWASP APTS provides an autonomous-testing governance baseline.
Evidence: `RESEARCH.md`; source links recorded there.
Risk: Public vendor pages contain marketing claims that require independent testing before product claims.
Next: Implement only after Phase 1 architecture and requirements gate.

### ARCH-0001: Mobile control plane and desktop execution plane

Date: 2026-08-17
Event: Cross-platform architecture research completed.
Decision: iPhone and Android are control/approval clients. Windows is the first execution host. Linux workers follow.
Reason: Apple sandbox/background/code-loading restrictions and Android sandbox/background limits make phones unsuitable as authoritative tool executors. Desktop hosts can provide worker isolation and durable execution.
Evidence: `ARCHITECTURE.md`; Apple Platform Security; Apple App Review Guidelines; Flutter documentation; Microsoft Windows Sandbox/Job Objects; Linux kernel seccomp/Landlock; NIST SP 800-207.
Risk: Mobile connectivity, device compromise, and approval replay require explicit controls.
Next: Phase 1 architecture implementation and threat-model gate.

### ROADMAP-0001: Ten-skill roadmap

Date: 2026-08-17
Event: Research was converted into a phased implementation plan.
Decision: Use five phases and ten skills.
Evidence: `ROADMAP.md` and `skills/README.md`.
Risk: Skill boundaries may need adjustment after Phase 1 design review.
Next: Do not start Phase 1 until the user confirms the skill directory is available to Luna/OpenCode.

## Source Register

Primary sources used for the planning baseline:

- OWASP APTS: https://github.com/OWASP/APTS
- OWASP WSTG: https://owasp.org/www-project-web-security-testing-guide/
- OWASP ASVS: https://owasp.org/www-project-application-security-verification-standard/
- NIST SP 800-115: https://csrc.nist.gov/pubs/sp/800/115/final
- NIST AI RMF: https://www.nist.gov/itl/ai-risk-management-framework
- NIST SP 800-207: https://csrc.nist.gov/pubs/sp/800/207/final
- CISA VDP Template: https://www.cisa.gov/vulnerability-disclosure-policy-template
- CISA BOD 20-01: https://www.cisa.gov/news-events/directives/bod-20-01-develop-and-publish-vulnerability-disclosure-policy
- OWASP Agentic Top 10: https://genai.owasp.org/resource/owasp-top-10-for-agentic-applications-for-2026/
- PentestGPT paper: https://www.usenix.org/conference/usenixsecurity24/presentation/deng
- Cybench: https://arxiv.org/abs/2408.08926
- CyberGym: https://arxiv.org/abs/2506.02548
- BountyBench: https://arxiv.org/abs/2505.15216
- AgentDojo: https://arxiv.org/abs/2406.13352
- CAI prompt-injection research: https://arxiv.org/abs/2508.21669
- Apple runtime security: https://support.apple.com/guide/security/security-of-runtime-process-sec15bfe098e/web
- Apple App Review Guidelines: https://developer.apple.com/app-store/review/guidelines/
- Flutter platforms: https://docs.flutter.dev/reference/supported-platforms
- Tauri security: https://v2.tauri.app/security/
- Windows Sandbox: https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-overview
- Windows Job Objects: https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects
- Linux seccomp: https://docs.kernel.org/userspace-api/seccomp_filter.html
- Linux Landlock: https://docs.kernel.org/userspace-api/landlock.html
- OAuth native apps: https://www.rfc-editor.org/rfc/rfc8252

## Skill Register

The following skills were created from the research baseline and are tracked as project artifacts:

- `cyber-project-governance`
- `cyber-cross-platform-architecture`
- `cyber-authorization-scope-policy`
- `cyber-safe-execution-containment`
- `cyber-model-tool-interoperability`
- `cyber-state-evidence-replay`
- `cyber-methodology-as-code`
- `cyber-web-api-business-logic`
- `cyber-mobile-control-plane`
- `cyber-evaluation-reporting-release`

## Phase Gate

Current phase: Phase 2 authorization and containment
Phase 1 status: Complete for artifact and contract scope; runtime controls not implemented
Live testing permission: Not granted by this project record
Release status: Not releasable

Phase 2 implementation is authorized by the human in the loop and is restricted to local fixtures, fake workers, and controlled host tests.

### SETUP-0001: Planning artifact verification

Date: 2026-08-17
Event: Project skeleton, audit file, planning documents, skill registry, and ten skill files were created and re-read.
Intent: Establish the auditable pre-implementation baseline before Phase 1.
Observed result: Required file check returned `required=16 missing=0`.
Evidence: `README.md`, `RESEARCH.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `skills/README.md`, and all ten `skills/*/SKILL.md` files were read after creation. The files contain the expected project scope, source-backed decisions, architecture boundaries, phase gates, skill frontmatter, and acceptance requirements.
Files created: `.gitignore`, `README.md`, `RESEARCH.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `agent_notes.Md`, `skills/README.md`, and ten skill files.
Safety result: No live target was contacted, no security tool was executed, no credential was stored, and Phase 1 implementation did not start.
Risk: The current audit is initialized but cannot be complete until future implementation, failures, fixes, tests, and release events are appended.
Next: Prepare the Phase 1 directive only after the user confirms the project root and skill set are ready for Luna/OpenCode execution.

### AUDIT-0002: Audit field reconciliation

Date: 2026-08-17
Event type: Audit governance correction
Actor: Luna/OpenCode
Intent: Ensure every historical project event can be reviewed using the audit record's required fields without rewriting prior entries.
Observed result: Historical conversation-derived entries predate the normalized field template and do not all spell out actor and event type.
Evidence: Entries AUDIT-0001, RESEARCH-0001, ARCH-0001, ROADMAP-0001, SETUP-0001, and VERIFY-0001/0002.
Decision: Preserve historical entries and add explicit normalized fields to every future entry. `docs/decision-log.md` indexes the historical entries and defines the reconciliation rule.
Risk: Historical entries rely partly on their surrounding headings for field interpretation.
Next: Keep all future entries normalized and include actor, intent, observed result, evidence, risks, and next action.

### REVIEW-0001: Independent Phase 1 review findings

Date: 2026-08-17
Event type: Independent artifact review
Actor: Independent review agent
Intent: Challenge Phase 1 artifacts for contradictions, missing gates, unsafe behavior, and unsupported completion claims.
Observed result: Review identified blockers involving missing governance records, broad traceability refs, post-proposal policy placement in the diagram, under-specified schemas, relay failure semantics, state/finding alignment, global skill assignment, and audit-field normalization.
Evidence: Independent review report returned in the project session; findings were mapped to `docs/requirements-matrix.md`, `ARCHITECTURE.md`, schemas, `ROADMAP.md`, `agent_notes.Md`, and missing `docs/*` records.
Risk: Phase 1 could have been falsely declared complete with incomplete contracts.
Next: Fix every blocker, rerun the full acceptance battery, and repeat independent review.

### VERIFY-0001: Initial Phase 1 acceptance command failure

Date: 2026-08-17
Event: First automated Phase 1 acceptance command stopped on the state-invariant assertion.
Observed result: The command reported `missing_state_invariant=RUNNING requires a valid`.
Root cause: The PowerShell test string interpreted Markdown backtick characters instead of matching the literal documented text. The project state model itself contains the required invariant.
Impact: Acceptance was not claimed. No project artifact was changed by this failed check.
Correction: Match the literal backtick-delimited invariant text and rerun the complete battery.
Next: Execute the corrected acceptance command and record its observed result.

### VERIFY-0003: Acceptance assertion synchronized with state contract

Date: 2026-08-17
Event type: Acceptance-command maintenance
Actor: Luna/OpenCode
Intent: Keep the acceptance command aligned with the strengthened Phase 1 state invariant.
Observed result: The command reported `missing_state_invariant=No state transition may skip external policy`.
Root cause: The state model was deliberately changed to require an explicit policy decision event for every action transition and to distinguish bookkeeping transitions. The test still expected the previous wording.
Impact: Acceptance was not claimed. No implementation artifact failed this check.
Correction: Replace the stale assertion with the current literal invariant `Every action transition has a policy decision event` and retain checks for permit, evidence, verifier, report-policy, and model-state invariants.
Next: Rerun the complete acceptance battery.

### REVIEW-0002: Remediation review findings

Date: 2026-08-17
Event type: Independent artifact review
Actor: Independent review agent
Intent: Verify whether the first remediation pass closed the Phase 1 blockers.
Observed result: No blockers or high findings remained, but medium findings remained for result-event precision, permit consumption conditionals, source-pattern reconciliation, low-impact relay disposition, audit addenda, and finding-transition completeness.
Evidence: Independent review report returned in the project session; findings referenced the result/permit schemas, standards lock, requirements matrix, relay design, state model, skill, and audit file.
Risk: Phase 1 could still overstate cross-record binding or audit completeness if the remaining findings were ignored.
Next: Close every remaining medium finding, rerun acceptance, and repeat independent review.

### AUDIT-0003: Normalized verification addenda

Date: 2026-08-17
Event type: Audit field reconciliation
Actor: Luna/OpenCode
Intent: Make verification-command history auditable without rewriting append-only historical entries.
Observed result: Added explicit field mappings for all verification-command events below.
Evidence: This addendum, the referenced `VERIFY-*` entries, and `docs/decision-log.md`.
Risk: The original command entries remain historical records and are supplemented rather than replaced.
Next: Use the normalized entry fields for all future audit events.

| Entry | Event type | Actor | Intent | Observed result | Evidence | Risk | Next action |
|---|---|---|---|---|---|---|---|
| VERIFY-0001 | Acceptance command failure | Luna/OpenCode | Match the initial state invariant | PowerShell assertion stripped Markdown backticks | Command output and `docs/state-model.md` | Acceptance not claimed | Correct literal assertion |
| VERIFY-0002 | Acceptance command failure | Luna/OpenCode | Match a literal Markdown state token | Wildcard matching did not match the backtick-delimited token | Command output and state-model re-read | Acceptance not claimed | Use literal containment |
| VERIFY-0003 | Acceptance command maintenance | Luna/OpenCode | Synchronize assertion with strengthened state wording | Test expected obsolete wording | Command output and updated state model | Acceptance not claimed | Use current invariant |
| VERIFY-0004 | Acceptance command parser failure | Luna/OpenCode | Check relay-loss words | PowerShell parser rejected embedded Markdown backtick | Command parser output and architecture re-read | Acceptance not claimed | Use delimiter-free literal words |

### VERIFY-0005: Final Phase 1 acceptance

Date: 2026-08-17
Event type: Phase acceptance execution
Actor: Luna/OpenCode
Intent: Verify the remediated Phase 1 foundation and architecture artifacts.
Observed result: `phase1_acceptance=passed`; `required_files=23`; `json_files_parsed=5`; `skill_files_validated=10`; `requirements_rows=12`; `standards_sources=26`; `live_testing=not_performed`.
Evidence: Final Phase 1 acceptance command; `docs/acceptance-matrix.md` P1-001 through P1-016; JSON parser output; frontmatter validation output.
Risk: This validates artifacts and contracts only. Runtime enforcement, sandbox strength, mobile security, provider security, and source content hashes remain unverified.
Next: Record independent closure review and stop before Phase 2.

### REVIEW-0003: Final independent Phase 1 review

Date: 2026-08-17
Event type: Independent closure review
Actor: Independent review agent
Intent: Review the final Phase 1 artifacts after all blocker and medium-finding remediations.
Observed result: No blockers, high, or medium findings remain. Evidence counts reconcile: 14 evidence-index entries, 12 requirements, 10 skills, 4 schema files, and 5 JSON contracts including the standards lock.
Evidence: Final independent review report returned in the project session.
Risk: Runtime cross-record enforcement, atomic permit consumption, relay-loss cleanup, sandbox strength, mobile approval security, provider security, and cross-engagement isolation remain unimplemented or untested.
Next: Stop Phase 1 and await human review before issuing the Phase 2 directive.

### PHASE-0001: Phase 1 completion

Date: 2026-08-17
Event type: Phase completion
Actor: Luna/OpenCode
Intent: Close the Foundation and Architecture phase according to `ROADMAP.md`.
Observed result: Phase 1 passed its artifact/contract acceptance battery and independent review. The phase produced research, standards lock, requirements traceability, threat model, architecture decision, state model, data contracts, local fixture plan, risk register, evidence index, acceptance matrix, and ten skills.
Evidence: `docs/acceptance-matrix.md` P1-001 through P1-016; `VERIFY-0005`; `REVIEW-0003`; `README.md`; `ROADMAP.md`.
Risk: Phase 1 does not claim runtime security, live testing, APTS conformance, or release readiness.
Next: Wait for human review. Do not begin Phase 2 until a new directive is issued.

### VERIFY-0004: Acceptance command parser correction

Date: 2026-08-17
Event type: Acceptance-command maintenance
Actor: Luna/OpenCode
Intent: Keep the acceptance command executable while checking relay-loss requirements.
Observed result: PowerShell stopped with a parser error because a Markdown backtick appeared inside a double-quoted assertion string.
Root cause: The test harness mixed Markdown markup with PowerShell escape syntax.
Impact: Acceptance did not execute. No project artifact failed this check.
Correction: Check the relay rule using literal words without Markdown delimiter characters.
Next: Rerun the complete acceptance battery.

### VERIFY-0002: Literal assertion correction

Date: 2026-08-17
Event: The first correction still failed in the acceptance command while matching Markdown backticks through PowerShell wildcard semantics.
Observed result: The command again reported `missing_state_invariant=`RUNNING` requires a valid`.
Root cause: The verification harness used wildcard matching for a literal Markdown token.
Impact: Acceptance was not claimed. No project artifact was changed by this failed check.
Correction: Use literal string containment for state-invariant assertions.
Next: Rerun the complete battery with literal containment.

### CLOSE-0001: Phase 1 closure consistency check

Date: 2026-08-17
Event type: Phase closure verification
Actor: Luna/OpenCode
Intent: Confirm that completion status, acceptance results, audit evidence, standards count, and phase transition state agree after closure updates.
Observed result: `phase1_closure=passed`; `acceptance_rows_passed=16`; `json_contracts_parsed=5`; `standards_sources=26`; `phase2_started=no`; `live_testing=not_performed`.
Evidence: Final post-closure consistency command; `docs/acceptance-matrix.md`; `ROADMAP.md`; `agent_notes.Md` PHASE-0001 and REVIEW-0003.
Risk: Runtime controls, source content hashes, sandbox strength, mobile security, provider security, and live-target behavior remain unverified and release-blocking.
Next: Stop and await human review before the Phase 2 directive.

### PHASE2-RESEARCH-0001: Authorization and containment contract

Date: 2026-08-17
Event type: Phase readiness research
Actor: Luna/OpenCode
Intent: Verify Phase 2 scope, source requirements, implementation constraints, and skills before authorization.
Observed result: Phase 2 research is source-backed by APTS SE/SC/HO/MR/AR controls, CISA VDP, NIST SP 800-207, Windows Job Objects/Sandbox, and Linux seccomp/Landlock. The two Phase 2 skills are present in the registry and were frontmatter-validated during Phase 1.
Evidence: `docs/phase2-research.md`, `ROADMAP.md`, `skills/cyber-authorization-scope-policy/SKILL.md`, `skills/cyber-safe-execution-containment/SKILL.md`.
Risk: Runtime policy, permit, and containment behavior is not yet implemented or verified.
Next: Implement Phase 2 using local fixtures only.

### TOOLCHAIN-0001: Phase 2 runtime choice

Date: 2026-08-17
Event type: Toolchain verification
Actor: Luna/OpenCode
Intent: Select a dependency-free implementation runtime available on the host.
Observed result: `dotnet --version` returned `10.0.300`; Node returned `v26.4.0`; Python, Rust/Cargo, and Docker were unavailable through the verified command paths.
Decision: Use .NET 10 for the Phase 2 core so Windows process containment and cryptographic primitives can be implemented without unverified package installation. Keep UI/provider integrations out of Phase 2.
Evidence: Toolchain commands executed from `E:\cyber-sop-harness`; `docs/phase2-research.md`.
Risk: Cross-platform Linux implementation and mobile UI remain later-phase work.
Next: Create the Phase 2 core project and tests.

### BUILD-0001: Phase 2 core implementation

Date: 2026-08-17
Event type: Phase 2 implementation
Actor: Luna/OpenCode
Intent: Implement the authorization and containment spine using the Phase 2 skills and research contract.
Observed result: Added .NET 10 core with signed manifest verification, scope/CIDR/wildcard/redirect evaluation, capability registry, policy decisions, RSA-signed one-use permits, encrypted credential handles, rate/concurrency limiter, fixture workers, relay-loss controller, rollback ledger, and Windows Job Object provider.
Evidence: `src/CyberSopHarness.Core`; `tests/Phase2.Tests`; `docs/phase2-research.md`.
Risk: Real target adapters, Linux containment integration, cloud isolation, mobile controls, and provider integrations remain out of scope.
Next: Compile and execute the Phase 2 behavioral suite.

### BUILD-0002: Core compile correction

Date: 2026-08-17
Event type: Build failure and fix
Actor: Luna/OpenCode
Intent: Resolve the first Phase 2 compile failure.
Observed result: Initial build failed with `CS8852` because the permit signature property was init-only but assigned after construction. The property was changed to a controlled setter; the next core build returned zero warnings and zero errors.
Evidence: `dotnet build src\\CyberSopHarness.Core\\CyberSopHarness.Core.csproj --configuration Release`; source correction in `Models.cs`.
Risk: Build failure was contained to Phase 2 code and no target/tool action occurred.
Next: Run behavioral tests.

### P2-TEST-0001: Scope fixture correction

Date: 2026-08-17
Event type: Behavioral test failure and fix
Actor: Luna/OpenCode
Intent: Verify fixture CIDR handling without weakening hard-deny behavior.
Observed result: The first suite run blocked documentation CIDR `198.51.100.0/24` because fixture mode denied every non-loopback address. The policy was corrected to continue denying private/reserved/metadata addresses while allowing documentation-range fixtures. The test was rerun.
Evidence: Initial test output `documentation CIDR was not accepted`; correction in `ScopeEvaluator.cs`.
Risk: Broad fixture denial would have hidden legitimate local policy tests; broad fixture allowance would weaken private/metadata protection.
Next: Rerun the full suite.

### P2-TEST-0002: Capability fixture correction

Date: 2026-08-17
Event type: Behavioral test fixture failure and fix
Actor: Luna/OpenCode
Intent: Verify R3 approval behavior through the registered capability path.
Observed result: The first capability-aware test used an R0 capability for an R3 request, so the capability gate correctly blocked before approval evaluation. The test was corrected to use the registered R3 capability.
Evidence: Initial test output `R3 action did not require approval`; correction in `tests/Phase2.Tests/Program.cs`.
Risk: The failed test exposed an ordering distinction between capability rejection and approval escalation.
Next: Rerun the full suite.

### P2-TEST-0003: Phase 2 behavioral suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify Phase 2 authorization and containment behavior using local fixtures only.
Observed result: Ten tests passed: manifest signature/validation; scope and redirect decisions; policy risk and approval decisions; permit signature/consumption/expiry/replay; credential vault encryption/revocation; rate limiting/concurrency; worker containment/permit enforcement; worker stopping; rollback order/idempotence; Windows Job Object setup/termination.
Evidence: `dotnet build tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release`; `dotnet run --project tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release`; output `phase2_tests=passed count=10`.
Risk: This does not prove live-target safety, Linux containment, mobile security, provider security, cloud isolation, or external tool behavior.
Next: Perform independent Phase 2 implementation review before closure.

### SKILL-LOAD-0001: Project-local Phase 2 skills loaded

Date: 2026-08-17
Event type: Skill activation
Actor: Luna/OpenCode
Intent: Resume Phase 2 under the project-local governance, authorization, containment, evaluation, and verification instructions.
Observed result: Global loader did not contain the project-local skill names; the authoritative copies under `E:\cyber-sop-harness\skills\` were read and applied directly. Global `project-governance`, `verify-first-discipline`, `developer-code-and-credo`, `verification-complete`, `professional-developer`, and `surgical-precision` were loaded successfully.
Evidence: Skill-loader output and reads of the four Phase 2 project-local skill files.
Risk: Project-local skills are not globally registered; future sessions must read them from the project directory unless registration is added.
Next: Continue Phase 2 review and verification.

### P2-REFACTOR-0001: Security-boundary hardening

Date: 2026-08-17
Event type: Phase 2 security refactor
Actor: Luna/OpenCode
Intent: Close independent-review findings in authorization, policy, permit, scope, approval, containment, credential, rate, and worker lifecycle code.
Observed result: Added trusted authority key store; full-manifest signature coverage; signed approvals; policy-owned permit issuance and re-evaluation; action/manifest/scope hashes; permit expiry and relay latch; scheme, resolved-address, same-origin, and metadata checks; external containment attestation; mandatory capability registry/freeze; mandatory rate/payload/duration/output enforcement; span-based credential use; reverse-order rollback synchronization; and suspended-start Windows Job Object process creation.
Evidence: `src/CyberSopHarness.Core`; updated Phase 2 tests; code review findings in the project session.
Risk: Provider/cloud/mobile/Linux runtime integration remains later-phase work.
Next: Compile and execute the full Phase 2 suite.

### P2-BUILD-0003: MSBuild generated-state warning

Date: 2026-08-17
Event type: Build warning
Actor: Luna/OpenCode
Intent: Verify Phase 2 after security refactor with warnings visible.
Observed result: Build succeeded with zero errors but emitted `MSB3101` because the generated `Phase2.Tests.csproj.AssemblyReference.cache` already existed. The warning persisted after `dotnet clean` and single-node build.
Evidence: `dotnet build tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release -m:1` output.
Risk: Generated-state warning may indicate an SDK/project-structure issue; it is not being suppressed and remains a build-quality follow-up.
Next: Continue behavioral and independent review; resolve the warning before release packaging.

### P2-TEST-0004: Timeout/stop-path correction

Date: 2026-08-17
Event type: Behavioral test failure and fix
Actor: Luna/OpenCode
Intent: Distinguish capability timeout from relay/operator stop cancellation.
Observed result: Relay stop initially surfaced as `TimeoutException` because the supervisor converted all non-caller cancellation to timeout. The supervisor now checks its stopping state; relay stop preserves cancellation semantics while capability expiry remains a timeout.
Evidence: Test output and correction in `Workers.cs`.
Risk: Cancellation classification is safety-relevant because stop events must not be mislabeled as worker timeouts.
Next: Rerun the full Phase 2 suite.

### P2-TEST-0005: Refactored Phase 2 suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify the hardened Phase 2 core after the refactor.
Observed result: `phase2_tests=passed count=10`.
Evidence: `dotnet run --project tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release`; ten PASS lines including trusted signatures, scope/redirect, approval, permits, credentials, rate limits, containment, stop, rollback, and suspended Job Object termination.
Risk: Independent review and the MSBuild warning follow-up remain outstanding.
Next: Run independent Phase 2 implementation review.

### P2-REFACTOR-0002: Review-driven hardening continuation

Date: 2026-08-17
Event type: Phase 2 security refactor
Actor: Luna/OpenCode
Intent: Close the next independent-review findings without weakening acceptance criteria.
Observed result: Added signed containment attestations and supervisor verification, bounded stop/force-stop orchestration, full signed approval binding and one-use approval consumption, resolved-address and redirect enforcement, supervisor-owned rate limits, defensive capability copies, rollback/credential cleanup on stop, frozen trust roots, permit-expiry cancellation, and atomic scope/policy re-evaluation.
Evidence: `src/CyberSopHarness.Core`; updated tests; review findings in the project session.
Risk: Linux, cloud, mobile, provider, and real tool integrations remain later-phase work.
Next: Run the clean build and Phase 2 behavioral suite, then request independent review.

### P2-BUILD-0004: Generated-state warning resolved

Date: 2026-08-17
Event type: Build fix
Actor: Luna/OpenCode
Intent: Remove the persistent MSBuild generated-state warning without suppressing warnings.
Observed result: Added `DisableRarCache=true` to the self-test project, cleaned generated outputs, and rebuilt. The build returned zero warnings and zero errors.
Evidence: `tests/Phase2.Tests/Phase2.Tests.csproj`; `dotnet clean tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release`; `dotnet build tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release -m:1`.
Risk: Disabling the assembly-reference cache trades incremental-build speed for deterministic warning-free test builds; it does not disable compilation or test assertions.
Next: Execute the clean behavioral suite and independent review.

### P2-TEST-0006: Clean hardened suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify the latest Phase 2 hardening after a clean build.
Observed result: Build returned zero warnings and zero errors; test output returned `phase2_tests=passed count=10` with ten PASS lines.
Evidence: Clean build and `dotnet run --project tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release --no-build` output.
Risk: Independent implementation review remains outstanding; live/real-tool/runtime platform risks remain out of scope.
Next: Perform independent Phase 2 implementation review.

### P2-REVIEW-0001: Independent review findings

Date: 2026-08-17
Event type: Independent implementation review
Actor: Independent review agent
Intent: Challenge the latest Phase 2 runtime for authorization substitution, time authority, malformed requests, mapped addresses, containment enforcement, and adversarial coverage.
Observed result: No blocker was reported, but high findings remained: caller-supplied permit issuer accepted by the supervisor; caller-supplied time used for expiry and rate controls; incomplete action fields not validated before authorization; IPv4-mapped IPv6 private/metadata handling; and fixture/declarative containment limits not equivalent to external process enforcement. Medium findings covered host-only scope/egress enforcement and insufficient adversarial tests.
Evidence: Independent review report returned in the project session; findings referenced `Workers.cs`, `PermitIssuer.cs`, `PolicyEngine.cs`, `ManifestValidation.cs`, `CredentialVault.cs`, `RateLimiter.cs`, `ScopeEvaluator.cs`, `WindowsJobObject.cs`, and `tests/Phase2.Tests/Program.cs`.
Risk: Phase 2 remains unclosed and must not be represented as authorized/live execution security.
Next: Remove issuer substitution, add malformed-input and mapped-address tests, then address authoritative time and containment scope explicitly.

### P2-REFACTOR-0003: Issuer and input-boundary correction

Date: 2026-08-17
Event type: Phase 2 security refactor
Actor: Luna/OpenCode
Intent: Close the issuer-substitution and malformed-input findings without weakening fail-closed behavior.
Observed result: `WorkerSupervisor.ExecuteAsync` now consumes only from its constructor-bound `PermitIssuer`; the caller can no longer supply a different issuer. `PolicyEngine` now blocks incomplete action requests and authorization-reference mismatches before capability/policy evaluation. Mapped IPv4 addresses are normalized before metadata/private checks, including an explicit mapped-metadata fixture test.
Evidence: `src/CyberSopHarness.Core/Workers.cs`, `src/CyberSopHarness.Core/PolicyEngine.cs`, `src/CyberSopHarness.Core/ScopeEvaluator.cs`, and `tests/Phase2.Tests/Program.cs`.
Risk: The change is not yet compiled or executed. Caller-supplied time, process-enforced limits, egress binding, and external provider containment remain unresolved or explicitly out of scope.
Next: Build and run the complete Phase 2 suite; then continue with time authority and containment-boundary decisions.

### P2-BUILD-0005: Issuer and input-boundary build

Date: 2026-08-17
Event type: Build verification
Actor: Luna/OpenCode
Intent: Compile the issuer-binding, malformed-input, and mapped-address corrections.
Observed result: `dotnet build tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release -m:1` succeeded with `0 Warning(s)` and `0 Error(s)`.
Evidence: Build output; `src/CyberSopHarness.Core` and `tests/Phase2.Tests` release assemblies were produced.
Risk: Compilation does not prove adversarial behavior or close the remaining time/containment findings.
Next: Execute the rebuilt behavioral suite.

### P2-TEST-0007: Issuer and input-boundary regression suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify the corrected supervisor issuer binding, malformed-action rejection, mapped metadata denial, and all prior Phase 2 behavior.
Observed result: Ten tests passed, including the new adversarial assertions; output was `phase2_tests=passed count=10` with ten `PASS` lines.
Evidence: `dotnet run --project tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release --no-build` output; updated `tests/Phase2.Tests/Program.cs`.
Risk: Caller-supplied time, process-enforced limits, egress binding, and external provider containment remain unresolved or out of scope.
Next: Obtain an independent post-fix review before changing the time/containment APIs.

### P2-REFACTOR-0004: Authoritative clock boundary

Date: 2026-08-17
Event type: Phase 2 security refactor
Actor: Luna/OpenCode
Intent: Remove caller-controlled timestamps from manifest validation, policy evaluation, permit issuance/consumption, credential use, rate acquisition, and worker execution.
Observed result: Added internal `AuthoritativeClock` backed by `DateTimeOffset.UtcNow`; security APIs now read the clock inside each operation instead of accepting a per-call `now` argument. Expiry and rate rollover tests were changed to use short real delays.
Evidence: `src/CyberSopHarness.Core/Clock.cs`, `ManifestValidation.cs`, `PolicyEngine.cs`, `PermitIssuer.cs`, `CredentialVault.cs`, `RateLimiter.cs`, `Workers.cs`, and `tests/Phase2.Tests/Program.cs`.
Risk: Host clock integrity and time synchronization remain operational assumptions. This change is not yet compiled or executed.
Next: Build and run the suite; fix any API or timing regressions before continuing containment integration.

### P2-BUILD-0006: Authoritative clock build

Date: 2026-08-17
Event type: Build verification
Actor: Luna/OpenCode
Intent: Compile the authoritative-clock API migration.
Observed result: `dotnet build tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release -m:1` succeeded with `0 Warning(s)` and `0 Error(s)`.
Evidence: Build output; release core and test assemblies produced.
Risk: Compile success does not establish real expiry/rate behavior.
Next: Execute the rebuilt suite and inspect timing-sensitive output.

### P2-TEST-0008: Authoritative clock regression suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify expiry, rate rollover, policy, credential, worker, rollback, and Windows containment behavior after removing caller-supplied timestamps.
Observed result: Ten tests passed; output was `phase2_tests=passed count=10` with ten `PASS` lines. Expiry uses a 25 ms real delay and rate rollover uses a 600 ms real delay.
Evidence: `dotnet build ... && dotnet run --project tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release --no-build` output.
Risk: Host clock integrity remains an operational assumption; authorized/live provider enforcement remains intentionally unavailable.
Next: Record the safety boundary in the Phase 2 contract and independently review the current implementation.

### P2-REVIEW-0002: Post-clock independent review

Date: 2026-08-17
Event type: Independent implementation review
Actor: Independent review agent
Intent: Re-audit the current Phase 2 after issuer/input and clock hardening.
Observed result: One high finding remained: `StopAllAsync` could abort before cleanup on a cancellation exception and its forced path waited on the graceful task. Medium findings remained for direct-stop permit latching, null collection validation, mapped-address normalization, non-Windows Job Object coverage, and the intentionally unintegrated authorized provider path.
Evidence: Independent review report returned in the project session; findings referenced `Workers.cs`, `Canonicalization.cs`, `PolicyEngine.cs`, `ScopeEvaluator.cs`, `WindowsJobObject.cs`, and `tests/Phase2.Tests/Program.cs`.
Risk: Stop/relay cleanup was not yet fail-closed under throwing callbacks or hung graceful stops.
Next: Catch cancellation errors, make forced cleanup independent of graceful completion, and test direct-stop issuance latching.

### P2-REFACTOR-0005: Fail-closed stop cleanup

Date: 2026-08-17
Event type: Phase 2 security refactor
Actor: Luna/OpenCode
Intent: Ensure stop cleanup cannot be skipped by cancellation errors or a hung graceful stop.
Observed result: `StopAllAsync` now catches cancellation exceptions before cleanup, latches the permit issuer with `HandleRelayLoss`, and evaluates forced stop plus operation completion without waiting for the graceful-stop task to converge. A direct-stop test verifies subsequent permit issuance is rejected.
Evidence: `src/CyberSopHarness.Core/Workers.cs`; `tests/Phase2.Tests/Program.cs`.
Risk: A worker that ignores both graceful and forced stop still causes a bounded timeout, by design; rollback and credential cleanup are still attempted before the timeout is reported.
Next: Build and execute the full suite, then repeat independent review.

### P2-BUILD-0007: Fail-closed stop build

Date: 2026-08-17
Event type: Build verification
Actor: Luna/OpenCode
Intent: Compile the stop-cleanup correction.
Observed result: `dotnet build tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release -m:1` succeeded with `0 Warning(s)` and `0 Error(s)`.
Evidence: Build output; release assemblies produced.
Risk: Compilation does not prove cleanup under a throwing or hung worker.
Next: Execute the suite and inspect stop-path assertions.

### P2-TEST-0009: Fail-closed stop regression suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify relay-loss stop, direct-stop issuer latching, worker cancellation, and all prior Phase 2 behavior.
Observed result: Ten tests passed; output was `phase2_tests=passed count=10` with ten `PASS` lines.
Evidence: `dotnet build ... && dotnet run --project tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release --no-build` output.
Risk: Throwing-callback and non-cooperative forced-stop paths are not yet directly exercised; authorized/live execution remains blocked.
Next: Add boundary validation and mapped-address normalization tests, then obtain a final independent review.

### P2-REFACTOR-0006: Policy input and mapped-address normalization

Date: 2026-08-17
Event type: Phase 2 security refactor
Actor: Luna/OpenCode
Intent: Convert malformed collection input into explicit policy blocks and make equivalent IPv4-mapped addresses consistent across exact, resolved, metadata, and CIDR checks.
Observed result: Canonicalization now tolerates null collections long enough for policy to reject them; policy rejects null collections and blank methodology references; mapped IPv4 values normalize before exact comparisons; mapped CIDR prefixes reduce from IPv6 `/120` to IPv4 `/24`; malformed CIDR strings no longer throw.
Evidence: `src/CyberSopHarness.Core/Canonicalization.cs`, `PolicyEngine.cs`, `ScopeEvaluator.cs`, and `tests/Phase2.Tests/Program.cs`.
Risk: This change is not yet independently reviewed; host clock, provider integration, and non-Windows containment remain residual limitations.
Next: Build and run the suite, then request a final independent review.

### P2-BUILD-0008: Policy input normalization build

Date: 2026-08-17
Event type: Build verification
Actor: Luna/OpenCode
Intent: Compile the malformed-input and mapped-address corrections.
Observed result: `dotnet build tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release -m:1` succeeded with `0 Warning(s)` and `0 Error(s)`.
Evidence: Build output; release assemblies produced.
Risk: Compilation does not prove all malformed and mapped boundary paths.
Next: Execute the regression suite.

### P2-TEST-0010: Policy input normalization regression suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify null/blank action rejection, exact and CIDR mapped-address normalization, malformed CIDR handling, and all prior Phase 2 behavior.
Observed result: Ten tests passed; output was `phase2_tests=passed count=10` with ten `PASS` lines.
Evidence: `dotnet build ... && dotnet run --project tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release --no-build` output.
Risk: Throwing cancellation callbacks, non-cooperative forced stops, non-Windows Job Object behavior, egress enforcement, and trusted external provider integration are not fully exercised.
Next: Obtain a final independent review before updating the Phase 2 acceptance status.

### P2-REVIEW-0003: Final boundary review

Date: 2026-08-17
Event type: Independent implementation review
Actor: Independent review agent
Intent: Verify Phase 2 closure readiness after policy normalization and fail-closed stop changes.
Observed result: No blocker remained. One high residual was identified: in-process fixture handlers cannot guarantee hard termination if they ignore cancellation. Medium/residual findings covered null nested objects, non-Windows Job Object coverage, broader reserved-address coverage, credential expiry-policy coupling, and absent trusted provider/egress integration. The review confirmed the documentation accurately states authorized/live dispatch is blocked.
Evidence: Independent review report returned in the project session; all Core source, Phase 2 tests, and Phase 2 documents were read.
Risk: Phase 2 is not a trusted/live execution release; a real provider must prove hard-stop enforcement before authorized dispatch is enabled.
Next: Encode hard-stop capability in containment attestations and finish the local-fixture verification run.

### P2-REFACTOR-0007: Hard-stop attestation and reserved-address boundary

Date: 2026-08-17
Event type: Phase 2 security refactor
Actor: Luna/OpenCode
Intent: Prevent future authorized providers from being accepted without an attested hard-stop guarantee and tighten reserved-address denial.
Observed result: `ContainmentAttestation` now signs `HardStopGuaranteed`; fixture attestations are explicitly false, registered external attestations are required to be true, and authorized verification requires the field. `WorkerSupervisor` now null-checks execution inputs. IPv4 unspecified/CGNAT and IPv6 unspecified/multicast/link-local/site-local ranges are denied.
Evidence: `src/CyberSopHarness.Core/Models.cs`, `ContainmentAuthority.cs`, `Workers.cs`, `ScopeEvaluator.cs`, and `tests/Phase2.Tests/Program.cs`.
Risk: No trusted external provider adapter is registered, so authorized/live dispatch remains blocked. In-process fixture handlers remain cooperative test doubles and cannot be used as hard-stop evidence.
Next: Build, execute, and independently re-review the final fixture boundary.

### P2-BUILD-0009: Hard-stop attestation build

Date: 2026-08-17
Event type: Build verification
Actor: Luna/OpenCode
Intent: Compile the hard-stop attestation, null-guard, and reserved-address changes.
Observed result: `dotnet build tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release -m:1` succeeded with `0 Warning(s)` and `0 Error(s)`.
Evidence: Build output; release assemblies produced.
Risk: Compilation does not prove provider hard-stop behavior.
Next: Execute the full suite and perform final review.

### P2-TEST-0011: Final local-fixture regression suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify all Phase 2 local-fixture, policy, scope, permit, stop, rollback, attestation, and Windows utility behavior after final hardening.
Observed result: Ten tests passed; output was `phase2_tests=passed count=10` with ten `PASS` lines.
Evidence: `dotnet build ... && dotnet run --project tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release --no-build` output.
Risk: Trusted external provider, hard process termination for uncooperative code, egress isolation, non-Windows containment, mobile/cloud security, and live-target behavior remain unverified and blocked by design.
Next: Update Phase 2 acceptance status to reflect local-fixture pass with authorized/live release blocked, then preserve this boundary for Phase 3 planning.

### P2-REFACTOR-0008: Fixture attestation consistency

Date: 2026-08-17
Event type: Phase 2 security refactor
Actor: Luna/OpenCode
Intent: Make fixture-mode containment verification explicitly reject a hard-stop claim as well as external enforcement.
Observed result: `ContainmentAuthority.Verify` now requires fixture attestations to remain non-external and non-hard-stop; the signed `HardStopGuaranteed` field remains required for any future authorized provider.
Evidence: `src/CyberSopHarness.Core/ContainmentAuthority.cs`.
Risk: The post-change behavioral command did not start because the .NET/MSBuild host raised `Releasing the double mapped memory failed`; this is an environment failure, not a test result.
Next: Execute the already-built test DLL directly and record the observed result.

### P2-TEST-0012: Direct-assembly final regression suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify the final attestation consistency change after the project-level runner hit a host failure.
Observed result: Direct execution of `dotnet tests\\Phase2.Tests\\bin\\Release\\net10.0\\Phase2.Tests.dll` passed all ten tests; output was `phase2_tests=passed count=10` with ten `PASS` lines.
Evidence: Direct test-DLL output; preceding build output with `0 Warning(s)` and `0 Error(s)`.
Risk: The project-level `dotnet run --no-build` invocation remains subject to the observed .NET/MSBuild `Releasing the double mapped memory failed` host failure; direct assembly execution is the verified fallback for this run.
Next: Keep Phase 2 status as local-fixture acceptance passed and authorized/live release blocked; plan the trusted provider/hard-stop adapter as the next gated runtime step.

### DOC-0001: Phase 2 status reconciliation

Date: 2026-08-17
Event type: Documentation update
Actor: Luna/OpenCode
Intent: Align public project status and acceptance records with observed verification and residual safety boundaries.
Observed result: `README.md` and `docs/phase2-acceptance.md` now state that local-fixture acceptance passed, independent review found no blocker/high for that boundary, and authorized/live dispatch remains blocked pending trusted provider, hard-stop, egress, and platform evidence. The acceptance commands include the direct-DLL fallback.
Evidence: `README.md`, `docs/phase2-acceptance.md`, `ROADMAP.md`, and this audit.
Risk: Overall roadmap status remains Phase 2 in progress because the authorized provider gate is intentionally open.
Next: Do not start Phase 3 until the project owner authorizes the next phase and the Phase 2 transition rule is satisfied.

### QUERY-0001: Research-defined next steps

Date: 2026-08-17
Event type: Roadmap guidance
Actor: Luna/OpenCode
Intent: Answer the human question about the research-defined next implementation step.
Observed result: The research and roadmap define the next work as Phase 3 Portable Runtime and Evidence after the documented local-fixture Phase 2 gate: provider adapter contract, tool capability manifest, typed action envelope, workflow state machine, append-only hash-linked events, raw/redacted evidence, replay, independent verification, and finding lifecycle. Authorized/live execution remains blocked until the trusted provider adapter and hard-stop/network-boundary evidence exist.
Evidence: `ROADMAP.md` Phase 3 deliverables/gates; `docs/phase2-research.md` implementation decision and Phase 2 gate; `ARCHITECTURE.md` policy/tool/evidence boundaries; `skills/cyber-model-tool-interoperability/SKILL.md`; `skills/cyber-state-evidence-replay/SKILL.md`.
Risk: Starting provider execution, mobile UI, or live testing before these contracts and gates would violate the phase transition rule.
Next: If authorized, load the two Phase 3 skills, write the Phase 3 research/acceptance contract, then implement deterministic local fake adapters and evidence/replay tests first.

### P3-RESEARCH-0001: Portable runtime and evidence contract

Date: 2026-08-17
Event type: Phase readiness research
Actor: Luna/OpenCode
Intent: Translate the Phase 3 interoperability and state/evidence skills into an implementation and acceptance contract before code changes.
Observed result: Added `docs/phase3-research.md` and `docs/phase3-acceptance.md`. The contract requires provider parity, typed/frozen tool registration, consumed-permit dispatch, normalized raw/redacted evidence, host-controlled state, hash-chain freezing, replay labels, independent verification, finding lifecycle, and report gating. It explicitly keeps live targets, real credentials, external tools, mobile UI, and authorized dispatch out of scope.
Evidence: `skills/cyber-model-tool-interoperability/SKILL.md`, `skills/cyber-state-evidence-replay/SKILL.md`, `ROADMAP.md`, `ARCHITECTURE.md`, new Phase 3 documents.
Risk: Phase 3 skills are project-local and not registered in the global loader; they were read directly from `E:\cyber-sop-harness\skills\`.
Next: Implement the typed runtime and local deterministic test project against the documented acceptance matrix.

### P3-IMPLEMENT-0001: Initial typed runtime and evidence slice

Date: 2026-08-17
Event type: Phase 3 implementation
Actor: Luna/OpenCode
Intent: Implement the first Phase 3 runtime slice from the approved contract.
Observed result: Added provider descriptors/proposals, action envelopes, frozen typed tool registrations, adapter result contracts, artifact storage, raw/redacted evidence events, hash-chain verification, workflow audit entries, broker orchestration, host-controlled workflow state, independent fixture verification, replay packages, finding lifecycle, and report policy.
Evidence: `src/CyberSopHarness.Core/Phase3Contracts.cs`, `Phase3Evidence.cs`, `Phase3Runtime.cs`; `tests/Phase3.Tests`.
Risk: The initial review identified missing authority bindings, permit replay, forged audit/evidence risks, incomplete timeout semantics, and future live-provider containment gaps. Phase 3 was not closed.
Next: Harden the broker and evidence/state boundaries before acceptance.

### P3-BUILD-0001: Initial runtime compile correction

Date: 2026-08-17
Event type: Build failure and fix
Actor: Luna/OpenCode
Intent: Compile the initial Phase 3 runtime before behavioral testing.
Observed result: The first core build failed with a definite-assignment error in the independent verifier; the verifier condition was split into explicit guarded steps. The subsequent core build succeeded with `0 Warning(s)` and `0 Error(s)`.
Evidence: `Phase3Runtime.cs`; `dotnet build src\\CyberSopHarness.Core\\CyberSopHarness.Core.csproj --configuration Release -m:1`.
Risk: The corrected build did not yet prove state/evidence behavior.
Next: Build and run the Phase 3 test project.

### P3-TEST-0001: Initial Phase 3 suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Exercise the initial Phase 3 provider, tool, evidence, state, replay, verifier, finding, and report paths.
Observed result: `dotnet tests\\Phase3.Tests\\bin\\Release\\net10.0\\Phase3.Tests.dll` returned `phase3_tests=passed count=8` with eight `PASS` lines.
Evidence: `tests/Phase3.Tests/Program.cs`; direct test-DLL output.
Risk: The initial suite did not prove authority binding, permit one-use at broker dispatch, immutable audit origin, policy-object parity, or several malformed/error cases.
Next: Fix independent review findings and rerun the suite.

### P3-REFACTOR-0001: Broker authority and evidence hardening

Date: 2026-08-17
Event type: Phase 3 security refactor
Actor: Luna/OpenCode
Intent: Prevent the Phase 3 broker from bypassing Phase 2 authorization and prevent replay or forged evidence paths.
Observed result: Added atomic `PermitIssuer.TryClaimConsumed` with expiry/signature/request/manifest/current-policy/approval-hash checks; made workflow audit append internal; made evidence append internal; required fixture-only broker mode, frozen local-safe registries, bound policy/permit checks, provider metadata validation/persistence, bounded output, cleanup, timeout/cancellation/error normalization, nonzero-exit handling, and replay-label recomputation. State transitions now bind run/action identity and verifier/report audit payloads.
Evidence: `src/CyberSopHarness.Core/PermitIssuer.cs`, `Phase3Contracts.cs`, `Phase3Evidence.cs`, `Phase3Runtime.cs`.
Risk: Authorized/live provider containment, egress, hard-stop, durable storage, and external verifier trust remain intentionally unavailable.
Next: Run adversarial Phase 3 tests and Phase 2 regression, then repeat independent review.

### P3-TEST-0002: Hardened Phase 3 suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify permit replay rejection, authorized broker blocking, mutable registry blocking, malformed provider handling, adapter failures, cleanup failures, and all prior Phase 3 paths.
Observed result: `dotnet build tests\\Phase3.Tests\\Phase3.Tests.csproj --configuration Release -m:1` succeeded with `0 Warning(s)` and `0 Error(s)`; direct execution returned `phase3_tests=passed count=8` with eight `PASS` lines.
Evidence: `tests/Phase3.Tests/Program.cs`; build output and direct test-DLL output.
Risk: Phase 2 regression after the latest PermitIssuer changes and independent post-hardening review are still pending.
Next: Run both regression suites and obtain an explicit independent review report.

### P3-REGRESSION-0001: Phase 2 compatibility after broker hardening

Date: 2026-08-17
Event type: Regression verification
Actor: Luna/OpenCode
Intent: Verify that the Phase 3 permit-claim and Phase 2 issuer changes did not regress the existing authorization/containment suite.
Observed result: `dotnet build tests\\Phase2.Tests\\Phase2.Tests.csproj --configuration Release -m:1` succeeded with `0 Warning(s)` and `0 Error(s)`; direct execution returned `phase2_tests=passed count=10` with ten `PASS` lines.
Evidence: Phase 2 build output and `dotnet tests\\Phase2.Tests\\bin\\Release\\net10.0\\Phase2.Tests.dll` output.
Risk: This does not prove the Phase 3 broker/evidence boundary.
Next: Verify the current Phase 3 build and suite, then repeat independent review.

### P3-TEST-0003: Current hardened Phase 3 suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify the current Phase 3 broker authority, provider metadata, one-use permit claim, evidence, state, replay, verifier, finding, and report paths.
Observed result: `dotnet build tests\\Phase3.Tests\\Phase3.Tests.csproj --configuration Release -m:1` succeeded with `0 Warning(s)` and `0 Error(s)`; direct execution returned `phase3_tests=passed count=8` with eight `PASS` lines.
Evidence: Phase 3 build output and `dotnet tests\\Phase3.Tests\\bin\\Release\\net10.0\\Phase3.Tests.dll` output.
Risk: The interrupted independent review still needs an explicit post-hardening report; durable storage, trusted external verification, containment, and live execution remain out of scope.
Next: Obtain a fresh independent Phase 3 review.

### P3-REVIEW-0001: Independent hardened-boundary review

Date: 2026-08-17
Event type: Independent implementation review
Actor: Independent review agent
Intent: Review the broker, permit, evidence, replay, state, verifier, finding, and report boundaries after the first hardening pass.
Observed result: No blocker remained for the exact fixture path, but the review identified high risks: public/overbroad fixture adapters, malformed adapter collections, non-owned artifact references, incomplete finding/report linkage, and cleanup failure potentially bypassing the finding path. Future live-provider containment remained blocked.
Evidence: Independent report returned in the project session; findings referenced `Phase3Contracts.cs`, `Phase3Evidence.cs`, `Phase3Runtime.cs`, `PermitIssuer.cs`, and `tests/Phase3.Tests/Program.cs`.
Risk: Phase 3 acceptance could not be claimed until cleanup and provenance gates were tightened.
Next: Restrict fixture adapters, bind cleanup receipts, normalize malformed collections, bind finding reports, and rerun the suite/review.

### P3-REFACTOR-0002: Fixture provenance and cleanup hardening

Date: 2026-08-17
Event type: Phase 3 security refactor
Actor: Luna/OpenCode
Intent: Close the current fixture-boundary high findings without enabling live execution.
Observed result: Fixture adapter registration now requires an internal friend-assembly marker; tool manifests require raw/redacted/observation evidence, cleanup, read-only behavior, synthetic data, no network destinations, and bounded runtime/output. Cleanup receipts must equal `CLEANUP_OK|<action_hash>`. Null/blank adapter collections are normalized to `ToolError`; custom artifact references are rejected in the local broker; finding transitions require action-hash and report evidence/verifier binding; replay identities use frozen content hashes; provider metadata is asserted in tests.
Evidence: `Phase3Contracts.cs`, `Phase3Runtime.cs`, `Phase3Evidence.cs`, `InternalsVisibleTo.cs`, and `tests/Phase3.Tests/Program.cs`.
Risk: Adapter tasks can still outlive a cooperative timeout because the fixture boundary has no OS-level process termination; live provider use remains blocked.
Next: Build and run Phase 3, then repeat independent review.

### P3-REVIEW-0002: Cleanup/report independent finding

Date: 2026-08-17
Event type: Independent implementation review
Actor: Independent review agent
Intent: Verify the fixture provenance/cleanup changes.
Observed result: No blocker remained. One high remained: failed cleanup was rejected by workflow `OBSERVED` but could still pass through independent verification/finding/report paths. The review also identified missing negative tests for wrong receipts, malformed collections, and direct report-policy denial.
Evidence: Independent report returned in the project session; findings referenced `Phase3Runtime.cs` cleanup, verifier, finding, and report paths.
Risk: A failed-cleanup result could have become reportable through a path that bypassed the workflow state machine.
Next: Require cleanup success in verifier, finding, and report-policy predicates and add a failed-cleanup regression.

### P3-REFACTOR-0003: Cleanup-to-report invariant

Date: 2026-08-17
Event type: Phase 3 security refactor
Actor: Luna/OpenCode
Intent: Ensure failed cleanup cannot become independently verified, reproducible, verified, or reportable evidence.
Observed result: `IndependentFixtureVerifier`, `FindingLifecycle`, and `ReportPolicy.Decide` now require matching run/action/action hash, Success/Partial status, `CleanupResult == SUCCEEDED`, and a typed passed verifier event. The test suite now verifies failed cleanup is rejected by independent verification and reproducible finding transition.
Evidence: `src/CyberSopHarness.Core/Phase3Runtime.cs`; `tests/Phase3.Tests/Program.cs` cleanup failure path.
Risk: No direct test constructs a forged already-Verified finding; state mutation remains internal and supported lifecycle paths reject failed cleanup.
Next: Run final Phase 3 build/test and narrow independent predicate review.

### P3-BUILD-0004: Final Phase 3 build

Date: 2026-08-17
Event type: Build verification
Actor: Luna/OpenCode
Intent: Compile the final fixture adapter, replay, artifact, report, and cleanup-gate changes.
Observed result: `dotnet build tests\\Phase3.Tests\\Phase3.Tests.csproj --configuration Release -m:1` succeeded with `0 Warning(s)` and `0 Error(s)`.
Evidence: Build output; core and Phase 3 release assemblies produced.
Risk: Build success does not prove the independent report predicate.
Next: Execute the final Phase 3 suite and inspect output.

### P3-TEST-0004: Final Phase 3 fixture suite

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Verify final Phase 3 provider, broker, permit, evidence, cleanup, state, replay, verifier, finding, and report behavior.
Observed result: Direct execution of `dotnet tests\\Phase3.Tests\\bin\\Release\\net10.0\\Phase3.Tests.dll` returned `phase3_tests=passed count=8` with eight `PASS` lines.
Evidence: Direct test-DLL output; final build output.
Risk: No live provider, process-level containment, durable storage, or external verifier path was exercised.
Next: Record the narrow independent predicate review and finalize the fixture-only acceptance record.

### P3-REVIEW-0003: Cleanup predicate follow-up

Date: 2026-08-17
Event type: Independent implementation review
Actor: Independent review agent
Intent: Verify the final cleanup-to-report predicate after the high finding fix.
Observed result: No blocker or high finding was identified. The review confirmed report policy now independently checks matching run/action/action hash, Success/Partial status, successful cleanup, and a typed passed verifier event. It noted only that no direct test for a forged already-Verified finding exists; supported lifecycle paths reject failed cleanup.
Evidence: Narrow independent report returned in the project session; current `Phase3Runtime.cs` and `tests/Phase3.Tests/Program.cs` were read.
Risk: Live-provider containment, durable authority, and external verifier trust remain out of scope.
Next: Keep Phase 3 accepted only for deterministic local fixtures and leave live-provider release blocked.

### P3-DOC-0001: Final Phase 3 acceptance reconciliation

Date: 2026-08-17
Event type: Documentation update
Actor: Luna/OpenCode
Intent: Align Phase 3 acceptance, roadmap, phase plan, README, and audit with observed results.
Observed result: `docs/phase3-acceptance.md` now records all ten rows PASS for the documented fixture boundary, build/test evidence `phase3_tests=passed count=8`, and live-provider limitations. `ROADMAP.md`, `docs/phase-plan.md`, `docs/phase3-research.md`, and `README.md` now identify Phase 3 as the current fixture-only phase and keep live execution blocked.
Evidence: Updated Phase 3 and project status documents; P3-REGRESSION-0001 and P3-TEST-0004.
Risk: This is not a production or authorized/live release claim.
Next: Provenance stamps can be added as the next Phase 3 extension, followed by durable evidence and trusted provider work in later gates.

### P3-TEST-0005: Final forged-permit regression

Date: 2026-08-17
Event type: Behavioral acceptance execution
Actor: Luna/OpenCode
Intent: Add and verify an explicit forged-permit object regression after the final independent review.
Observed result: Added a copied-permit test; the broker rejected it before adapter invocation. The final Phase 3 direct-DLL run returned `phase3_tests=passed count=8` with eight `PASS` lines. The final Phase 2 direct-DLL regression returned `phase2_tests=passed count=10` with ten `PASS` lines.
Evidence: `tests/Phase3.Tests/Program.cs`; `dotnet build` outputs with `0 Warning(s)` and `0 Error(s)`; direct Phase 2/Phase 3 test-DLL outputs.
Risk: Live-provider containment, egress, durable evidence, external verifier trust, and non-cooperative process termination remain blocked/out of scope.
Next: Keep Phase 3 accepted only for deterministic local fixtures; implement signed provenance stamps as the next extension if authorized.

### DECISION-0002: Pause provenance for local-model research

Date: 2026-08-17
Event type: Human-directed scope pause
Actor: human
Intent: Pause the signed provenance implementation to evaluate packaging WhiteRabbitNeo as an optional local model alongside user-supplied local models or APIs.
Observed result: Provenance work is paused before code changes. No model files were downloaded, executed, or added. Existing Phase 2/3 fixture acceptance remains unchanged and authorized/live execution remains blocked.
Evidence: Human continuation directive in the project session; current `P3-DOC-0001` and `P3-TEST-0005` records.
Risk: Model redistribution, license compatibility, supply-chain integrity, local runtime resource use, provider privacy, and API egress must be resolved before implementation.
Next: Research WhiteRabbitNeo provenance/license/format/runtime facts, create mandatory local-model skills, and determine the correct roadmap phase.

### MODEL-RESEARCH-0001: WhiteRabbitNeo optional-provider decision

Date: 2026-08-18
Event type: Research and architecture decision
Actor: Luna/OpenCode
Intent: Determine whether WhiteRabbitNeo can be offered as an optional local model alongside user-local models and external APIs.
Observed result: The addition is viable only as an opt-in Phase 3B workstream, not as an unconditional bundled default. The official `WhiteRabbitNeo/WhiteRabbitNeo-13B-v1` model is a 13B Transformers/custom-code model with revision `5ecae8d533223436512e31edc3f56bced00265f1`, `license: llama2`, six PyTorch shards, and approximately 52,066,287,694 bytes of repository storage. Its model card requires `trust_remote_code=True`, so the shipped runtime must not execute its repository Python code.

The researched third-party `QuantFactory/WhiteRabbitNeo-13B-v1-GGUF` inventory reports Q3_K_S at 5,659,083,584 bytes, Q4_0 at 7,365,948,864 bytes, and Q4_K_M at 7,866,070,464 bytes. Q3_K_S is the first hardware probe candidate for this Windows/GTX 1060 6GB/16GB RAM machine; Q4_K_M is a quality candidate but is not approved until RAM/VRAM/latency are measured. The quantizer is not the original publisher, so redistribution/derivative permission must be resolved before bundling.

The official model card includes WhiteRabbitNeo extended restrictions, including lawful use and no military use, in addition to the Llama 2 license label. llama.cpp is MIT licensed, supports GGUF and an OpenAI-compatible local server, and documents loopback binding, offline mode, health, model identity, and disabling optional tools/agent/MCP. The startup path must bind `127.0.0.1`, disable tools/agent/MCP/web UI/prompt logging, verify hashes/identity/health/warmup, and never silently fall back to an external API.
Evidence: `docs/local-model-research.md`; `skills/cyber-local-model-runtime/SKILL.md`; `skills/cyber-model-provider-selection/SKILL.md`; `skills/cyber-model-packaging-supply-chain/SKILL.md`; WhiteRabbitNeo model API/card; QuantFactory/QuantFactory model API/file inventory; llama.cpp server/build/models docs and MIT license.
Risk: No model file, runtime binary, or external API was downloaded or executed. License clearance, quantization provenance, hardware load, performance, and runtime supply-chain verification remain open.
Next: Implement Phase 3B skills/contracts first; then benchmark an approved GGUF locally before any bundling decision. Keep the signed provenance implementation paused until the model/provider contract is settled.

### SKILL-0002: Local model/provider skill set

Date: 2026-08-18
Event type: Skill creation
Actor: Luna/OpenCode
Intent: Create the mandatory implementation guidance requested for the local-model addition.
Observed result: Added three project-local skills, each containing the required `Directive`, `Rationale and Architectural Reason`, and `Threat Matrix` components: `cyber-local-model-runtime`, `cyber-model-provider-selection`, and `cyber-model-packaging-supply-chain`. Updated the skill registry from ten to thirteen skills.
Evidence: `skills/cyber-local-model-runtime/SKILL.md`, `skills/cyber-model-provider-selection/SKILL.md`, `skills/cyber-model-packaging-supply-chain/SKILL.md`, `skills/README.md`.
Risk: Skills define required behavior but do not claim runtime/model success until implementation and hardware tests execute.
Next: Start Phase 3B implementation only after the user authorizes the research decision and the artifact/license gate is explicit.

### PHASE-DECISION-0002: Phase 3B local model workstream

Date: 2026-08-18
Event type: Roadmap decision
Actor: Luna/OpenCode
Intent: Place the optional local model/provider addition in the correct execution phase.
Observed result: Added Phase 3B to `ROADMAP.md`: local model runtime, provider selection, model/runtime manifests, license/hash verification, startup/health/identity/warmup/stop, resource gates, and provider parity. Phase 3B is before Phase 4 and separate from Phase 5 mobile work. It remains local-only and does not authorize live tools.
Evidence: `ROADMAP.md`, `docs/phase-plan.md`, `README.md`, `docs/local-model-research.md`.
Risk: Moving model packaging into Phase 5 or directly into live execution would bypass provider, provenance, resource, and supply-chain gates.
Next: Build the model runtime contract and deterministic fake provider first; perform a real local GGUF load only after legal/provenance manifest approval.

### MODEL-RESEARCH-0002: WhiteRabbitNeo V3 7B correction

Date: 2026-08-18
Event type: Research correction
Actor: human
Intent: Correct the WhiteRabbitNeo 7B repository identity used for model comparison.
Observed result: The researched repository is `bartowski/WhiteRabbitNeo_WhiteRabbitNeo-V3-7B-GGUF`, revision `5cc667f09d00b213c07530c716a0f900dd59f5aa`. Its metadata identifies `WhiteRabbitNeo-V3-7B`, a `DeepHat/DeepHat-V1-7B` / Qwen2.5-Coder-derived model, Apache-2.0 metadata, Qwen2 architecture, and 32,768 context. The Q4_K_M file is `4,683,074,752` bytes, Q5_K_M is `5,444,832,448` bytes, and Q6_K is `6,254,200,000` bytes. The quantizer card states llama.cpp build `b5432` was used.

The model metadata embeds a cybersecurity system prompt saying it always assists and never refuses, and exposes tool-call formatting when tools are supplied. This is a safety concern: our runtime must override the prompt policy and pass no tools, MCP, agent, shell, or filesystem definitions. The model is a strong first hardware candidate, but quality is not yet proven against the official WhiteRabbitNeo 2.5 Qwen-Coder 7B candidate.
Evidence: `https://huggingface.co/api/models/bartowski/WhiteRabbitNeo_WhiteRabbitNeo-V3-7B-GGUF`; its README and GGUF tree; `https://huggingface.co/api/models/WhiteRabbitNeo/WhiteRabbitNeo-2.5-Qwen-2.5-Coder-7B`.
Risk: Third-party quantizer provenance, model-card restrictions, embedded refusal behavior, prompt-template control, and local performance remain open. The exposed HF token was not used or stored.
Next: Treat V3 Q4_K_M as the first benchmark candidate, compare Q4_K_M/Q5_K_M against the official 2.5 Qwen-Coder candidate, and require a custom safety chat template plus policy-path tests before approval.

### P3B-PROBE-0001: Local model runtime environment

Date: 2026-08-18
Event type: Environment verification
Actor: Luna/OpenCode
Intent: Check whether the machine already has a usable llama.cpp runtime/model before implementing or downloading anything.
Observed result: `llama-server.exe`, `llama-cli.exe`, and `llamacpp-server.exe` were not found. `nvidia-smi` reported `NVIDIA GeForce GTX 1060`, `6144 MiB`, driver `582.28`. `dotnet --version` returned `10.0.300`; `py --version` returned Python `3.13.14`; the `python` alias was unavailable. Drive E had approximately `91,380,826,112` bytes free. No GGUF model artifact exists under the project tree.
Evidence: `where.exe` runtime probes; `nvidia-smi --query-gpu=name,memory.total,driver_version --format=csv,noheader`; `dotnet --version`; `py --version`; `Get-PSDrive`; project glob.
Risk: No approved runtime/model can be loaded yet. Python 3.13 is not assumed compatible with the required model/runtime path; no download or execution was attempted.
Next: Implement the manifest/provider contracts and launcher tests first; then select a pinned Windows llama.cpp binary and approved Q4_K_M artifact for a real load gate.

### P3B-DOWNLOAD-0001: Authenticated HF download blocked

Date: 2026-08-18
Event type: Download safety event
Actor: Luna/OpenCode
Intent: Use the official Hugging Face client/Xet path to stage the V3 Q4_K_M model on E:.
Observed result: The raw curl/aria2 transfer was stopped after repeated Xet CDN interruptions. `huggingface_hub` 1.27.0 and `hf_xet` are installed in `E:\pip_temp\opencode\hf-venv`, but no local `HF_TOKEN` is configured. The public client warned that unauthenticated requests are rate-limited. The exposed token from the conversation was not used or stored.
Evidence: Hugging Face official download documentation; `hf_xet=True` probe; `HF_TOKEN_NOT_CONFIGURED`; partial staged artifact at `E:\pip_temp\opencode\wrn-v3-7b` is not valid until its expected size/hash is complete.
Risk: Do not use the exposed token. The model is not yet approved, complete, or runnable.
Next: Human must revoke the exposed token and configure a fresh `HF_TOKEN` locally without sending it through chat; then resume `hf_hub_download` into the project model directory with the pinned revision.

### P3B-RUNTIME-0001: Runtime/model staging and load gate

Date: 2026-08-18
Event type: Phase 3B runtime verification
Actor: Luna/OpenCode
Intent: Stage the pinned V3 Q4_K_M model and llama.cpp runtime on E: and attempt the first safe synthetic load.
Observed result: Authenticated `huggingface_hub`/`hf_xet` verification completed the model at `E:\cyber-sop-harness\models\WhiteRabbitNeo-V3-7B-GGUF\WhiteRabbitNeo_WhiteRabbitNeo-V3-7B-Q4_K_M.gguf`; size is `4,683,074,752` bytes and SHA-256 is `584bfc1f4c160928842866c566129f9789c4671af8e51a9e36ba0ebf10f24f41`. The pinned llama.cpp `b10488` CPU runtime and MIT license are staged under `runtime/llama.cpp/b10488`; runtime version is `0.1.2-dev (build 10488, commit 9d77fa172)` and its executable hash is `3e2ac8887fb37fa4312654cf6625e3449a7e87989b0116c4a582fd518d02cf2f`. The first smoke script failed because PowerShell async event handlers had no runspace; the script was corrected to use llama.cpp `--log-file`. A safe retry is currently blocked by host state: 24 pre-existing llama-server processes use approximately 2.44 GB working set, only approximately 3.58 GB physical RAM is free, and port `18080` is free.
Evidence: Project model/runtime paths; HF revision/file hash; llama.cpp release API digest `6c938f...` for the CPU zip; runtime `--version`; GPU probe; memory/process/port probes; `run_wrn_v3_smoke.ps1`.
Risk: The model has not yet been successfully loaded or run. Launching it now could page heavily or disrupt active user model services. No existing process was killed or modified.
Next: Human should free sufficient RAM or explicitly authorize a measured load while existing model services are stopped; then run the corrected synthetic `READY` smoke test and record latency/RAM/VRAM/clean shutdown.

### P3B-DOWNLOAD-0002: Verified model and runtime staged in project

Date: 2026-08-18
Event type: Artifact verification
Actor: Luna/OpenCode
Intent: Complete the approved local evaluation staging on the project E: drive.
Observed result: The authenticated `huggingface_hub`/`hf_xet` client completed the exact V3 Q4_K_M file into the project cache; the staged model root copy is `E:\cyber-sop-harness\models\WhiteRabbitNeo-V3-7B-GGUF\WhiteRabbitNeo_WhiteRabbitNeo-V3-7B-Q4_K_M.gguf`. Size is `4,683,074,752` bytes and SHA-256 is `584bfc1f4c160928842866c566129f9789c4671af8e51a9e36ba0ebf10f24f41`. The pinned llama.cpp `b10488` CPU runtime was copied under `E:\cyber-sop-harness\runtime\llama.cpp\b10488`; its executable hash is `3e2ac8887fb37fa4312654cf6625e3449a7e87989b0116c4a582fd518d02cf2f`. Added `MODEL-NOTICE.md` and `MODEL-RUNTIME-MANIFEST.json` with source, revision, hashes, restrictions, runtime, and safety template.
Evidence: Project model/runtime files; HF revision and authenticated client output; `Get-FileHash`; runtime `--version`; `models/WhiteRabbitNeo-V3-7B-GGUF/MODEL-RUNTIME-MANIFEST.json`.
Risk: Redistribution approval and measured resource telemetry remain open. The model is staged for local evaluation, not release bundling.
Next: Run the corrected real smoke test and record model identity, deterministic output, timing, and shutdown.

### P3B-RUNTIME-0002: Real V3 Q4_K_M smoke test

Date: 2026-08-18
Event type: Real runtime verification
Actor: Luna/OpenCode
Intent: Load and run the staged WhiteRabbitNeo V3 Q4_K_M model through the pinned llama.cpp server using the project safety posture.
Observed result: The server started CPU-only on `127.0.0.1:18080` with offline mode, web UI disabled, agent disabled, no tools flag, and the project-owned safe chat template. `/health` passed; `/v1/models` returned the stable alias `wrn-v3-7b-q4-k-m`; the synthetic prompt returned exactly `READY`; measured elapsed time was `9.148` seconds; the smoke harness stopped the process and port `18080` was released.
Evidence: `E:\pip_temp\opencode\run_wrn_v3_smoke.ps1` output: `MODEL_ID=wrn-v3-7b-q4-k-m`, `COMPLETION=READY`, `ELAPSED_SECONDS=9.148`, `SMOKE=passed`; `wrn-v3-llama.log`; process/port post-check.
Risk: This is a CPU-only smoke/warmup result, not a full performance benchmark. RAM peak, VRAM behavior, long-context stability, output quality, and release redistribution remain unverified.
Next: Measure resource usage and latency across Q4_K_M/Q5_K_M candidates; preserve the model’s embedded prompt as untrusted and keep the custom safety template mandatory.

### P3B-BUG-0001: Pinned runtime flag correction

Date: 2026-08-18
Event type: Runtime failure and fix
Actor: Luna/OpenCode
Intent: Correct the smoke/launcher arguments to the actual llama.cpp `b10488` interface.
Observed result: The first direct launch failed with `error: invalid argument: --no-tools`; the pinned binary disables tools by omission rather than a `--no-tools` flag. Removed that unsupported flag from `LocalModelRuntime`, the smoke script, and research instructions. The subsequent real smoke test passed with `READY` and the stable alias.
Evidence: llama.cpp `--help`; `src/CyberSopHarness.Core/LocalModelRuntime.cs`; `E:\pip_temp\opencode\run_wrn_v3_smoke.ps1`; P3B-RUNTIME-0002.
Risk: Future llama.cpp revisions may change flags; the runtime manifest must keep the binary revision pinned and verify startup behavior.
Next: Keep the exact runtime revision and test its supported help flags during packaging.

### P3B-RUNTIME-0003: Cold reload telemetry gap

Date: 2026-08-18
Event type: Runtime verification failure
Actor: Luna/OpenCode
Intent: Capture peak working-set telemetry on a second real Q4_K_M load.
Observed result: The telemetry smoke harness started the pinned CPU runtime and reached model loading, but did not reach `/health` within 180 seconds; the harness stopped its exact project process and port `18080` remained free. No runtime error was logged beyond model loading and tokenizer metadata warning. The prior warm smoke remains valid: `READY` in `9.148` seconds.
Evidence: `run_wrn_v3_smoke.ps1`; `wrn-v3-llama.log`; post-run process/port check.
Risk: Cold/reload latency and resource behavior are not stable enough for a packaged-default claim. No RAM peak was recorded from the failed telemetry attempt.
Next: Benchmark with controlled cold-start conditions, lower context, and Q4/Q5 comparison; keep the model optional and do not claim reliable startup until repeated cold runs pass.

### P3B-REFACTOR-0001: Runtime launch alignment

Date: 2026-08-18
Event type: Phase 3B runtime fix
Actor: Luna/OpenCode
Intent: Align `LocalModelRuntime` with the proven CPU smoke posture and stabilize model identity.
Observed result: Added explicit CPU-only `--n-gpu-layers 0`, six threads, eight-token prediction cap, 180-second cold readiness window, and `--alias wrn-v3-7b-q4-k-m`. The unsupported `--no-tools` flag had already been removed after direct help verification; tools remain disabled by omission. The project-owned safe chat template remains mandatory.
Evidence: `src/CyberSopHarness.Core/LocalModelRuntime.cs`, `runtime/llama.cpp/b10488/safe-chat-template.jinja`, Phase 3B build/test output.
Risk: CPU-only settings are the current measured path; GPU offload and Q5 comparison are not yet benchmarked.
Next: Run the opt-in project-owned real runtime test.

### P3B-RUNTIME-0004: Project-owned real runtime success

Date: 2026-08-18
Event type: Real runtime verification
Actor: Luna/OpenCode
Intent: Verify the project `LocalModelRuntime` service itself, not only the standalone smoke script.
Observed result: With `PHASE3B_REAL_MODEL=1`, the Phase 3 suite passed all ten contract tests plus `real Phase 3B local model runtime`; output ended `phase3_tests=passed count=11`. The service loaded the staged Q4_K_M model, passed `/health`, verified `/v1/models` alias `wrn-v3-7b-q4-k-m`, completed warmup, stopped the process tree, and released port `18080`.
Evidence: `dotnet build tests\\Phase3.Tests\\Phase3.Tests.csproj --configuration Release -m:1`; `PHASE3B_REAL_MODEL=1 dotnet tests\\Phase3.Tests\\bin\\Release\\net10.0\\Phase3.Tests.dll`; post-run `PORT_18080_RELEASED`.
Risk: The separate cold telemetry rerun timed out at 180 seconds, so repeated startup and resource ceilings are not yet stable enough for a packaged-default claim. Redistribution approval and model-output policy/provenance integration remain open.
Next: Keep the model as local evaluation-only; measure RAM/VRAM/Q5 and integrate provenance/policy before any release decision.

### P3B-PROVIDER-0001: Real local proposal adapter success

Date: 2026-08-18
Event type: Real provider integration verification
Actor: Luna/OpenCode
Intent: Verify that the staged local model can produce a typed proposal through the project provider adapter without receiving tools or authorization authority.
Observed result: The opt-in real Phase 3B test started the project runtime, called the local OpenAI-compatible endpoint, received a JSON-only synthetic `ACTION_REQUEST`, passed `ActionRequestValidator`, and stopped the runtime. The full opt-in suite returned `phase3_tests=passed count=13`.
Evidence: `LocalModelProviderAdapter`, `ActionProposalParser`, `tests/Phase3.Tests/Program.cs` real-model path; `PHASE3B_REAL_MODEL=1 dotnet tests\\Phase3.Tests\\bin\\Release\\net10.0\\Phase3.Tests.dll` output.
Risk: The proposal has not yet been passed through a complete live `PolicyEngine -> PermitIssuer -> ToolBroker -> Evidence -> Provenance` integration test. The model remains local-evaluation-only.
Next: Integrate the real provider proposal into the existing policy/permit/evidence/provenance path and test rejection of out-of-scope or malformed model output.

### P3B-PROVENANCE-0001: Signed model/evidence provenance

Date: 2026-08-18
Event type: Phase 3B security implementation
Actor: Luna/OpenCode
Intent: Bind model/evidence results and project downloads to a signed product/build/provenance identity.
Observed result: Added `ProvenanceAuthority`, `ProvenanceStamp`, signed release manifests, visible provenance rendering, and broker integration. Normal broker outcomes now carry a stamp bound to product/build, run/action, authorization/scope, policy, provider/model, tool, evidence event/hash, and raw/redacted hashes. Tampering fails verification; signed release manifests verify downloaded file entries.
Evidence: `src/CyberSopHarness.Core/Provenance.cs`, `Phase3Runtime.cs`, `tests/Phase3.Tests/Program.cs` provenance/release assertions.
Risk: The real model proposal is not yet sent through a complete policy/permit/broker/provenance end-to-end action; durable key custody and release-key rotation remain future release work.
Next: Run Phase 2 regression, then add the real-proposal-to-policy integration gate without granting tool authority to the model.

### P3B-INTEGRATION-0001: Real proposal through policy and provenance

Date: 2026-08-18
Event type: End-to-end local integration verification
Actor: Luna/OpenCode
Intent: Prove the real V3 model proposal cannot bypass the project policy/permit/broker/evidence/provenance chain.
Observed result: The opt-in real test loaded the Q4_K_M model, received a validated synthetic `ACTION_REQUEST`, evaluated it through `PolicyEngine`, consumed a one-use `PermitIssuer` permit, dispatched only the registered synthetic fixture adapter, appended evidence, and verified the signed provenance stamp. The test suite returned `phase3_tests=passed count=13`.
Evidence: `tests/Phase3.Tests/Program.cs` real-model path; `LocalModelProviderAdapter`; `ToolBroker`; `ProvenanceAuthority`; direct `PHASE3B_REAL_MODEL=1` output.
Risk: The path is loopback/local-fixture only. Q5 comparison, RAM/VRAM telemetry, external APIs, real tools, live targets, and redistribution approval remain blocked or pending.
Next: Keep Q4 as local-evaluation candidate; complete resource/licensing gates before packaging and do not enable live execution.

### P3B-PROVIDER-0002: Provider selection persistence

Date: 2026-08-18
Event type: Phase 3B implementation and test
Actor: Luna/OpenCode
Intent: Add the startup-selection persistence contract without storing API keys or leaking secrets.
Observed result: Added `ModelProviderSelectionStore` with validation, atomic temporary-file replacement, stream disposal, and metadata-only persistence. Added a regression asserting local/API selection rules and safe reload. The first test exposed an undisposed file stream; `LoadAsync` now disposes it correctly. Phase 3B contract suite returned `phase3_tests=passed count=13`.
Evidence: `src/CyberSopHarness.Core/ModelProviderSelection.cs`, `tests/Phase3.Tests/Program.cs` selection test, final build/output.
Risk: UI wiring and platform secure-secret stores remain unimplemented; external API use remains opt-in.
Next: Keep selection service behind the future startup UI and integrate it with signed runtime manifests.

### P3B-INTEGRATION-0002: Final real Phase 3B regression

Date: 2026-08-18
Event type: Real runtime regression verification
Actor: Luna/OpenCode
Intent: Re-run the full Phase 3B suite after provider-selection and provenance changes.
Observed result: With `PHASE3B_REAL_MODEL=1`, all contract, parser, selection, provider, runtime, policy, permit, broker, evidence, and provenance tests passed; output was `phase3_tests=passed count=14`. The model runtime stopped cleanly after the test.
Evidence: Direct release-DLL output; `tests/Phase3.Tests/Program.cs`; model/runtime manifest.
Risk: Q5 comparison, RAM/VRAM telemetry beyond CPU working set, redistribution approval, UI startup wizard, external API integration, and live-provider containment remain open.
Next: Keep Q4 local-evaluation-only and resolve the remaining release gates before bundling or enabling external providers.

### P3B-BENCH-0001: Q4_K_M controlled cold benchmark

Date: 2026-08-18
Event type: Hardware benchmark
Actor: Luna/OpenCode
Intent: Measure repeatability and peak memory for the staged Q4_K_M model under the constrained CPU-only launch posture.
Observed result: Three cold runs passed health, model identity, synthetic warmup, and cleanup. Readiness times were `122265 ms`, `83666 ms`, and `87711 ms`; total times were `125319 ms`, `86321 ms`, and `90750 ms`; peak working sets were `4694564864`, `4694679552`, and `4694675456` bytes. The successful benchmark required `--no-repack`; without it llama.cpp failed allocating a `3121348608` byte CPU_REPACK buffer.
Evidence: `E:\pip_temp\opencode\wrn-q4-benchmark.jsonl`; per-run llama logs; `benchmark_wrn_q4.ps1`; Q4 manifest.
Risk: This is CPU-only; VRAM offload, Q5 quality/performance, and repeated cold stability under different host memory states remain open.
Next: Use Q4_K_M as the current optional local candidate; do not bundle by default until provenance/license and policy integration gates pass.

### P3B-BENCH-0002: Q5_K_M comparison blocked by host memory

Date: 2026-08-18
Event type: Benchmark artifact failure
Actor: Luna/OpenCode
Intent: Download Q5_K_M through authenticated `huggingface_hub`/Xet for the planned quality comparison.
Observed result: The Xet client failed with Rust `MemoryError`; the non-Xet HTTP fallback also failed with Python `MemoryError` after approximately `951701972` bytes. The invalid partial Q5 directory was deleted. Q4 remains complete and verified; no Q5 artifact remains in the project.
Evidence: Q5 downloader stderr logs; HF file metadata expected size `5444832448` and LFS hash `6d7c235f2e79bc65c9cb4478b4460a8efdd1da13dd5185b49788693e1824bd58`; project model-directory listing.
Risk: The host cannot currently complete a Q5 comparison through the available transfer paths without memory/resource changes. This does not invalidate Q4 load/run evidence.
Next: Keep Q5 optional; prioritize model-output policy/provenance integration and consider a lower-memory Q5 acquisition path later.

### QUERY-0002: Research-defined Phase 3B next step

Date: 2026-08-18
Event type: Roadmap guidance
Actor: Luna/OpenCode
Intent: Answer the human question about the next research-required action after the first successful real model load.
Observed result: The next step is not bundling or API fallback. Research and `docs/phase3b-acceptance.md` require a controlled resource/startup benchmark first: repeated cold Q4_K_M loads, peak RAM/VRAM, latency/tokens-per-second, context-budget behavior, clean stop, and comparison with Q5_K_M. The observed 180-second cold reload timeout keeps P3B-006/P3B-008 open. After that, integrate model output into the typed provider/action/policy/permit/evidence/provenance path, then resolve redistribution/license approval before any bundled default.
Evidence: `docs/phase3b-acceptance.md`; `docs/local-model-research.md`; `skills/cyber-local-model-runtime/SKILL.md`; `skills/cyber-model-provider-selection/SKILL.md`; `skills/cyber-model-packaging-supply-chain/SKILL.md`; P3B-RUNTIME-0003 and P3B-RUNTIME-0004.
Risk: Calling the model a packaged default before cold-start/resource/license gates pass would overstate readiness and could destabilize the host or create an unapproved redistribution obligation.
Next: Run the controlled Q4/Q5 benchmark and diagnose the cold-start timeout while keeping the model local-evaluation-only.

### SKILL-0003: Gap skills for durable evidence, key custody, and secret custody

Date: 2026-08-19
Event type: Skill creation
Actor: Luna/OpenCode
Intent: Close the three research-identified gaps with project-local skills so future sessions apply the researched behavior without guessing.
Observed result: Created three skills, each containing the required Directive, Rationale and Architectural Reason, and Threat Matrix components: cyber-durable-evidence-persistence (append-only, hash-chained, crash-safe persistence modeled on SQLite atomic-commit semantics), cyber-provenance-key-custody (SLSA-aligned signing-key custody, separation of runtime vs release keys, canonical-payload signing, rotation, offline verification), and cyber-secret-custody (Windows DPAPI ProtectedData/Credential Manager custody, metadata-only selection store, load-at-execution, redaction, consent/egress gating, rotation/clearing). Updated the skill registry from thirteen to sixteen skills in skills/README.md and the phase table in ROADMAP.md; Phase 3B status updated to in progress.
Evidence: skills/cyber-durable-evidence-persistence/SKILL.md, skills/cyber-provenance-key-custody/SKILL.md, skills/cyber-secret-custody/SKILL.md, skills/README.md, ROADMAP.md.
Risk: Skills define required behavior and are not implemented yet; persistence, key, and secret code paths remain to be built and verified.
Next: Implement durable evidence persistence, key custody, and DPAPI secret custody in the harness, then resume the Phase 3B startup provider-selection wizard surfacing ProviderDisclosure fields (provider/model/version, source, license status, data path, retention warning, resource estimate, OFFLINE/LOCAL/EXTERNAL status).

### P3B-INTEGRATION-0003: Durable evidence, secret custody, key custody, and wizard implemented

Date: 2026-08-19
Event type: Implementation
Actor: Luna/OpenCode
Intent: Implement the three research-defined gap subsystems (durable evidence persistence, Windows secret custody, provenance key custody) and the Phase 3B startup provider-selection wizard core, then verify the full battery.
Observed result: Added DurableEvidenceStore.cs (append-only JSONL journal, per-record fsync via WriteThrough + Flush(true), double-chain verification of record and internal event/audit hashes, VERIFIED/PARTIAL/CORRUPT recovery, torn-tail truncation, durable artifact store with hash re-verification), SecretProtector.cs (ISecretProtector, Windows DPAPI ProtectedData CurrentUser + per-context entropy via System.Security.Cryptography.ProtectedData 9.0.0, AES-GCM test protector, PersistentSecretStore with traversal rejection and tamper detection), ProvenanceKeyCustody.cs (role-separated RuntimeEvidence/Release keys, protected PKCS8 at rest, single-line base64 retired-key archive, ReleaseSigningAuthority, offline public-key verification), and ProviderWizard.cs (three-choice disclosure flow rendering OFFLINE/LOCAL/EXTERNAL, explicit egress acknowledgement, no-secret-fallback rejection, selection invalidation binding). ProvenanceAuthority now binds ReleaseKeyRef to the key fingerprint, verifies stamps under retired keys by key-ref matching, and exposes static offline verifiers. Fixes during verification: per-append FileStream (ReadAllBytes sharing violation), retired-key archive line format, CA1416 platform guards, and test identity fingerprint binding. Battery: Phase 2 phase2_tests=passed count=10; Phase 3 phase3_tests=passed count=17; opt-in real model count=18; builds 0 warnings/0 errors. Docs updated: docs/phase3b-acceptance.md rows P3B-012..P3B-015 PASS, ROADMAP.md deliverables, README.md skill count.
Evidence: src/CyberSopHarness.Core/DurableEvidenceStore.cs, SecretProtector.cs, ProvenanceKeyCustody.cs, ProviderWizard.cs, modified Provenance.cs, 	ests/Phase3.Tests/Program.cs; direct test-run output.
Risk: DPAPI wrong-entropy test is Windows-specific; persistent key/secret stores are new and have not yet been wired into the real-model runtime path or a UI shell.
Next: Wire durable journaling and DPAPI secret loading into the real provider/runtime path, then implement the wizard UI shell (CLI) and external API provider behind explicit consent.

### P3B-INTEGRATION-0004: Runtime journal mirror, consent-gated external provider, and CLI wizard shell

Date: 2026-08-19
Event type: Implementation
Actor: Luna/OpenCode
Intent: Wire durable persistence into the running broker/state path, add the external API provider behind explicit consent, and deliver the CLI wizard shell.
Observed result: EvidenceLedger and WorkflowAuditLog now accept an optional DurableEvidenceJournal and mirror every appended event/audit entry with artifact persistence; ExternalApiProviderAdapter (new ExternalApiProvider.cs) is consent-gated (no consent, stale consent, missing provider secret, or different vault yields PolicyBlocked with zero network requests), sends the secret only in the Authorization header, and parses completions through the strict ActionProposalParser; CyberSopHarness.App CLI added with setup (renders disclosures, requires egress ack, persists selection and binds the previous selection), status (selection + OFFLINE/LOCAL/EXTERNAL egress), and secret set|clear|has (hidden stdin input, DPAPI store). Verified with a loopback TcpListener fixture that asserts the secret never appears in the request body and arrives only as Bearer <secret>. Battery: Phase 2 phase2_tests=passed count=10; Phase 3 phase3_tests=passed count=19; opt-in real model count=20; builds 0 warnings/0 errors. CLI smoke: empty data dir reports NO_SELECTION; external choice invisible until a secret exists; real staged model selected via setup persisted selection_237f25eeeaeb47e89cd3b88e45a28469 with EGRESS LOCAL and no external egress. Docs updated: docs/phase3b-acceptance.md rows P3B-016/P3B-017 PASS, ROADMAP.md Phase 3B status.
Evidence: src/CyberSopHarness.Core/ExternalApiProvider.cs, modified Phase3Evidence.cs and DurableEvidenceStore.cs, src/CyberSopHarness.App/Program.cs; direct test-run and CLI smoke output; E:\cyber-sop-harness\data\provider-selection.json.
Risk: The external adapter is consent-gated but has not been exercised against a real remote endpoint; secret set reads only from the console, not piped stdin; the CLI disclosure for local models uses a generic resource estimate rather than the pinned runtime manifest.
Next: Wire the persisted selection into the real start path (read selection, load manifest, start runtime, check policy compatibility before readiness), then drive the external provider through the full policy/permit/evidence path with a real approved endpoint.

### P3B-INTEGRATION-0005: Persisted selection drives the real start path (bootstrapper, loopback adapter, CLI run)

Date: 2026-08-19
Event type: Implementation
Actor: Luna/OpenCode
Intent: Wire the persisted provider selection into the real start path: load the selection, resolve and validate the staged runtime manifest, start the pinned runtime, reach readiness only after health/identity/warmup gates, then drive every provider kind through the same session surface and expose it from the CLI.
Observed result: Added RuntimeBootstrapper.cs (HarnessBootstrapper + RuntimeSession): VerifiedLocal and UserLocal-with-path start a validated managed runtime (manifest hashes, resource limits, absolute paths) and return a session whose provider is the LocalModelProviderAdapter; UserLocal-without-path requires a loopback endpoint and yields a LoopbackEndpointProviderAdapter; ExternalApi requires recorded consent and a stored secret or fails closed. Bootstrapper converts startup failures (missing selection, missing manifest, tampered model, missing consent/secret, non-loopback user-local endpoint, readiness timeout) into controlled InvalidOperationException. Added EndpointGuard.IsLoopback + LoopbackEndpointProviderAdapter (no auth header, strict ActionProposalParser, PolicyBlocked class on non-loopback) to ExternalApiProvider.cs. Added StagedModelCatalog.LoadAsync to translate the staged MODEL-RUNTIME-MANIFEST.json (relative paths, model_bytes) into validated ModelRuntimeManifest keyed by model directory. CLI gained run [--port <n>]: loads selection, builds catalog, starts the session, issues one synthetic typed probe, prints READY/PROBE/STOPPED. LocalModelRuntime now accepts a readiness budget (bootstrapper uses 420s vs 180s default) and the CLI catches TimeoutException cleanly. TestRealModelRuntime was refactored to start through the bootstrapper with a persisted selection, and TestHarnessBootstrapper covers the fail-closed matrix plus a loopback TCP fixture. Battery: Phase 2 phase2_tests=passed count=10; Phase 3 phase3_tests=passed count=20; opt-in real model through bootstrapper count=21; builds 0 warnings/0 errors. CLI live smoke with the real staged model: READY selection_237f25eeeaeb47e89cd3b88e45a28469 WhiteRabbitNeo-V3-7B-GGUF kind=UserLocal; PROBE None action=cli-probe-action latency=54.0s tokens=313; STOPPED; no llama-server processes left after stop. Two CLI runs under host load exposed a 180s readiness timeout; fixed with the 420s budget and controlled conversion. Docs updated: docs/phase3b-acceptance.md rows P3B-018/P3B-019 PASS.
Evidence: src/CyberSopHarness.Core/RuntimeBootstrapper.cs, StagedModelCatalog.cs, ExternalApiProvider.cs (EndpointGuard, LoopbackEndpointProviderAdapter), LocalModelRuntime.cs (readiness budget), src/CyberSopHarness.App/Program.cs (run command), tests/Phase3.Tests/Program.cs; direct test-run and CLI run output.
Risk: External selection can still only be smoke-verified against loopback fixtures until a real approved endpoint exists; the loopback path has not been driven through the full policy/permit/evidence/provenance broker path; readiness budget 420s is a tuned constant, not measured telemetry.
Next: Drive the consent-gated external/loopback provider through the full ActionEnvelope -> policy -> permit -> broker -> evidence -> provenance path in a test, then continue with the remaining Phase 3B blockers (VRAM telemetry, Q5 comparison, redistribution approval).

### P3B-INTEGRATION-0006: External provider driven through the full policy/permit/evidence/provenance path

Date: 2026-08-19
Event type: Implementation
Actor: Luna/OpenCode
Intent: Prove the consent-gated external provider is not a second-class path: a proposal arriving over HTTP must traverse the identical chain as the local model (proposal -> policy -> one-use permit -> broker -> evidence -> signed provenance).
Observed result: Added TestExternalProviderBrokerPath to the Phase 3 suite. A loopback TcpListener fixture serves an OpenAI chat-completion envelope; the bootstrapper starts an ExternalApi session from a persisted selection with recorded consent and a stored secret; the proposal is asserted valid and the secret appears only in the Authorization header (never the body); then policy Allow, one-use permit issue/consume, frozen fixture tool registry, broker dispatch, evidence Success, and provenance verification all pass for the external action. Battery: Phase 2 phase2_tests=passed count=10; Phase 3 phase3_tests=passed count=21; builds 0 warnings/0 errors. Docs updated: docs/phase3b-acceptance.md row P3B-020 PASS.
Evidence: tests/Phase3.Tests/Program.cs (TestExternalProviderBrokerPath); direct test-run output.
Risk: The external path is proven against a loopback fixture; a real approved remote endpoint still needs to be configured and exercised once, and its egress consent/retention must be documented before any live use.
Next: The remaining Phase 3B blockers are VRAM telemetry (P3B-006), Q5 comparison (P3B-008), and redistribution/license approval (P3B-009). No further implementation path is blocked on code; decide whether to pursue telemetry/Q5 or move to Phase 3C planning.

### P3B-INTEGRATION-0007: Q5_K_M staged, telemetry wired, paired Q4/Q5 measurement closes P3B-006 and P3B-008

Date: 2026-08-19
Event type: Implementation
Actor: Luna/OpenCode
Intent: Close the remaining measurable Phase 3B blockers: stage the Q5_K_M candidate (P3B-008), add honest RAM/VRAM/latency telemetry (P3B-006), and run a paired Q4 vs Q5 comparison on the host.
Observed result: Q5_K_M download (5,444,832,448 bytes, SHA-256 6d7c235f2e79bc65c9cb4478b4460a8efdd1da13dd5185b49788693e1824bd58, GGUF magic verified) succeeded via a hand-rolled resumable HTTP streaming downloader after Xet proved unreliable (two MemoryErrors and an interrupted 2.3 GB blob that never finalized; the blob content hash matched the final file hash, confirming content-addressing). Added ResourceTelemetry.cs (working-set via process, VRAM/GPU via nvidia-smi with graceful fallback) and CLI run --telemetry; LocalProcessId exposed on RuntimeSession. Q5 initially failed the strict proposal gate: it wraps the ACTION_REQUEST in markdown code fences (deterministic at temperature 0). Added ProposalTextNormalizer.StripOuterCodeFence (strips an outer fence only when it wraps the entire output with no trailing prose; anything else is passed to the strict parser unchanged) used by all three adapters, plus a hardened system prompt ("never use markdown code fences; output only the bare JSON object"). Paired telemetry runs (same probe, same pinned binary b10488, back-to-back): Q4 ready 4.37 GiB / probe peak 4.42 GiB / 59.8s / 361 tokens; Q5 ready 5.02 GiB / probe peak 5.06 GiB / 67.6s / 361 tokens; VRAM 435 MiB/6.00 GiB in both (CPU-only -ngl 0; model uses zero VRAM, GPU desktop baseline only). Q5 costs ~0.64 GiB more RAM and ~8s more per probe for no gate benefit; Q4 selection restored as active. Battery: Phase 3 phase3_tests=passed count=21 (normalizer cases folded into TestModelProposalParsing); builds 0 warnings/0 errors. Docs updated: phase3b-acceptance.md P3B-006/P3B-008 PASS, P3B-021 PASS (fence normalization), evidence and safety-boundary sections.
Evidence: models/WhiteRabbitNeo-V3-7B-Q5_K_M/ (gguf + MODEL-NOTICE.md + MODEL-RUNTIME-MANIFEST.json), src/CyberSopHarness.Core/ResourceTelemetry.cs, ProposalTextNormalizer in LocalModelProvider.cs, adapter system-prompt hardening, src/CyberSopHarness.App/Program.cs (--telemetry), E:\pip_temp\opencode\download_wrn_v3_q5_stream.py + q5_diag.ps1, direct CLI run output.
Risk: The Q5 readiness/probe numbers come from a single paired session each (temperature 0, deterministic sampling, but host load varies); the fence normalizer is a presentation-layer accommodation, so the strict parser remains the gate; redistribution approval (P3B-009) is still the only open Phase 3B blocker and is a human decision.
Next: P3B-009 remains blocked on license/redistribution approval for WhiteRabbitNeo (Apache-2.0 metadata + extended restrictions). Otherwise Phase 3B deliverables are complete; decide whether to plan Phase 3C (release packaging/UI) or start an authorized-live-endpoint exercise for the external provider.

### P3B-INTEGRATION-0008: External endpoint configuration surface (endpoint set/clear/show)

Date: 2026-08-19
Event type: Implementation
Actor: Luna/OpenCode
Intent: Remove the hardcoded https://api.invalid/v1 external endpoint so an authorized live endpoint can actually be configured, disclosed, selected, and run through the CLI.
Observed result: Added ExternalEndpointStore.cs in Core: ExternalEndpointValidator enforces absolute URI, https (any host) or http (loopback only), no embedded credentials, no query/fragment; ExternalEndpointStore persists a non-secret JSON file (external-endpoint.json) with load/save/clear. CLI gained endpoint set|clear|show; status prints the ENDPOINT line; BuildDisclosuresAsync now shows the external choice only when BOTH an endpoint is configured AND a secret exists, and carries the real endpoint into the selection so the bootstrapper's StartExternal uses it. Fail-closed behavior verified by CLI smoke: non-loopback http rejected with a clear reason, external setup without a stored secret reports provider choice not available, status reflects the endpoint, clear removes it. Battery: Phase 3 phase3_tests=passed count=22 (TestExternalEndpointStore covers the validation matrix and round trip); builds 0 warnings/0 errors.
Evidence: src/CyberSopHarness.Core/ExternalEndpointStore.cs, src/CyberSopHarness.App/Program.cs, tests/Phase3.Tests/Program.cs, docs/phase3b-acceptance.md P3B-022; direct CLI smoke output.
Risk: The endpoint is configuration, not proof of authorization: consent for a specific remote still depends on the operator's written authorization and the egress acknowledgement captured during setup; http loopback endpoints are allowed by design for local test servers.
Next: Phase 3B code surface is complete. To exercise the external provider against a real approved endpoint: endpoint set <url>, secret set external-api, setup --provider external-api --ack-egress yes, run. Remaining open item is P3B-009 redistribution approval (human decision).

### P3B-INTEGRATION-0009: CLI run now executes the governed chain and persists durable signed evidence

Date: 2026-08-19
Event type: Implementation
Actor: Luna/OpenCode
Intent: Close the gap between the library-proven governed chain (policy -> permit -> broker -> evidence -> provenance -> workflow lifecycle) and the actual product surface: a real CLI run must produce durable, signed evidence in the data directory, not just a probe print.
Observed result: Added public SyntheticFixtureToolAdapter to Core (implements the internal ILocalFixtureToolAdapter marker so ToolRegistry accepts it; ExecuteAsync returns a bounded read-only synthetic result; CleanupAsync returns the CLEANUP_OK|actionHash receipt the broker requires). The CLI run command now, after a valid probe: validates the action, evaluates policy (fixture.inspect capability registry + authorization trust store bound to the probe key), issues/consumes a one-use permit, freezes a fixture tool registry, opens a DurableEvidenceJournal at data/evidence.journal with a DurableArtifactStore at data/artifacts, mirrors evidence and workflow audit into the journal, loads/creates the DPAPI-protected runtime evidence key (ProvenanceKeyStore at data/keys), signs provenance, dispatches through the broker, walks the workflow state machine (Planned->Proposed->Allowed->Running->Observed->Verified->Reportable) with the IndependentFixtureVerifier and ReportPolicy gates, and reopens the journal to print recovery status. Live real-model run (Q4): POLICY Allow ref=policy-phase2; PERMIT issued=True; DISPATCH executed evidence=result_417390de... status=Success; PROVENANCE verified=True; VERIFIED passed=True; REPORT allowed=True (verified evidence is reportable); WORKFLOW Reportable (accepted); JOURNAL Verified events=1 audit=13; STOPPED cleanly. Persisted: data/evidence.journal (11,489 B), artifacts/ raw+redacted (16 B each), keys/runtimeevidence.key (DPAPI). Battery: Phase 3 phase3_tests=passed count=23 (TestSyntheticFixtureToolAdapter added); Phase 2 count=10; builds 0 warnings/0 errors; no llama-server processes left after stop.
Evidence: src/CyberSopHarness.Core/Phase3Contracts.cs (SyntheticFixtureToolAdapter), src/CyberSopHarness.App/Program.cs (ExecuteGovernedAsync), tests/Phase3.Tests/Program.cs, docs/phase3b-acceptance.md P3B-023; direct CLI run output; data/ directory contents.
Risk: The governed CLI path is still fixture-only by design (broker blocks non-fixture engagement modes); the workflow walk uses the same fixture verifier semantics as the tests, so 'Reportable' here means reportable-within-fixture, not a production finding; secrets are only redacted from output when an external provider secret exists.
Next: Phase 3B is complete except P3B-009 (redistribution approval, human decision). Natural next step: Phase 3C release packaging (harness + pinned runtime as a distributable with checksums and a release manifest, model unbundled), or an authorized-live-endpoint exercise for the external provider (endpoint set <url>; secret set external-api; setup --provider external-api --ack-egress yes; run).

### P3B-INTEGRATION-0010: Public GitHub repository published with professional release surface

Date: 2026-08-20
Event type: Release
Actor: Luna/OpenCode
Intent: Publish the harness as a professional, high-visibility open-source repository: technical README structured for conversion and AI parseability, shields.io badges, MIT license, trust files, CI, and an AI-readable companion file.
Observed result: Created CyberSopHarness.slnx (net10.0 solution with Core, App, Phase2.Tests, Phase3.Tests; TreatWarningsAsErrors). Wrote professional README.md (name + one-line pitch, 5 badges: CI/License/.NET/Platform/Tests-33, Mermaid architecture, Security section first, install, 3-command quickstart, CLI reference table, evidence/provenance, tests with verified counts, docs, contributing, MIT SPDX last), SECURITY.md (fail-closed security model + reporting), CONTRIBUTING.md (build/test/conventions/PR), llms.txt (AI-readable summary), LICENSE (MIT, AnonymousNomad 2026), .github/workflows/ci.yml (windows-latest: setup-dotnet 10.0.x, build slnx, Phase2+Phase3 suites, no real-model opt-in). Extended .gitignore to exclude data/, models/, runtime/, bin/, obj/, .vs/, *.user. Verified locally: phase2_tests=passed count=10, phase3_tests=passed count=23 (33 total), build clean. git init main branch, identity AnonymousNomad, secret sweep clean (no ghp_/private-key patterns in staged tree; sk-* hits are intentional test fixtures proving secrets are never persisted in plaintext). Commit 9c50bbf (91 files, 11,134 insertions). Repo created via GitHub API under AnonymousNomad (first token 401 Bad credentials, user supplied fresh token), pushed with non-persistent Basic auth header (no token in .git/config, none in commit tree). Published: https://github.com/AnonymousNomad/cyber-sop-harness (public, default branch main, MIT detected). Post-push verification: HEAD 9c50bbf8ca714281b592eedf0525efe69f3f26f8 matches local, tracked files 91, token absent from config and tree.
Evidence: README.md, LICENSE, SECURITY.md, CONTRIBUTING.md, llms.txt, .github/workflows/ci.yml, CyberSopHarness.slnx, .gitignore, commit 9c50bbf, live repo page and API responses.
Risk: The two PATs used in this session were shared through chat and are now considered exposed: user should revoke both immediately. Badges for CI will show 'no status' until the first workflow run on GitHub. README documents 33 tests, which the CI must keep green.
Next: Watch the first CI run (workflow badge will light up); revoke/rotate the PATs; optionally add a demo GIF and enable Discussions/star-history for traction; later Phase 3C release packaging.

### P3C-RUNTIME-0011: Governed HTTP dispatch is now available cross-platform from the desk

Date: 2026-08-25

Event type: Implementation

Actor: Codex

Intent: Remove the Windows-only custody blocker and connect signed engagement proposals to the governed HTTP header-inspection broker path without weakening scope or evidence controls.

Observed result: Added `PassphraseSecretProtector` using versioned PBKDF2-HMAC-SHA512/AES-256-GCM custody blobs, a 210,000-iteration floor, per-blob salt/nonce, context-bound AAD, and a 12-character minimum. App startup selects DPAPI on Windows and passphrase custody elsewhere; provenance keys reuse the selected protector. Desk `proposal submit --file` now validates trust/action/policy, binds a one-use permit, registers an exact-origin HTTP inspection adapter using operator-resolved addresses, executes through `ToolBroker`, records durable evidence, signs provenance, verifies the stamp, and requires successful cleanup before success. Added deterministic recovery tests for correct unlock, wrong passphrase, tampering, context mismatch, and short passphrases. Live loopback desk smoke reached the local server and returned `status=Success`, `provenance_verified=True`, and `cleanup=SUCCEEDED`; it initially exposed the missing permit-consumption step, which was fixed before commit. Battery after the fix: build clean at 0 warnings/errors; Phase2=10, Phase3=27, CommandDesk=7 (44 total).

Evidence: `src/CyberSopHarness.Core/SecretProtector.cs`, `src/CyberSopHarness.App/Program.cs`, `tests/Phase3.Tests/Program.cs`, README status/evidence sections, live isolated authorized loopback journal/artifacts.

Risk: The HTTP adapter is read-only metadata inspection, but authorized-mode dispatch can contact real targets; production use still requires operator-supplied legal authorization, exact DNS resolution, containment review, and acceptance that Linux/macOS passphrase custody is portable rather than hardware-backed.

Next: Add desk integration coverage so permit binding cannot regress; expand typed adapters only through the same policy/permit/evidence gate; add OS keychain or secure enclave custody where practical; then exercise one explicitly authorized external target under operational supervision.
