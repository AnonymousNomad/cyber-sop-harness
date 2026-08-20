---
name: cyber-durable-evidence-persistence
description: Governs durable, append-only, hash-chained, crash-safe persistence of evidence events, artifacts, and workflow state so records survive process exit, crash, and power loss and can be replayed. Use whenever evidence, artifacts, or state must outlive a single process.
---

# Cyber Durable Evidence Persistence

## Directive

Implement a durable evidence store that persists the in-memory `EvidenceLedger`/`ArtifactStore`/workflow state to disk with these guarantees:

- Append-only: never overwrite or rewrite a committed record; new events are appended as complete, self-contained records (for example one JSON object per line).
- Hash-chained: each persisted record carries the previous record's hash so any gap, reorder, edit, or truncation is detectable on load.
- Crash-safe: flush/fsync after each append so a committed record survives process exit, crash, and power loss. Do not rely on buffered writes alone.
- Atomic record boundary: write the full record, then flush. A partial trailing record after a crash is treated as uncommitted and discarded during recovery.
- Artifact durability: raw/redacted artifact bytes are written to stable storage and referenced by SHA-256; the reference is only valid once the bytes are flushed and their hash matches.
- Recovery on load: re-read the chain, verify every link and every artifact hash, and stop at the first valid prefix. Report `VERIFIED`, `PARTIAL`, or `CORRUPT`.
- Replayable: a recovered chain must be loadable back into the in-memory ledger and re-verifiable with the same `VerifyIntegrity` path.

Do not store secrets in the evidence store. Redact before persisting. Do not persist live-target data unless the engagement explicitly allows it.

## Rationale and Architectural Reason

Evidence that lives only in memory is lost on process exit and cannot be audited, replayed, or used to support a finding. The existing `EvidenceLedger` already hash-chains events in memory; persistence must preserve that chain across restarts without weakening it.

The crash-safety model follows the SQLite atomic-commit design: an operating system buffers and reorders writes, so a `write` returning does not mean the bytes reached stable storage. A flush/fsync at the commit point is what makes a record durable. A partial trailing record after a crash means the write did not complete, so it must be discarded rather than trusted. Because records are append-only and hash-chained, recovery is simply "verify from the front and keep the longest valid prefix"; there is no in-place repair, which keeps recovery itself from becoming a tampering vector.

Keeping persistence append-only and separate from the in-memory ledger means the durable layer is a projection of the verified chain, not a second source of truth. The in-memory ledger remains the authority during a run; the durable store is the audit/replay substrate.

## Threat Matrix

| Threat/trap | Likely complication/error | Required prevention/detection | Test |
|---|---|---|---|
| Buffered write lost on crash | Record "written" but not on disk after power loss | fsync/flush after each append; verify on reload | Kill-process-mid-append recovery test |
| Partial trailing record | Torn JSON line after crash | Atomic record boundary; discard incomplete tail | Truncated-file recovery test |
| Chain gap or reorder | Missing or swapped record | Hash-chain link verification on load | Tampered-chain detection test |
| Artifact hash mismatch | Reference points to altered bytes | Re-hash artifact on load; compare | Corrupted-artifact detection test |
| Secret leakage into evidence | API key/token persisted | Redact before persist; scan persisted output | Secret-canary persistence scan |
| Unbounded growth | Evidence file grows without limit | Size budget, rotation, and retention policy | Rotation/retention test |
| Replay divergence | Recovered chain fails `VerifyIntegrity` | Reload into ledger and re-verify | Round-trip replay test |
| Concurrent writers | Two processes append to one file | Single-writer lock or per-run file isolation | Concurrent-write rejection test |
