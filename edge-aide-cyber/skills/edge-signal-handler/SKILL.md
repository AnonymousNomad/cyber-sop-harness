# Edge Signal Handler & Graceful Shutdown

## What To Do
Handle all possible termination signals to ensure state is saved before the process dies. Android sends different signals for different kill reasons.

## Why
Understanding Android's kill signals helps us survive:
- SIGTERM (15): Graceful kill — we can clean up
- SIGKILL (9): OOM kill — no cleanup possible (need watchdog)
- SIGSTOP (19): Freeze — process pauses but survives
- SIGHUP (1): Terminal hangup — can reinitialize

## Signal Map

| Signal | Number | Android Trigger | Action |
|---|---|---|---|
| SIGTERM | 15 | `am force-stop` | Save state, exit cleanly |
| SIGKILL | 9 | OOM killer | Cannot handle — need watchdog |
| SIGHUP | 1 | Terminal disconnect | Reinitialize connections |
| SIGUSR1 | 10 | Custom | Checkpoint state |
| SIGUSR2 | 12 | Custom | Reload configuration |
| SIGSTOP | 19 | `adb shell kill -STOP` | Pause, wait for SIGCONT |

## Code Guidance

```javascript
// src/android/signal-handler.mjs

export class SignalHandler {
  #handlers = new Map();
  #checkpointFn = null;

  constructor(checkpointFn) {
    this.#checkpointFn = checkpointFn;
  }

  install() {
    // Graceful shutdown
    process.on('SIGTERM', () => this.#handle('SIGTERM'));
    process.on('SIGINT', () => this.#handle('SIGINT'));

    // Terminal disconnect
    process.on('SIGHUP', () => this.#handle('SIGHUP'));

    // Manual checkpoint
    process.on('SIGUSR1', () => this.#handle('SIGUSR1'));

    // Config reload
    process.on('SIGUSR2', () => this.#handle('SIGUSR2'));

    // Prevent crashes from unhandled errors
    process.on('uncaughtException', (err) => {
      console.error('Uncaught exception:', err.message);
      this.#checkpointFn?.();
      process.exit(1);
    });

    process.on('unhandledRejection', (reason) => {
      console.error('Unhandled rejection:', reason);
    });
  }

  #handle(signal) {
    console.log(`Received ${signal}`);
    switch (signal) {
      case 'SIGTERM':
      case 'SIGINT':
        this.#checkpointFn?.();
        process.exit(0);
        break;
      case 'SIGHUP':
        // Reinitialize connections
        this.#handlers.get('reinit')?.();
        break;
      case 'SIGUSR1':
        this.#checkpointFn?.();
        break;
      case 'SIGUSR2':
        this.#handlers.get('reload')?.();
        break;
    }
  }

  on(event, handler) {
    this.#handlers.set(event, handler);
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| SIGKILL cannot be caught | Data loss | Watchdog + checkpointing |
| SIGHUP flood | Rapid reinit loops | Debounce reinit |
| Exception in handler | Crash | Wrap all handlers in try/catch |

## Dependencies
- Node.js process signals (built-in)

## Pitfalls
- SIGKILL is uncatchable — this is why OOM kills lose data
- SIGHUP may fire frequently on network changes
- `uncaughtException` should save state but not continue running
- Some signals may be blocked by seccomp