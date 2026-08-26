# Edge Architecture Decision

## What To Do
Lock the architectural pattern: single-process Node.js daemon serving HTTP+WebSocket on loopback, with browser-based terminal UI. No .NET runtime, no native modules beyond llama.cpp HTTP client. All governance in pure JavaScript.

## Why
- Termux provides Node.js natively on Android
- Browser access to localhost works on all tablets without special permissions
- Single process eliminates IPC complexity on resource-constrained devices
- Pure JS governance avoids cross-runtime serialization overhead
- llama.cpp already proven working via HTTP API from prior benchmarks

## Code Guidance
Use ESM (`"type": "module"` in package.json). Structure:
```
src/
  server.mjs          # entry point: HTTP + WS setup
  routes/
    health.mjs        # GET /api/health
    command.mjs       # POST /api/command (terminal input)
    engagement.mjs    # CRUD for engagement manifest
    evidence.mjs      # evidence queries
    model.mjs         # model status, pin, serve
  governance/
    policy-engine.mjs
    permit-issuer.mjs
    scope-evaluator.mjs
    evidence-chain.mjs
    secret-vault.mjs
  tools/
    registry.mjs      # frozen adapter registry
    adapters/
      dns-reverse.mjs
      http-headers.mjs
      port-scan.mjs
  model/
    provider.mjs      # llama.cpp client
    context-manager.mjs
    cipher-state.mjs
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Daemon exposed on non-loopback | Remote code execution | Bind to 127.0.0.1 explicitly; reject other binds |
| Single process crash kills everything | Loss of active state | Implement PID file + auto-restart wrapper script |
| No separation between model inference and governance | Model output influences policy decisions | Governance runs synchronously before dispatching to model |
| Memory pressure from single large heap | OOM kill | Monitor RSS, refuse operations above threshold |

## Dependencies
- Node.js >= 18.x (Termux package)
- ws (^8.x) for WebSocket
- zod (^3.x) for schema validation
- No other runtime dependencies (zero-dependency core principle)

## Pitfalls & Bugs
- Node.js in Termux may not have access to all system calls due to Android SELinux policies.
- `process.on('SIGTERM')` may not fire reliably when Android kills the app; implement PID-file-based stale detection.
- WebSocket connections drop when the screen locks; the client must reconnect gracefully.
- Large JSON payloads over WS can block the event loop; set max payload size and use streaming for tool outputs.
