# Edge WebSocket API

## What To Do
Design the WebSocket message protocol for real-time terminal interaction, status updates, and streaming model output. All messages are JSON with a `type` field.

## Why
The terminal UI needs bidirectional communication: commands flow up, results stream down. HTTP alone can't stream model tokens or push evidence updates. WebSocket on loopback is the lightest option that works in mobile browsers.

## Code Guidance
```javascript
// Message types
const MessageTypes = Object.freeze({
  // Client → Server
  COMMAND: 'command',           // Terminal command submission
  ENGAGEMENT_LOAD: 'engagement.load',
  MODEL_PIN: 'model.pin',
  PERMIT_REQUEST: 'permit.request',
  EVIDENCE_QUERY: 'evidence.query',

  // Server → Client
  OUTPUT: 'output',             // Text output for terminal
  STATUS: 'status',             // Status bar update
  MODEL_TOKEN: 'model.token',   // Streaming token from inference
  EVIDENCE_ENTRY: 'evidence.entry',
  ERROR: 'error',
  READY: 'ready',
});

function handleMessage(ws, raw, state) {
  let msg;
  try {
    msg = JSON.parse(raw);
  } catch {
    send(ws, MessageTypes.ERROR, { code: 'PARSE_ERROR', message: 'Invalid JSON' });
    return;
  }

  if (!msg.type || !Object.values(MessageTypes).includes(msg.type)) {
    send(ws, MessageTypes.ERROR, { code: 'UNKNOWN_TYPE', message: `Unknown type: ${msg.type}` });
    return;
  }

  switch (msg.type) {
    case MessageTypes.COMMAND:
      handleCommand(ws, msg.payload, state);
      break;
    default:
      send(ws, MessageTypes.ERROR, { code: 'NOT_IMPLEMENTED', message: `${msg.type} handler pending` });
  }
}

function send(ws, type, payload) {
  if (ws.readyState !== 1) return;
  ws.send(JSON.stringify({ type, payload, at: new Date().toISOString() }));
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Oversized message | Memory exhaustion / DoS | Set maxPayload on WSS (default 1 MiB) |
| Malformed JSON | Crash | try/catch around parse; structured error response |
| Injection via message fields | Command execution | Never interpolate message data into shell strings |
| Connection flooding | Resource exhaustion | Limit concurrent connections (default 3) |
| Replay of old messages | Duplicate actions | Include nonce/timestamp; reject stale messages |

## Dependencies
- `ws` (^8.x)

## Pitfalls & Bugs
- Mobile browsers may buffer WS messages during background; flush on visibility change.
- Large tool outputs should be chunked across multiple `OUTPUT` messages rather than sent as one giant frame.
- `ws.readyState` can change between check and send; wrap in try/catch.
- JSON.stringify of circular objects throws; ensure all payloads are plain objects.
- Heartbeat/ping-pong is needed because Android's network stack may silently drop idle TCP connections.
