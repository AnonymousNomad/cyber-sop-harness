# Edge Auto-Recovery System

## What To Do
Build a complete recovery system that detects crashes, restores state from checkpoints, reconnects WebSocket clients, and resumes where it left off.

## Why
The combination of memory pressure and Android's OOM killer means crashes will happen. The goal is to make them invisible to the operator — the terminal recovers automatically within seconds.

## Code Guidance

```javascript
// src/android/auto-recovery.mjs
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';

export class AutoRecovery {
  #checkpointDir;
  #state;
  #recoveryCount = 0;

  constructor(checkpointDir, initialState) {
    this.#checkpointDir = checkpointDir;
    this.#state = initialState;
  }

  async attemptRecovery() {
    const checkpoint = await this.#loadLatestCheckpoint();
    if (!checkpoint) {
      console.log('No checkpoint found, starting fresh');
      return false;
    }

    const age = Date.now() - checkpoint.timestamp;
    const maxAge = 5 * 60 * 1000; // 5 minutes

    if (age > maxAge) {
      console.log(`Checkpoint too old (${Math.round(age / 1000)}s), starting fresh`);
      return false;
    }

    // Restore state
    Object.assign(this.#state, checkpoint.state);
    this.#recoveryCount++;

    console.log(`Recovered from checkpoint (${Math.round(age / 1000)}s old, recovery #${this.#recoveryCount})`);
    return true;
  }

  async #loadLatestCheckpoint() {
    try {
      const files = await readdir(this.#checkpointDir);
      const checkpoints = files
        .filter(f => f.startsWith('cp-') && f.endsWith('.json'))
        .sort()
        .reverse();

      for (const file of checkpoints) {
        try {
          const data = await readFile(join(this.#checkpointDir, file), 'utf8');
          return JSON.parse(data);
        } catch {
          continue; // Try next checkpoint
        }
      }
    } catch {}
    return null;
  }

  getRecoveryCount() { return this.#recoveryCount; }
  getState() { return this.#state; }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| All checkpoints corrupted | Cannot recover | Checkpoint to multiple locations |
| Recovery restores stale state | Wrong data displayed | Age check, reject old checkpoints |
| Recovery loop (crash on startup) | Infinite restart | Max recovery attempts |

## Dependencies
- StateCheckpoint module
- Node.js fs module

## Pitfalls
- Recovery should be fast (<2s) — operator shouldn't notice
- Some state cannot be restored (open network connections, child processes)
- Recovery count should be displayed to operator for transparency
- If recovery fails 3 times, suggest manual restart