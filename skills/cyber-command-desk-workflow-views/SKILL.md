---
name: cyber-command-desk-workflow-views
description: Implements professional bug-bounty and pentest command-desk views for preflight, engagements, recon, proposals, approvals, actions, evidence, findings, and emergency control.
---

# Cyber Command Desk Workflow Views

## What And Why

Phase 3 organizes governed bug-bounty and authorized pentest work into scannable views. A view
is a projection of policy/evidence state, not a place where the model or operator can override
authority. Every offensive step remains a proposal until the harness issues a permit.

## Required Views

1. **Preflight** — disk, RAM, CPU affinity, runtime/model hashes, license, policy version,
   journal integrity, loopback containment, stop contacts, and offline defaults.
2. **Engagement** — manifest validity, authorization window, target allowlist, exclusions,
   rate limits, data handling, credentials/custody, cleanup requirements, and activation state.
3. **Targets** — resolved in-scope candidates, unresolved names, excluded collisions, last
   evidence ID, and next allowed method. Never show wildcard expansion as authorization.
4. **Proposals** — strict JSON request, capability/risk, expected observation, policy result,
   approval requirement, expiry, and one-action confirmation.
5. **Actions** — queued/running/blocked/complete states, permit ID, worker PID/tree, elapsed time,
   cancel/rollback controls, and current containment.
6. **Evidence** — append-only events, hashes/provenance, verification state, secret-redaction
   status, export eligibility, and replay package integrity.
7. **Findings** — candidate, corroborated, false positive, needs expert review, reported, and
   disclosed. A finding becomes confirmed only with independent evidence.
8. **Report** — impact, reproducible sanitized steps, affected asset, evidence references,
   remediation, disclosure window, and redaction checks.
9. **Emergency** — always-visible stop control, reason capture, permit revocation, worker-tree
   shutdown, credential suspension, evidence preservation, and recovery checklist.

## Interaction Rules

- Default landing view is preflight/doctor, not reconnaissance.
- Selecting a target does not authorize testing it.
- Submitting model output opens the proposal parser/policy flow; it never executes directly.
- R3 requires explicit typed confirmation even when a batch approval exists.
- Destructive/out-of-policy requests remain visible as blocked with the exact policy reason.
- Every mutating command returns action ID, status, evidence/event ID, next permitted transition,
   and cancellation path.

## Code To Write

- `ViewRegistry` mapping view name, verb tree, required permissions, and refresh source.
- `ViewContext` carrying engagement snapshot, policy decisions, evidence cursor, terminal size,
   and cancellation token.
- Read-model projections from `DurableEvidenceStore`, `PolicyEngine`, `PermitIssuer`,
  `RollbackLedger`, `ResourceTelemetry`, and staged model catalog.
- Deterministic formatters for human, compact, and JSON output.
- Keyboard shortcuts: help `F1`, actions `F2`, proposals `F3`, evidence `F4`, findings `F5`,
  reports `F6`, emergency `Ctrl+C` confirmation and `F12`.
- Regression fixtures for wildcard collision, expired approval, injected HTTP response, missing
  evidence, duplicate finding, tampered export, and stop during running action.

## Dependencies

- Existing Phase 3 contracts/schemas and governance core.
- Phase 1 command registry and Phase 2 status/theme system.
- Typed tool registry/broker; no direct subprocess invocation from a view.
- Optional Terminal.Gui/Spectre.Console after view projections and tests exist.

## Threat Matrix

| Threat | Control |
|---|---|
| Wildcard/sibling takeover | Resolve against allowlist/exclusions before display or dispatch |
| Model hallucinated finding | Candidate state requires independent evidence and reviewer action |
| Approval replay | Approval bound to engagement/action/provider/policy nonce and expiry |
| Evidence tampering | Hash-chain verification gates export/report views |
| Sensitive report leak | Mandatory redaction/secret scan and explicit publication level |
| Hung tool traps operator | Worker timeout, cancel, supervisor status, independent stop |
| Accidental live operation | Offline fixture default and explicit engagement activation |

## Bugs And Pitfalls

- Do not show a target as “ready” when only its parent domain is authorized.
- Do not combine authentication, exploitation, exfiltration, and cleanup into one opaque action.
- Do not let report generation import unredacted raw evidence by default.
- Do not make emergency stop a submenu action or depend on model availability.
- Do not persist customer data in view caches beyond engagement retention rules.

## Gate

Phase 3 passes when all nine views render from offline fixtures, every mutation traverses policy/
permit/evidence, blocked cases show reasons, reports contain only referenced evidence, emergency
stop works from every view, and JSON/human outputs agree semantically.
