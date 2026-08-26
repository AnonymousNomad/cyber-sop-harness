import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { createModelProvider, ModelProviderError } from '../src/model/provider.mjs';
import { createHash } from 'node:crypto';

describe('model provider', () => {
  describe('pinModel', () => {
    it('rejects missing model file', async () => {
      const provider = createModelProvider({});
      await assert.rejects(
        () => provider.pinModel('/nonexistent/model.gguf', 'abc'),
        ModelProviderError
      );
    });

    it('rejects file smaller than 1KB', async () => {
      const tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'edge-model-test-'));
      const filePath = path.join(tmpDir, 'tiny.gguf');
      await fs.writeFile(filePath, Buffer.alloc(10));
      const provider = createModelProvider({});
      await assert.rejects(
        () => provider.pinModel(filePath, 'abc'),
        (err) => err.code === 'MODEL_TOO_SMALL'
      );
    });

    it('accepts correctly hashed model file', async () => {
      const tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'edge-model-test-'));
      const filePath = path.join(tmpDir, 'model.gguf');
      const content = Buffer.alloc(2048, 0x42);
      await fs.writeFile(filePath, content);
      const hash = createHash('sha256').update(content).digest('hex');

      const provider = createModelProvider({});
      await provider.pinModel(filePath, hash);

      assert.ok(provider.isPinned);
      assert.equal(provider.modelHash, hash);
    });

    it('rejects wrong hash', async () => {
      const tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'edge-model-test-'));
      const filePath = path.join(tmpDir, 'model.gguf');
      await fs.writeFile(filePath, Buffer.alloc(2048, 0x42));

      const provider = createModelProvider({});
      await assert.rejects(
        () => provider.pinModel(filePath, 'wrong_hash_value_000000000000000000000000000'),
        (err) => err.code === 'HASH_MISMATCH'
      );
    });
  });

  describe('checkHealth', () => {
    it('returns false when llama-server is not running', async () => {
      const provider = createModelProvider({ host: 'http://127.0.0.1:59999' });
      const healthy = await provider.checkHealth();
      assert.equal(healthy, false);
      assert.equal(provider.isReady, false);
    });
  });
});
