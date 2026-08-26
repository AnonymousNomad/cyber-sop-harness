import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { WAKE_WORDS } from '../src/model/voice-assistant.mjs';

describe('voice assistant', () => {
  it('exports wake words', () => {
    assert.ok(Array.isArray(WAKE_WORDS));
    assert.ok(WAKE_WORDS.includes('hey cipher'));
    assert.ok(WAKE_WORDS.length >= 2);
  });

  it('wake words are all lowercase', () => {
    for (const word of WAKE_WORDS) {
      assert.equal(word, word.toLowerCase());
    }
  });
});
