# Edge Evidence Chain

## What To Do
Implement an append-only, SHA-256 hash-chained evidence journal. Each entry links to the previous entry's hash. Tampering with any entry breaks the chain and is detectable during verification.

## Why
Professional security reports require evidence that hasn't been altered. A hash chain provides cryptographic proof of ordering and integrity without requiring a blockchain. On edge, this runs locally and exports as a verifiable package.

## Code Guidance
```javascript
import { createHash } from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';

export function createEvidenceChain(dataDir) {
  const filePath = path.join(dataDir, 'evidence.jsonl');
  let lastHash = null;

  async function load() {
    try {
      const raw = await fs.readFile(filePath, 'utf8');
      const lines = raw.split('\n').filter(Boolean);
      if (lines.length > 0) {
        const lastEntry = JSON.parse(lines[lines.length - 1]);
        lastHash = lastEntry.hash;
      }
    } catch { lastHash = null; }
  }

  async function append(event) {
    const timestamp = new Date().toISOString();
    const entry = {
      seq: await getNextSeq(),
      at: timestamp,
      type: event.type,
      data: sanitize(event.data),
    };

    const contentToHash = JSON.stringify({ ...entry, prevHash: lastHash });
    entry.prevHash = lastHash;
    entry.hash = createHash('sha256').update(contentToHash).digest('hex');

    await fs.appendFile(filePath, JSON.stringify(entry) + '\n');
    lastHash = entry.hash;
    return entry;
  }

  async function verify() {
    const raw = await fs.readFile(filePath, 'utf8');
    const entries = raw.split('\n').filter(Boolean).map(JSON.parse);
    let expectedPrev = null;

    for (const entry of entries) {
      const recomputed = createHash('sha256')
        .update(JSON.stringify({
          seq: entry.seq, at: entry.at, type: entry.type,
          data: entry.data, prevHash: entry.prevHash
        }))
        .digest('hex');

      if (entry.prevHash !== expectedPrev) return { valid: false, breakAt: entry.seq };
      if (entry.hash !== recomputed) return { valid: false, breakAt: entry.seq };
      expectedPrev = entry.hash;
    }
    return { valid: true, entries: entries.length };
  }

  async function getNextSeq() {
    try {
      const raw = await fs.readFile(filePath, 'utf8');
      const lines = raw.split('\n').filter(Boolean);
      return lines.length;
    } catch { return 0; }
  }

  function sanitize(data) {
    if (typeof data === 'string') {
      return data.replace(/(?:Bearer|token|key|password|secret)\s*[:=]\s*\S+/gi, '[REDACTED]');
    }
    return data;
  }

  return { load, append, verify, get lastHash() { return lastHash; } };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Attacker modifies past evidence entry | False findings / cover tracks | Hash chain detects modification during verify() |
| Attacker deletes entire file | Loss of all evidence | Export signed snapshots periodically |
| Sensitive data (credentials) stored in evidence | Credential exposure | Sanitizer strips known secret patterns before writing |
| Concurrent appends corrupt chain | Broken verification | Single-process design eliminates concurrent writes |
| Disk full during append | Partial write | Catch ENOSPC; report clearly; do not silently continue |

## Dependencies
- Node.js built-in `crypto`, `fs/promises`, `path`

## Pitfalls & Bugs
- `JSON.stringify` key order matters for hashing; always construct the object in the same key order.
- The sanitizer regex won't catch all credential formats; expand patterns based on your tool outputs.
- Sequence number based on line count breaks if lines are manually removed; use a monotonic counter stored alongside.
- Large evidence files slow verification; implement incremental verification that checks only recent entries.
- The `load()` function reads the entire file to find the last hash; for large files, read just the last few KB instead.
