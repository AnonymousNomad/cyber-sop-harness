# Edge OOM Resilience & Auto-Recovery

## What To Do
Build a watchdog daemon that survives OOM kills by running outside the main process. When the Linux environment is killed and restarted, the watchdog detects this and restores state from checkpoint files.

## Why
Android's OOM killer sends SIGKILL to the Linux container process. No cleanup handlers run. The only way to survive is to have a separate monitor process (or use Android's own restart mechanisms) that detects the crash and restores state.

## Architecture

```
┌─────────────────────────────────────────────┐
│  Android System                             │
│  ┌──────────────────────────────────────┐   │
│  │  Linux Container (our app)           │   │
│  │  ┌──────────────────────────────┐    │   │
│  │  │  Terminal Daemon (Node.js)   │    │   │
│  │  │  - State checkpoint every 30s │   │   │
│  │  │  - PID file in /tmp          │    │   │
│  │  └──────────────────────────────┘    │   │
│  │  ┌──────────────────────────────┐    │   │
│  │  │  Watchdog (shell script)     │    │   │
│  │  │  - Runs in separate shell    │    │   │
│  │  │  - Checks PID every 10s     │    │   │
│  │  │  - Restarts if dead          │    │   │
│  │  └──────────────────────────────┘    │   │
│  └──────────────────────────────────────┘   │
│                                             │
│  OOM Killer → SIGKILL → Container dies      │
│  Watchdog → detects → waits → restarts      │
└─────────────────────────────────────────────┘
```

## Code Guidance

```bash
#!/bin/bash
# scripts/watchdog.sh — runs in separate shell session
DAEMON_PID_FILE="/tmp/edge-cyber-daemon.pid"
CHECK_INTERVAL=10
MAX_RETRIES=5
RETRY_DELAY=30

check_daemon() {
  if [ -f "$DAEMON_PID_FILE" ]; then
    PID=$(cat "$DAEMON_PID_FILE")
    if kill -0 "$PID" 2>/dev/null; then
      return 0  # alive
    fi
  fi
  return 1  # dead
}

restart_daemon() {
  local retries=0
  while [ $retries -lt $MAX_RETRIES ]; do
    echo "[$(date)] Restarting daemon (attempt $((retries+1)))"
    cd "$HOME/.edge-cyber" && node src/server.mjs &
    sleep $RETRY_DELAY
    if check_daemon; then
      echo "[$(date)] Daemon restarted successfully"
      return 0
    fi
    retries=$((retries+1))
  done
  echo "[$(date)] Failed to restart after $MAX_RETRIES attempts"
}

echo "[$(date)] Watchdog started"
while true; do
  if ! check_daemon; then
    echo "[$(date)] Daemon not running, restarting..."
    restart_daemon
  fi
  sleep $CHECK_INTERVAL
done
```

## Code: State Checkpoint

```javascript
// src/android/state-checkpoint.mjs
import { writeFile, readFile, mkdir } from 'node:fs/promises';
import { join } from 'node:path';

export class StateCheckpoint {
  #dir;
  #intervalMs = 30000;
  #timer = null;

  constructor(checkpointDir) {
    this.#dir = checkpointDir;
  }

  async init() {
    await mkdir(this.#dir, { recursive: true });
  }

  start(getStateFn) {
    this.#timer = setInterval(async () => {
      const state = getStateFn();
      await this.save(state);
    }, this.#intervalMs);
  }

  stop() {
    if (this.#timer) clearInterval(this.#timer);
  }

  async save(state) {
    const file = join(this.#dir, `checkpoint-${Date.now()}.json`);
    await writeFile(file, JSON.stringify(state));
    // Keep only last 5 checkpoints
    await this.#cleanup();
  }

  async load() {
    const { readdir } = await import('node:fs/promises');
    const files = await readdir(this.#dir);
    const checkpoints = files
      .filter(f => f.startsWith('checkpoint-'))
      .sort()
      .reverse();
    if (checkpoints.length === 0) return null;
    const data = await readFile(join(this.#dir, checkpoints[0]), 'utf8');
    return JSON.parse(data);
  }

  async #cleanup() {
    const { readdir, unlink } = await import('node:fs/promises');
    const files = await readdir(this.#dir);
    const checkpoints = files.filter(f => f.startsWith('checkpoint-')).sort();
    while (checkpoints.length > 5) {
      await unlink(join(this.#dir, checkpoints.shift()));
    }
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Watchdog itself gets killed | No recovery possible | Use `nohup` + separate shell session |
| Checkpoint file corrupted | Cannot restore state | Write to temp then rename (atomic) |
| Restart loop | Battery drain, system lag | Max retries + exponential backoff |
| State stale after long downtime | Restored to wrong state | Include timestamp, reject old checkpoints |

## Dependencies
- Bash shell (available)
- Node.js (available)
- `/tmp` writable (available)

## Pitfalls
- Watchdog runs in same container — can also be killed by OOM
- `nohup` may not survive Android process management
- State checkpoints must be small (<1MB) to write fast
- Checkpoint interval too frequent wastes I/O; too slow loses state
- Android may kill the watchdog's shell session on screen off