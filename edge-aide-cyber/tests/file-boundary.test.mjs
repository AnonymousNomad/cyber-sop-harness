import { describe, it, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { createFileBoundary, PathEscapeError } from '../src/lib/file-boundary.mjs';

let tmpDir, boundary;

beforeEach(async () => {
  tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'edge-cyber-test-'));
  boundary = createFileBoundary(tmpDir);
});

describe('file boundary', () => {
  it('resolves paths within the root', () => {
    const resolved = boundary.resolve('data', 'test.txt');
    assert.ok(resolved.startsWith(tmpDir));
    assert.ok(resolved.includes('test.txt'));
  });

  it('rejects path traversal with ..', () => {
    assert.throws(() => boundary.resolve('../../etc/passwd'), PathEscapeError);
  });

  it('rejects absolute path outside root', () => {
    assert.throws(() => boundary.resolve('/etc/passwd'), PathEscapeError);
  });

  it('rejects null byte injection', async () => {
    try {
      boundary.resolve('data\x00/../../etc/passwd');
      assert.fail('should have thrown');
    } catch (err) {
      assert.ok(err instanceof PathEscapeError);
    }
  });

  it('allows writing and reading files within root', async () => {
    await boundary.writeFile('test-file.txt', 'hello world');
    const content = await boundary.readFile('test-file.txt');
    assert.equal(content, 'hello world');
  });

  it('creates parent directories on write', async () => {
    await boundary.writeFile('deep/nested/dir/file.txt', 'content');
    const content = await boundary.readFile('deep/nested/dir/file.txt');
    assert.equal(content, 'content');
  });

  it('lists directory contents', async () => {
    await boundary.writeFile('a.txt', 'a');
    await boundary.writeFile('b.txt', 'b');
    const entries = await boundary.listDir();
    assert.ok(entries.includes('a.txt'));
    assert.ok(entries.includes('b.txt'));
  });

  it('checks existence', async () => {
    await boundary.writeFile('exists.txt', 'yes');
    assert.ok(await boundary.exists('exists.txt'));
    assert.ok(!(await boundary.exists('does-not-exist.txt')));
  });

  it('appends to files', async () => {
    await boundary.writeFile('append-test.txt', 'line1\n');
    await boundary.appendFile('append-test.txt', 'line2\n');
    const content = await boundary.readFile('append-test.txt');
    assert.equal(content, 'line1\nline2\n');
  });

  it('root property returns resolved root', () => {
    assert.equal(boundary.root, path.resolve(tmpDir));
  });

  it('handles deeply nested traversal attempt', () => {
    assert.throws(() => boundary.resolve('a/b/c/../../../../../etc/shadow'), PathEscapeError);
  });
});
