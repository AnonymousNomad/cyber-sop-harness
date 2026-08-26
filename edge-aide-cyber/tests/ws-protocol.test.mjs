import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { MessageTypes, validateMessage } from '../src/lib/ws-protocol.mjs';

describe('ws protocol validation', () => {
  it('accepts valid command message', () => {
    const result = validateMessage(JSON.stringify({ type: 'command', payload: { text: '/status' } }));
    assert.ok(result.ok);
    assert.equal(result.message.type, 'command');
  });

  it('rejects non-JSON input', () => {
    const result = validateMessage('not json at all');
    assert.ok(!result.ok);
    assert.equal(result.code, 'PARSE_ERROR');
  });

  it('rejects JSON array', () => {
    const result = validateMessage('[1,2,3]');
    assert.ok(!result.ok);
    assert.equal(result.code, 'PARSE_ERROR');
  });

  it('rejects unknown message type', () => {
    const result = validateMessage(JSON.stringify({ type: 'hack', payload: {} }));
    assert.ok(!result.ok);
    assert.equal(result.code, 'UNKNOWN_TYPE');
  });

  it('rejects missing type field', () => {
    const result = validateMessage(JSON.stringify({ payload: {} }));
    assert.ok(!result.ok);
    assert.equal(result.code, 'UNKNOWN_TYPE');
  });

  it('rejects oversized messages', () => {
    const huge = 'x'.repeat(1024 * 1024 + 1);
    const result = validateMessage(huge);
    assert.ok(!result.ok);
    assert.equal(result.code, 'MESSAGE_TOO_LARGE');
  });

  it('rejects null input', () => {
    const result = validateMessage(null);
    assert.ok(!result.ok);
    assert.equal(result.code, 'INVALID_INPUT');
  });

  it('all MessageTypes values are valid types in validator', () => {
    for (const type of Object.values(MessageTypes)) {
      const msg = validateMessage(JSON.stringify({ type, payload: {} }));
      if (!msg.ok && msg.code === 'UNKNOWN_TYPE') {
        assert.fail(`MessageTypes.${type} not recognized by validator`);
      }
    }
  });
});
