import { describe, it, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { createFileBoundary } from '../src/lib/file-boundary.mjs';
import { createCipherBus } from '../src/model/cipher-state.mjs';

let tmpDir, boundary, cipher;

beforeEach(async () => {
  tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'edge-cipher-test-'));
  boundary = createFileBoundary(tmpDir);
  await boundary.mkdir('.edge-cyber');
  cipher = createCipherBus(boundary);
  await cipher.init();
});

describe('cipher state bus', () => {
  it('starts empty when no prior state exists', async () => {
    const events = await cipher.query({});
    assert.equal(events.length, 0);
  });

  it('appends and retrieves events', async () => {
    await cipher.append({ type: 'command', text: '/status' });
    await cipher.append({ type: 'approval', pattern: 'nmap-scan', decision: 'approve' });
    const events = await cipher.query({});
    assert.equal(events.length, 2);
  });

  it('filters by type', async () => {
    await cipher.append({ type: 'command', text: '/status' });
    await cipher.append({ type: 'approval', pattern: 'dns-lookup', decision: 'approve' });
    const commands = await cipher.query({ type: 'command' });
    assert.equal(commands.length, 1);
    assert.equal(commands[0].type, 'command');
  });

  it('returns most recent first (reversed)', async () => {
    await cipher.append({ type: 'event', label: 'first' });
    await cipher.append({ type: 'event', label: 'second' });
    const events = await cipher.query({ type: 'event' });
    assert.equal(events[0].label, 'second');
    assert.equal(events[1].label, 'first');
  });

  it('assigns monotonically increasing sequence numbers', async () => {
    await cipher.append({ type: 'seq-test' });
    await cipher.append({ type: 'seq-test' });
    await cipher.append({ type: 'seq-test' });
    const events = await cipher.query({ type: 'seq-test', limit: 10 });
    assert.equal(events[0].seq, 3);
    assert.equal(events[1].seq, 2);
    assert.equal(events[2].seq, 1);
  });

  it('ignores invalid append calls', async () => {
    await cipher.append(null);
    await cipher.append(undefined);
    await cipher.append('string');
    const events = await cipher.query({});
    assert.equal(events.length, 0);
  });

  it('getLearnedPreferences returns empty for insufficient data', async () => {
    const prefs = await cipher.getLearnedPreferences(3);
    assert.deepEqual(prefs, []);
  });

  it('persists across init cycles', async () => {
    await cipher.append({ type: 'persist-test', value: 'survives-restart' });
    const newBus = createCipherBus(boundary);
    await newBus.init();
    const events = await newBus.query({ type: 'persist-test' });
    assert.equal(events.length, 1);
    assert.equal(events[0].value, 'survives-restart');
  });
});
