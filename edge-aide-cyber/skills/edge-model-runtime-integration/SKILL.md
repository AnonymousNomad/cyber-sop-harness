# Edge Model Runtime Integration

## What To Do
Integrate llama.cpp server as the local model inference backend. Connect via HTTP on loopback. Implement hash-pinned model loading, health checks, streaming token output, and graceful fallback when the model server is unavailable.

## Why
The model is the reasoning engine but must never have direct authority. It runs as a separate llama-server process that this daemon talks to over HTTP. This isolation means a model crash doesn't take down governance.

## Code Guidance
```javascript
// src/model/provider.mjs
import { createHash } from 'node:crypto';
import fs from 'node:fs/promises';

const LLAMA_HOST = 'http://127.0.0.1:8080';

export function createModelProvider(config) {
  let ready = false;
  let modelHash = null;

  async function verifyAndPin(modelPath, expectedSha256) {
    const fileBuffer = await fs.readFile(modelPath);
    const actual = createHash('sha256').update(fileBuffer).digest('hex');
    if (actual !== expectedSha256) {
      throw new Error(`model hash mismatch: expected ${expectedSha256}, got ${actual}`);
    }
    modelHash = actual;
  }

  async function checkHealth() {
    try {
      const res = await fetch(`${LLAMA_HOST}/health`);
      return res.ok;
    } catch { return false; }
  }

  async function* streamCompletion(messages, opts = {}) {
    const body = JSON.stringify({
      messages,
      temperature: opts.temperature ?? 0.1,
      max_tokens: opts.maxTokens ?? 512,
      stream: true,
    });

    const res = await fetch(`${LLAMA_HOST}/v1/chat/completions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
    });

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop() || '';
      for (const line of lines) {
        if (!line.startsWith('data: ') || line === 'data: [DONE]') continue;
        const data = JSON.parse(line.slice(6));
        const token = data.choices?.[0]?.delta?.content;
        if (token) yield token;
      }
    }
  }

  return {
    verifyAndPin, checkHealth, streamCompletion,
    get isReady() { return ready; },
    get modelHash() { return modelHash; },
  };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Model file tampered between download and load | Poisoned model behavior | SHA-256 verification before every startup |
| llama-server binds to non-loopback | Remote model access | Launch with `--host 127.0.0.1`; verify |
| Prompt injection via user input in context | Model produces malicious proposal | Treat all model output as untrusted; policy engine decides |
| Context window overflow | Truncated or garbage output | Context manager enforces budget before sending |
| Model returns non-JSON when JSON expected | Parser failure | Strict parser rejects; never guess |

## Dependencies
- llama.cpp server binary (compiled for aarch64)
- LFM2.5-1.2B-Thinking-Q4_K_M.gguf (pinned: `7223a2202405b02e8e1e6c5baa543c43dc98c1d9741a5c2a0ee1583212e1231b`)
- Node.js built-in `fetch` (available in Node >= 18)

## Pitfalls & Bugs
- llama.cpp's `/health` endpoint may not exist in older versions; use `/v1/models` as fallback.
- SSE streaming format from llama.cpp may include partial JSON lines; buffer and split carefully.
- The `fetch` API in Node.js doesn't support AbortController timeout by default; wrap with `AbortSignal.timeout()`.
- LFM2.5 uses a custom chat template (`<|startoftext|>`, `<|endoftext|>`, etc.) — do NOT use ChatML or Alpaca format.
- If llama-server is killed by Android OOM, the daemon should detect this via health check and report degraded status, not crash.
