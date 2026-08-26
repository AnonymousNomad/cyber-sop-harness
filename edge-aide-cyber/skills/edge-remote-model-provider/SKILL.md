# Edge Remote Model Provider

## What To Do
Implement a provider that connects to a remote llama.cpp server (e.g., North Mini Code running on a laptop) over LAN via OpenAI-compatible API. Enables the tablet to use larger models when network is available, falling back to local LFM2.5 when not.

## Why
North Mini Code (30.5B MoE) is too large for the tablet (needs 18+ GB RAM at Q4, tablet has 7.2 GB). Running it on a laptop and connecting the tablet over LAN gives the best of both worlds: edge governance + cloud-class reasoning.

## Architecture
```
Tablet (edge-aide-cyber)
  → LFM2.5 local (fast, governed, always available)
  → North Mini Code remote (powerful, requires laptop on same network)
```

## Code Guidance
- `src/model/provider.mjs` exports both `createModelProvider` (local) and `createRemoteModelProvider` (remote)
- Remote provider connects to `{REMOTE_MODEL_HOST}/v1/chat/completions` with OpenAI-compatible SSE streaming
- Supports optional `REMOTE_MODEL_KEY` for API key authentication
- Health check: `GET /v1/models` endpoint
- Environment variables: `REMOTE_MODEL_HOST`, `REMOTE_MODEL_NAME`, `REMOTE_MODEL_KEY`

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Remote model on non-secure network | Man-in-the-middle | Use HTTPS when available; document LAN-only recommendation |
| Remote model returns untrusted output | Governance bypass | Same policy engine applies; model output is always untrusted input |
| Network interruption mid-inference | Partial response | AbortSignal timeout; retry with local model fallback |
| Remote model hallucinates tool calls | Unexpected execution | Strict JSON parser rejects malformed output |

## Dependencies
- Remote llama.cpp server with OpenAI-compatible API (e.g., `llama-server --host 0.0.0.0`)
- Network connectivity between tablet and laptop (same LAN)
- North Mini Code Q4_K_M GGUF (~18 GB) on the laptop

## Pitfalls
- Remote model adds 1-5ms network latency per request (negligible on LAN)
- `REMOTE_MODEL_HOST` must be set before server boot; cannot change at runtime
- Remote provider's `isReady` always returns false (health checked at boot, not continuously)
- If laptop sleeps/disconnects, remote calls fail gracefully; local model remains available
- North Mini Code uses Cohere2 MoE architecture — verify llama.cpp version supports it
