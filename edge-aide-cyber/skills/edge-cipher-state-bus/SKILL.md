# Edge Cipher State Bus

## What To Do
Port AIDE's Cipher state bus to edge: an append-only JSONL event log that all components write to and query from. This is the "memory" that lets learned preferences persist across sessions.

## Why
Without shared state, each subsystem operates blind. The Cipher bus creates a unified timeline of approvals, rejections, findings, and operator preferences that the scaffold system queries to personalize prompts.

## Code Guidance
```javascript
import { promises as fs } from 'node:fs';
import path from 'node:path';

export function createCipherBus(workspaceDir) {
  const stateFile = path.join(workspaceDir, '.edge-cyber', 'cipher-state.jsonl');

  async function append(event) {
    if (!event || typeof event !== 'object') return;
    const entry = { ...event, at: new Date().toISOString() };
    await fs.mkdir(path.dirname(stateFile), { recursive: true }).catch(() => {});
    await fs.appendFile(stateFile, JSON.stringify(entry) + '\n').catch(() => {});
  }

  async function query({ type, since, limit = 100 } = {}) {
    try {
      const raw = await fs.readFile(stateFile, 'utf8');
      let entries = raw.split('\n').filter(Boolean).map(line => {
        try { return JSON.parse(line); } catch { return null; }
      }).filter(Boolean);
      if (type) entries = entries.filter(e => e.type === type);
      if (since) entries = entries.filter(e => e.at >= since);
      return entries.slice(-limit).reverse();
    } catch { return []; }
  }

  async function getLearnedPreferences(minCount = 3, limit = 15) {
    const approvals = await query({ type: 'approval', limit: 500 });
    const rejections = await query({ type: 'rejection', limit: 500 });
    const patterns = {};
    for (const entry of [...approvals, ...rejections]) {
      if (!entry.pattern || !entry.decision) continue;
      if (!patterns[entry.pattern]) patterns[entry.pattern] = { count: 0, approved: 0 };
      patterns[entry.pattern].count += 1;
      if (entry.decision === 'approve') patterns[entry.pattern].approved += 1;
    }
    return Object.entries(patterns)
      .filter(([, s]) => s.count >= minCount && s.approved / s.count >= 0.6)
      .sort((a, b) => b[1].approved - a[1].approved)
      .slice(0, limit)
      .map(([pattern]) => `[learned] ${pattern}`);
  }

  return { append, query, getLearnedPreferences };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| File grows unbounded | Disk exhaustion on edge device | Implement rotation after N entries or M bytes |
| Concurrent writes corrupt JSONL | Data loss | Node.js appendFile is atomic for small writes on Linux |
| Sensitive data in event log | Secret leakage | Sanitize events before appending; strip credentials |
| Replay attack on learned preferences | Wrong suggestions injected | Include session ID; ignore cross-session patterns unless explicitly imported |

## Dependencies
- Node.js built-in `fs/promises`, `path`

## Pitfalls & Bugs
- `appendFile` on Android's FUSE filesystem may be slower than expected; batch writes if throughput matters.
- JSON.parse on partially-written lines will fail; the filter handles this gracefully but logs a warning.
- Learned preferences from testing sessions shouldn't influence production engagements without explicit reset.
- Event timestamps use wall clock which can jump on NTP sync; for ordering, use a monotonically increasing sequence number.
