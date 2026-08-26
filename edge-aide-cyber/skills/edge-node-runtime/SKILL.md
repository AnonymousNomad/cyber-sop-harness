# Edge Node.js Runtime Foundation

## What To Do
Create the single-process Node.js ESM server that binds to loopback, initializes all subsystems in order (device profile → governance → model → tools → UI), and provides a health endpoint.

## Why
This is the skeleton everything else hangs on. Getting startup ordering wrong means governance might not be ready before tools try to use it. The health endpoint lets the browser UI know when to connect.

## Code Guidance
```javascript
// src/server.mjs
import http from 'node:http';
import { WebSocketServer } from 'ws';
import { captureDeviceProfile } from './lib/device-profile.mjs';

const PORT = parseInt(process.env.PORT || '7420', 10);
const HOST = '127.0.0.1'; // NEVER 0.0.0.0

const state = { profile: null, governance: null, model: null, tools: null };

async function boot() {
  state.profile = captureDeviceProfile();

  // Phase gate: each subsystem must initialize before dependent ones
  state.governance = await initGovernance(state.profile);
  state.model = await initModel(state.governance);
  state.tools = await initTools(state.governance);

  const server = http.createServer(requestHandler);
  const wss = new WebSocketServer({ server });

  wss.on('connection', (ws, req) => {
    if (req.socket.remoteAddress !== '127.0.0.1') {
      ws.close(4003, 'loopback only');
      return;
    }
    handleConnection(ws, state);
  });

  server.listen(PORT, HOST, () => {
    console.log(`Edge AIDE Cyber listening on ${HOST}:${PORT}`);
    writePidFile(process.pid);
  });
}

function requestHandler(req, res) {
  res.setHeader('Content-Type', 'application/json');
  if (req.url === '/api/health') {
    res.end(JSON.stringify({
      status: state.governance ? 'ready' : 'booting',
      device: state.profile,
      model: state.model?.status || 'not-loaded',
      uptime: process.uptime(),
    }));
  } else {
    res.statusCode = 404;
    res.end(JSON.stringify({ error: 'not found' }));
  }
}

process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);

async function shutdown() {
  // Flush evidence chain, close WS connections, stop llama-server
  console.log('Shutting down...');
  process.exit(0);
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Binding to 0.0.0.0 | Remote exploitation | Hardcode HOST; reject env override |
| Startup order violation | Tools available before governance | Sequential async init with explicit gates |
| Unhandled promise rejection | Silent crash | Add `process.on('unhandledRejection')` handler |
| Port already in use | Confusing failure | Check before listen; report clearly |

## Dependencies
- `ws` (^8.x)
- `zod` (^3.x) for schema validation

## Pitfalls & Bugs
- Termux may have an older Node.js version; check `process.version` at boot and warn below v18.
- Android may assign a different port if the requested one is taken; always log the actual bound address.
- `server.listen()` callback fires before all subsystems are ready; use a separate readiness flag.
- PID file must be cleaned up on exit or the restart script will think a stale process exists.
- On some Android versions, `process.kill(pid, 'SIGTERM')` from within the same process doesn't work as expected.
