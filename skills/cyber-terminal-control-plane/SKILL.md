---
name: cyber-terminal-control-plane
description: Designs and implements the safe terminal-first CLI/TUI control plane for authorized Cyber SOP Harness operations. Use when adding terminal commands, interactive approvals, machine-readable output, emergency controls, model runtime commands, or evidence/report workflows.
---

# Cyber Terminal Control Plane

## What And Why

The terminal is an operator console over the governance core, not a shell escape. It must make authorization state, risk, provider identity, evidence, and stop controls visible while keeping every action subject to policy.

## Required Commands

- `doctor`: verify memory, disk, CPU affinity, runtime hashes, model hashes, licenses, policy version, journal integrity, and containment readiness.
- `engagement validate`: validate scope, time window, methods, exclusions, data rules, rate limits, stop contacts, and cleanup without target interaction.
- `model pin`: verify model/runtime source revision, SHA-256, architecture, context limit, RAM budget, and license before use.
- `model serve`: start loopback-only local inference with explicit context and resource limits; deny startup when budgets fail.
- `proposal submit`: accept strict JSON proposals from a model and route them through parser, policy, permit, broker, evidence, and verification.
- `action status`: show pending approval, active permit, blocked reason, result, verification state, and cleanup state.
- `evidence export`: export signed/redacted replay packages only after integrity checks pass.
- `report build`: generate reports only from allowed finding states and referenced evidence.
- `emergency stop`: deny permits, stop worker trees, preserve evidence, revoke credentials, and record the reason independently of model health.

## Interface Rules

Return structured JSON on stdout and diagnostics on stderr. Use stable exit codes for success, invalid input, policy denial, missing authority, resource failure, and interrupted work. Render untrusted output as inert text, strip terminal control sequences, and never colorize raw evidence in ways that hide injected instructions.

Interactive confirmation is required for R3 actions unless a separate signed batch approval exists. A `--yes` flag may suppress repetition but never bypasses scope, expiry, approval, containment, evidence, or policy checks.

## Dependencies

Engagement manifest schema, scope evaluator, policy engine, permit issuer, typed tool registry, worker supervisor, evidence ledger, provenance signer, model/runtime manifest, llama.cpp or equivalent pinned runtime, and platform process-control APIs.

## Threat Matrix

| Threat | Control |
|---|---|
| Model output becomes command authority | Parse as proposal; policy issues the only decision |
| Approval bypass through flags | Flags cannot replace manifest, permit, signature, or risk gate |
| Terminal injection | Escape/inert untrusted text; no direct paste into commands |
| Secret leakage | Redact stdout/stderr/journals/crash reports; keep credentials out of argv |
| Hung model blocks stop | Emergency stop must not depend on model or provider |
| Ambiguous failure | Stable exit code plus policy/evidence reference |
| Accidental live operation | Offline/local fixture default and explicit engagement activation |
| Evidence tampering | Hash chain validation before export or report generation |

## Bugs And Pitfalls

Do not implement free-form shell mode. Do not print raw prompts containing target data by default. Do not let progress rendering obscure a denial. Do not cache approvals across engagement, provider, model, policy, or scope changes. Do not report a finding verified because a model asserted it.

## Gate

A release is terminal-ready only when offline fixture tests prove valid execution, malformed proposals rejection, out-of-scope blocking, R3 approval enforcement, tamper rejection, redaction, emergency stop, recovery, and deterministic exit codes.
