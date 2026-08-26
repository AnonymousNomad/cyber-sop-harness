import { createHash } from 'node:crypto';
import fs from 'node:fs/promises';

const DEFAULT_HOST = 'http://127.0.0.1:8081';
const REQUEST_TIMEOUT_MS = 120000;

export class ModelProviderError extends Error {
  constructor(code, message) {
    super(message);
    this.name = 'ModelProviderError';
    this.code = code;
  }
}

export function createModelProvider(config = {}) {
  const host = config.host || DEFAULT_HOST;
  const maxTokensDefault = config.maxTokens || 512;
  const temperatureDefault = config.temperature ?? 0.1;
  const modelName = config.modelName || 'local';
  const isRemote = config.remote === true;

  let pinned = false;
  let modelHash = null;
  let modelPath = null;
  let ready = false;

  async function pinModel(filePath, expectedSha256) {
    const stat = await fs.stat(filePath).catch(() => null);
    if (!stat) throw new ModelProviderError('MODEL_NOT_FOUND', `model file not found: ${filePath}`);
    if (stat.size < 1024) throw new ModelProviderError('MODEL_TOO_SMALL', 'model file suspiciously small');

    const hash = createHash('sha256');
    const stream = await fs.open(filePath, 'r');
    try {
      const buffer = Buffer.alloc(1024 * 1024);
      while (true) {
        const { bytesRead } = await stream.read(buffer);
        if (bytesRead === 0) break;
        hash.update(buffer.subarray(0, bytesRead));
      }
    } finally {
      await stream.close();
    }

    const actual = hash.digest('hex');
    if (actual !== expectedSha256) {
      throw new ModelProviderError('HASH_MISMATCH', `expected ${expectedSha256}, got ${actual}`);
    }

    modelHash = actual;
    modelPath = filePath;
    pinned = true;
  }

  async function checkHealth() {
    try {
      const res = await fetch(`${host}/health`, { signal: AbortSignal.timeout(5000) });
      if (!res.ok && res.status !== 200) return false;
      ready = true;
      return true;
    } catch {
      try {
        const res = await fetch(`${host}/v1/models`, { signal: AbortSignal.timeout(5000) });
        if (res.ok) { ready = true; return true; }
      } catch {}
      ready = false;
      return false;
    }
  }

  async function* streamCompletion(messages, opts = {}) {
    if (!ready) {
      const healthy = await checkHealth();
      if (!healthy) throw new ModelProviderError('MODEL_UNAVAILABLE', `llama-server not reachable at ${host}`);
    }

    const body = JSON.stringify({
      messages,
      temperature: opts.temperature ?? temperatureDefault,
      max_tokens: opts.maxTokens ?? maxTokensDefault,
      stream: true,
      stop: opts.stop,
    });

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), opts.timeout ?? REQUEST_TIMEOUT_MS);

    try {
      const res = await fetch(`${host}/v1/chat/completions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body,
        signal: controller.signal,
      });

      if (!res.ok) {
        const detail = await res.text().catch(() => '');
        throw new ModelProviderError('INFERENCE_ERROR', `llama-server returned ${res.status}: ${detail.slice(0, 200)}`);
      }

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
          const trimmed = line.trim();
          if (!trimmed.startsWith('data: ') || trimmed === 'data: [DONE]') continue;
          try {
            const data = JSON.parse(trimmed.slice(6));
            const token = data.choices?.[0]?.delta?.content;
            if (token) yield token;
          } catch {}
        }
      }
    } catch (err) {
      if (err.name === 'AbortError') throw new ModelProviderError('TIMEOUT', 'inference timed out');
      throw err;
    } finally {
      clearTimeout(timer);
    }
  }

  async function complete(messages, opts = {}) {
    let result = '';
    for await (const token of streamCompletion(messages, opts)) {
      result += token;
    }
    return result;
  }

  return {
    pinModel,
    checkHealth,
    streamCompletion,
    complete,
    get isReady() { return ready; },
    get isPinned() { return pinned; },
    get modelHash() { return modelHash; },
    get modelPath() { return modelPath; },
    get host() { return host; },
    get name() { return modelName; },
    get remote() { return isRemote; },
  };
}

export function createRemoteModelProvider(config = {}) {
  const host = config.host;
  if (!host) throw new Error('remote provider requires host URL');
  const apiKey = config.apiKey || null;
  const model = config.modelName || 'north-mini-code';

  async function checkHealth() {
    try {
      const res = await fetch(`${host}/v1/models`, {
        headers: apiKey ? { Authorization: `Bearer ${apiKey}` } : {},
        signal: AbortSignal.timeout(5000),
      });
      return res.ok;
    } catch { return false; }
  }

  async function* streamCompletion(messages, opts = {}) {
    const body = JSON.stringify({
      model,
      messages,
      temperature: opts.temperature ?? 0.1,
      max_tokens: opts.maxTokens ?? 1024,
      stream: true,
    });

    const res = await fetch(`${host}/v1/chat/completions`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(apiKey ? { Authorization: `Bearer ${apiKey}` } : {}),
      },
      body,
    });

    if (!res.ok) throw new Error(`remote model returned ${res.status}`);

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
        const trimmed = line.trim();
        if (!trimmed.startsWith('data: ') || trimmed === 'data: [DONE]') continue;
        try {
          const data = JSON.parse(trimmed.slice(6));
          const token = data.choices?.[0]?.delta?.content;
          if (token) yield token;
        } catch {}
      }
    }
  }

  async function complete(messages, opts = {}) {
    let result = '';
    for await (const token of streamCompletion(messages, opts)) result += token;
    return result;
  }

  return Object.freeze({
    checkHealth,
    streamCompletion,
    complete,
    get isReady() { return false; },
    get isPinned() { return false; },
    get name() { return model; },
    get remote() { return true; },
    get host() { return host; },
  });
}
