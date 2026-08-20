---
name: cyber-state-evidence-replay
description: Defines the workflow state machine, evidence event model, hash-linked audit trail, replay protocol, finding lifecycle, independent verification, and coverage accounting. Use whenever implementing state, logs, findings, reports, or recovery.
---

# Cyber State Evidence and Replay

## State

Use explicit states: `READY`, `PLANNED`, `PROPOSED`, `ALLOWED`, `RUNNING`, `STOPPING`, `OBSERVED`, `VERIFIED`, `BLOCKED`, `UNKNOWN`, `STOPPED`, and `REPORTABLE`.

The model may suggest transitions. The host controls transitions.

## Evidence Event

Every action records request, tool, tool version, target, policy result, approval, timestamp, exit status, raw-output reference, redacted-output reference, SHA-256 hashes, parent event, artifact references, and cleanup result.

Hash raw bytes, not a summary. A hash proves identity and integrity, not semantic correctness.

## Acknowledgement

Before the model uses a result, require acknowledgement of action ID, result event ID, raw-output hash, and observation references. A mismatch freezes the run and invalidates later reasoning.

## Finding Lifecycle

A finding moves through `HYPOTHESIS`, `CANDIDATE`, `REPRODUCIBLE`, `VERIFIED`, and `REPORTABLE`.

Use `UNVERIFIED` when a previously supported claim loses verification. Use `UNKNOWN` when evidence or reproduction is incomplete. Use `BLOCKED` when policy, authorization, safety, or approval prevented verification. Finding transitions must explicitly record `UNVERIFIED -> UNKNOWN` or `UNVERIFIED -> BLOCKED` when appropriate. None of these states may be silently treated as a confirmed negative or a verified finding.

Finding transition graph:

```text
HYPOTHESIS -> CANDIDATE
CANDIDATE -> REPRODUCIBLE | UNKNOWN | REJECTED
REPRODUCIBLE -> VERIFIED | UNKNOWN | BLOCKED
VERIFIED -> REPORTABLE | REJECTED
UNVERIFIED -> UNKNOWN | BLOCKED | REJECTED
```

## Independent Verification

The verifier must be separate from the discovery context and use raw evidence, a reproduction request, a safe fixture, independent observation, and scope/policy records.

## Replay

Label runs `REPLAYABLE`, `PARTIALLY_REPLAYABLE`, or `NON_REPLAYABLE`. A run is replayable only when the relevant request, tool, version, environment, fixture, and evidence are available.

## Acceptance Tests

Prove that a missing event blocks state transition, a hash mismatch freezes execution, a false interpretation does not become a finding, an independent verifier reproduces a known fixture, a report cannot include an unverified finding, and coverage shows tested, blocked, unknown, and not-tested procedures.
