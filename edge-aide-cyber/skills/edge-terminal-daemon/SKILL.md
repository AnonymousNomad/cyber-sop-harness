# Edge Terminal Daemon

## What To Do
Build a persistent Node.js daemon that serves as the main terminal process. It manages the WebSocket server, model runtime, governance engine, and tool adapters. It checkpoints state every 30 seconds and writes a PID file for the watchdog.

## Why
The terminal needs to be a long-running process that survives screen locks, app switches, and partial OOM events. A daemon with PID file management enables the watchdog to detect crashes and restart.

## Code Guidance

```javascript
// src/daemon/manager.mjs
import { writeFile, readFile, unlink } from 'node:fs/promises';
import { join } from 'node:path';

export class DaemonManager {
  #pidFile;
  #checkpointDir;
  #checkpointIntervalMs = 30000;
  #timer = null;

  constructor({ workspaceDir }) {
    this.#pidFile = join(workspaceDir, 'daemon.pid');
    this.#checkpointDir = join(workspaceDir, '.checkpoints');
  }

  async acquireLock() {
    try {
      const existing = await readFile(this.#pidFile, 'utf8');
      const pid = parseInt(existing.trim());
      if (pid && !isNaN(pid)) {
        try {
          process.kill(pid, 0); // Check if alive
          throw new Error(`Daemon already running (PID ${pid})`);
        } catch (err) {
          if (err.code === 'ESRCH') {
            // Process dead, stale PID file
            await unlink(this.#pidFile);
          } else {
            throw err;
          }
        }
      }
    } catch (err) {
      if (err.code === 'ENOENT') {
        // No PID file, we're good
      } else {
        throw err;
      }
    }

    await writeFile(this.#pidFile, String(process.pid));
    // Cleanup on exit
    process.on('exit', () => {
      try { require('node:fs').unlinkSync(this.#pidFile); } catch {}
    });
  }

  startCheckpointing(getStateFn) {
    const { mkdirSync } = require('node:fs');
    mkdirSync(this.#checkpointDir, { recursive: true });

    this.#timer = setInterval(async () => {
      try {
        const state = getStateFn();
        const file = join(this.#checkpointDir, `cp-${Date.now()}.json`);
        const tmpFile = file + '.tmp';
        await writeFile(tmpFile, JSON.stringify(state));
        const { rename } = await import('node:fs/promises');
        await rename(tmpFile, file); // Atomic write
        await this.#cleanCheckpoints();
      } catch {}
    }, this.#checkpointIntervalMs);
  }

  async #cleanCheckpoints() {
    const { readdir, unlink } = await import('node:fs/promises');
    const files = await readdir(this.#checkpointDir);
    const cps = files.filter(f => f.startsWith('cp-')).sort();
    while (cps.length > 5) {
      await unlink(join(this.#checkpointDir, cps.shift()));
    }
  }

  async getLastCheckpoint() {
    try {
      const { readdir } = await import('node:fs/promises');
      const files = await readdir(this.#checkpointDir);
      const cps = files.filter(f => f.startsWith('cp-')).sort().reverse();
      if (cps.length === 0) return null;
      const data = await readFile(join(this.#checkpointDir, cps[0]), 'utf8');
      return JSON.parse(data);
    } catch {
      return null;
    }
  }

  stop() {
    if (this.#timer) clearInterval(this.#timer);
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| PID file stale after crash | Cannot start new instance | Check if PID alive before blocking |
| Checkpoint write during crash | Corrupted file | Atomic write (tmp + rename) |
| Multiple daemon instances | State corruption | Lock file with PID check |

## Dependencies
- Node.js (available)
- File system write access (available)

## Pitfalls
- `process.on('exit')` may not fire on SIGKILL
- Checkpoint interval should match memory guardian frequency
- PID file in `/tmp` may be cleared on container restart
- Use `flock` or PID check, not file locks (unreliable on Android)