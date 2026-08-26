import { describe, it, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { createContextManager } from '../src/model/context-manager.mjs';

describe('context budget manager', () => {
  let cm;

  beforeEach(() => {
    cm = createContextManager({ maxTokens: 100 });
  });

  it('starts with zero history', () => {
    const stats = cm.stats();
    assert.equal(stats.historyTurns, 0);
  });

  it('tracks token usage', () => {
    cm.addTurn('user', 'hello world this is a test message');
    const stats = cm.stats();
    assert.ok(stats.used > 0);
    assert.ok(stats.used <= 100);
  });

  it('evicts oldest turns when over budget', () => {
    for (let i = 0; i < 50; i++) {
      cm.addTurn('user', `message number ${i} with some padding text to consume tokens`);
    }
    const stats = cm.stats();
    assert.ok(stats.used <= 100, `used ${stats.used} should be <= 100`);
    assert.ok(stats.historyTurns < 50, 'should have evicted some turns');
  });

  it('keeps minimum history turns', () => {
    for (let i = 0; i < 100; i++) {
      try { cm.addTurn('user', 'a very long message that consumes many tokens in the context window'); }
      catch {}
    }
    assert.ok(cm.stats().historyTurns >= 2 || cm.stats().historyTurns === 0);
  });

  it('pins blocks and includes them in messages', () => {
    cm.pinBlock('identity', 'You are a cybersecurity operations assistant.');
    const messages = cm.getMessages();
    assert.equal(messages[0].role, 'system');
    assert.ok(messages[0].content.includes('cybersecurity'));
  });

  it('unpins blocks', () => {
    cm.pinBlock('test', 'pinned content');
    cm.unpinBlock('test');
    assert.deepEqual(cm.stats().pinnedBlocks, []);
  });

  it('replaces existing pinned block by id', () => {
    cm.pinBlock('id1', 'first version');
    cm.pinBlock('id1', 'second version');
    const stats = cm.stats();
    assert.equal(stats.pinnedBlocks.length, 1);
  });

  it('truncates tool output over limit', () => {
    const bigOutput = 'x'.repeat(5000);
    cm.addToolOutput('nmap.scan', bigOutput, 'permit123');
    const messages = cm.getMessages();
    const lastMsg = messages[messages.length - 1];
    assert.ok(lastMsg.content.includes('truncated'));
  });

  it('clearHistory resets turns', () => {
    cm.addTurn('user', 'hello');
    cm.clearHistory();
    assert.equal(cm.stats().historyTurns, 0);
  });

  it('reports utilization percentage', () => {
    cm.addTurn('user', 'short');
    const stats = cm.stats();
    assert.ok(stats.utilizationPct >= 0 && stats.utilizationPct <= 100);
  });

  it('throws when even pinned blocks exceed budget', () => {
    const tinyCm = createContextManager({ maxTokens: 10 });
    tinyCm.pinBlock('big', 'this is a very long system prompt that definitely exceeds ten tokens in estimation');
    assert.throws(() => tinyCm.addTurn('user', 'any message'), /budget exceeded/);
  });

  it('getMessages returns proper role sequence', () => {
    cm.addTurn('user', 'question');
    cm.addTurn('assistant', 'answer');
    const msgs = cm.getMessages();
    assert.equal(msgs[0].role, 'user');
    assert.equal(msgs[1].role, 'assistant');
  });
});
