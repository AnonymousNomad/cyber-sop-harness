import { describe, it, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { createFileBoundary } from '../src/lib/file-boundary.mjs';
import { createFileWatcher } from '../src/autodebug/watcher.mjs';
import { checkFile } from '../src/autodebug/detector.mjs';
import { createAutoFixer } from '../src/autodebug/fixer.mjs';
import { createNotifier } from '../src/autodebug/notifier.mjs';

let tmpDir, boundary;

beforeEach(async () => {
  tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'edge-autodebug-'));
  boundary = createFileBoundary(tmpDir);
});

describe('file watcher', () => {
  it('detects created files', async () => {
    const watcher = createFileWatcher(boundary, { pollInterval: 100 });
    await boundary.writeFile('test.mjs', 'console.log("hi");');
    const changes = await watcher.detectChanges();
    assert.ok(changes.length >= 1);
    assert.equal(changes[0].type, 'created');
    assert.equal(changes[0].path, 'test.mjs');
  });

  it('detects newly created files after initial scan', async () => {
    const watcher = createFileWatcher(boundary, { pollInterval: 100 });
    await boundary.writeFile('test.mjs', 'console.log("hi");');
    await watcher.detectChanges();
    await boundary.writeFile('second.mjs', 'console.log("new");');
    const changes = await watcher.detectChanges();
    assert.ok(changes.some(c => c.type === 'created' && c.path === 'second.mjs'));
  });

  it('ignores node_modules', async () => {
    const watcher = createFileWatcher(boundary, { pollInterval: 100 });
    await boundary.mkdir('node_modules/pkg');
    await boundary.writeFile('node_modules/pkg/index.js', 'module.exports = {};');
    const changes = await watcher.detectChanges();
    assert.ok(!changes.some(c => c.path.includes('node_modules')));
  });

  it('only watches specified extensions', async () => {
    const watcher = createFileWatcher(boundary, { pollInterval: 100, extensions: ['.js'] });
    await boundary.writeFile('readme.md', '# hello');
    await boundary.writeFile('code.js', 'var x = 1;');
    const changes = await watcher.detectChanges();
    assert.ok(changes.some(c => c.path === 'code.js'));
    assert.ok(!changes.some(c => c.path === 'readme.md'));
  });

  it('tracks file count', async () => {
    const watcher = createFileWatcher(boundary, { pollInterval: 100 });
    await boundary.writeFile('a.js', 'var a = 1;');
    await boundary.writeFile('b.js', 'var b = 2;');
    await watcher.detectChanges();
    assert.equal(watcher.fileCount, 2);
  });
});

describe('syntax detector', () => {
  it('passes valid JS', async () => {
    await boundary.writeFile('valid.mjs', 'const x = 1;\nexport default x;');
    const result = await checkFile(tmpDir, 'valid.mjs');
    assert.equal(result.clean, true);
    assert.equal(result.errors.length, 0);
  });

  it('catches syntax errors in JS', async () => {
    await boundary.writeFile('broken.mjs', 'const x = ;');
    const result = await checkFile(tmpDir, 'broken.mjs');
    assert.equal(result.clean, false);
    assert.ok(result.errors.length > 0);
    assert.equal(result.errors[0].severity, 'error');
  });

  it('catches invalid JSON', async () => {
    await boundary.writeFile('bad.json', '{"key": "value",}');
    const result = await checkFile(tmpDir, 'bad.json');
    assert.equal(result.clean, false);
    assert.ok(result.errors.length > 0);
  });

  it('passes valid JSON', async () => {
    await boundary.writeFile('good.json', '{"key": "value"}');
    const result = await checkFile(tmpDir, 'good.json');
    assert.equal(result.clean, true);
  });

  it('returns unchecked for unknown extension', async () => {
    await boundary.writeFile('image.png', 'binary');
    const result = await checkFile(tmpDir, 'image.png');
    assert.equal(result.checked, false);
  });
});

describe('auto-fixer', () => {
  it('registers pending fixes', () => {
    const fixer = createAutoFixer({ modelProvider: null, fileBoundary: boundary, checkFile, evidenceChain: null });
    fixer.registerPending('src/app.mjs', [{ line: 1, message: 'error' }]);
    assert.equal(fixer.pendingCount, 1);
  });

  it('resolves pending fixes', () => {
    const fixer = createAutoFixer({ modelProvider: null, fileBoundary: boundary, checkFile, evidenceChain: null });
    fixer.registerPending('src/app.mjs', [{ line: 1, message: 'error' }]);
    fixer.resolvePending('src/app.mjs');
    assert.equal(fixer.pendingCount, 0);
  });

  it('reports model unavailable when no provider', async () => {
    const fixer = createAutoFixer({ modelProvider: { isReady: false }, fileBoundary: boundary, checkFile, evidenceChain: null });
    const result = await fixer.attemptFix('test.mjs', [{ line: 1, message: 'err' }]);
    assert.equal(result.ok, false);
    assert.ok(result.reason.includes('not available'));
  });

  it('toggles auto mode', () => {
    const fixer = createAutoFixer({ modelProvider: null, fileBoundary: boundary, checkFile, evidenceChain: null });
    assert.equal(fixer.autoMode, false);
    fixer.autoMode = true;
    assert.equal(fixer.autoMode, true);
  });
});

describe('notifier', () => {
  it('sends detected errors to clients', () => {
    const received = [];
    const fakeClients = [{ readyState: 1, send: (data) => received.push(JSON.parse(data)) }];
    const notifier = createNotifier();
    notifier.notifyErrorsDetected(fakeClients, [{ path: 'app.mjs', errors: [{ message: 'err' }] }]);
    assert.equal(received.length, 1);
    assert.equal(received[0].type, 'autodebug.detected');
    assert.equal(received[0].payload.count, 1);
  });

  it('sends fix success', () => {
    const received = [];
    const fakeClients = [{ readyState: 1, send: (data) => received.push(JSON.parse(data)) }];
    const notifier = createNotifier();
    notifier.notifyFixResult(fakeClients, { ok: true, filePath: 'app.mjs', errorsFixed: 3 });
    assert.equal(received[0].type, 'autodebug.fixed');
    assert.equal(received[0].payload.errorsFixed, 3);
  });

  it('skips closed clients', () => {
    const received = [];
    const fakeClients = [{ readyState: 3, send: (data) => received.push(data) }];
    const notifier = createNotifier();
    notifier.notifyErrorsDetected(fakeClients, []);
    assert.equal(received.length, 0);
  });
});
