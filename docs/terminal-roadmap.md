# Terminal-First Roadmap

Status: research baseline for the next implementation stages

Date: 2026-08-23

Current repository revision: `e92e2eaf24a85e8c566b1b469ac1053eec4c86c6`

## Objective

Turn Cyber SOP Harness into a professional, terminal-first cybersecurity operations console for authorized bug-bounty work on edge devices. The model proposes and explains; the harness enforces authorization, scope, risk approval, typed execution, evidence, independent verification, cleanup, and reporting. It must not become an unrestricted Parrot-style shell.

## Measured Edge Decision

The practical local candidate tested was `WhiteRabbitNeo-2.5-Qwen-2.5-Coder-7B` GGUF in `Q4_K_M`, with the ARM-specialized `Q4_0_8_8` comparison. Both load with pinned llama.cpp and produce output, but the Android host currently retains enough RAM that a 7B model cannot remain resident. Weight streaming reduces generation to unusable speeds.

- Keep the pinned Q4_K_M artifact as the quality/reference model for dedicated-RAM tests or external GPU/cloud use.
- Do not claim interactive edge readiness until a clean device profile has at least 5 GiB continuously available RAM.
- For daily work on this tablet, use a smaller approved assistant model, an explicitly consented cloud provider, or a desktop/GPU gateway with the tablet as a control terminal.
- Never enable automatic cloud fallback because target data may leave the device.

## Phase 3C — Terminal Runtime Hardening

Command-desk presentation and interaction phases are tracked separately in
[`command-desk-roadmap.md`](command-desk-roadmap.md). That work must reuse this phase's stable verbs,
JSON contracts, containment rules, and emergency controls.

### Deliverables

1. Stable CLI verbs: `doctor`, `engagement validate`, `model pin`, `model serve`, `proposal submit`, `action status`, `evidence export`, `report build`, and `emergency stop`.
2. Deterministic process behavior: JSON results on stdout, diagnostics on stderr, and stable exit codes for validation, policy denial, resource failure, and interruption.
3. Terminal safety: interactive confirmation for R3 actions, no bypass flag for scope/approval/containment, neutralized terminal escape sequences, and credential redaction.
4. Runtime integration: verified manifests, loopback-only serving, explicit context limits, disabled model tool authority, and no automatic fallback.

### Gate

All CLI verbs pass offline fixtures. Tampered artifacts fail startup. Policy denial occurs before adapter execution. Emergency stop works while the model is hung. No secret appears in stdout, stderr, journal, or crash output.

## Phase 4A — Methodology Engine

### Deliverables

1. A versioned SOP registry containing objective, prerequisites, allowed capabilities, oracle, evidence requirements, cleanup, escalation, and prohibited actions.
2. A compiler that turns SOP YAML or JSON into a deterministic workflow graph.
3. A coverage ledger showing completed, unknown, blocked, and not-applicable steps honestly.
4. Offline fixtures covering true positives, false positives, malformed evidence, out-of-scope proposals, prompt injection, and interrupted cleanup.
5. Terminal views for the next safe step, blocked reason, required approval, current coverage, and replay package.

### Gate

Invalid procedures fail compilation. Procedures cannot grant authority beyond the engagement manifest. State advances reference durable evidence. Independent verification reproduces known lab findings.

## Phase 4B — Bug-Bounty Operations Profile

### Deliverables

1. Scope-aware reconnaissance and mapping using only authorized passive or low-impact methods.
2. Web and API methodologies mapped to OWASP WSTG and ASVS references already locked in `docs/standards-lock.json`.
3. Business-logic test templates requiring human-supplied application context.
4. Finding lifecycle from hypothesis through reproducible, independently verified, and reportable states.
5. Platform-ready reports with steps, impact, evidence references, remediation notes, uncertainty, and redacted attachments.

### Gate

No live request occurs without a valid engagement manifest and permit. Wildcards, redirects, shared infrastructure, and tenant ambiguity fail closed. Reports contain only independently verified claims or clearly labeled hypotheses.

## Phase 5T — Remote Terminal Control Plane

### Deliverables

1. Hardened SSH or mTLS terminal client for tablet-to-gateway operation.
2. Device enrollment, revocation, short-lived session keys, and operator identity binding.
3. Action-bound approvals carrying nonce, expiration, risk class, provider identity, and engagement hash.
4. Local emergency stop independent of remote client availability.
5. Signed evidence synchronization with conflict-safe append-only merge rules.

### Gate

Approval replay fails. Revoked clients cannot authenticate. Network loss denies new remote work but preserves evidence and local stop capability. Provider changes invalidate active approvals and trigger security regressions.

## Phase 6 — Release Assurance

### Deliverables

1. Build provenance, SBOM, dependency license report, and pinned source revisions.
2. Security regressions for authorization, containment, model swaps, evidence integrity, recovery, reporting, and terminal handling.
3. Performance profiles for minimum viable edge hardware and recommended gateway hardware.
4. A release manifest linking model, runtime, policies, methodologies, tests, audit trail, and documentation.

### Gate

No release ships unsigned artifacts, unresolved high risks, live-target claims without completed assurance, or documentation that overstates readiness.
