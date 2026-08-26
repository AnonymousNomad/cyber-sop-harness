import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { defineAdapter, createAdapterRegistry } from '../src/tools/registry.mjs';
import { sanitizeText, sanitizeObject } from '../src/tools/sanitizer.mjs';

describe('tool adapter registry', () => {
  const fakeAdapter = defineAdapter({
    name: 'test.tool',
    capability: 'test.cap',
    riskLevel: 'R1',
    execute: async () => ({ ok: true }),
  });

  const anotherAdapter = defineAdapter({
    name: 'other.tool',
    capability: 'other.cap',
    riskLevel: 'R2',
    execute: async () => ({ ok: true, data: { x: 1 } }),
  });

  it('creates frozen registry', () => {
    const registry = createAdapterRegistry([fakeAdapter, anotherAdapter]);
    assert.equal(registry.size, 2);
    assert.ok(Object.isFrozen(registry));
  });

  it('gets adapter by name', () => {
    const registry = createAdapterRegistry([fakeAdapter]);
    assert.ok(registry.has('test.tool'));
    assert.equal(registry.get('test.tool').name, 'test.tool');
  });

  it('returns null for unknown tool', () => {
    const registry = createAdapterRegistry([fakeAdapter]);
    assert.equal(registry.get('nonexistent'), null);
    assert.ok(!registry.has('nonexistent'));
  });

  it('lists all adapters with metadata', () => {
    const registry = createAdapterRegistry([fakeAdapter, anotherAdapter]);
    const list = registry.list();
    assert.equal(list.length, 2);
    assert.deepEqual(list[0], { name: 'test.tool', capability: 'test.cap', riskLevel: 'R1' });
  });

  it('rejects adapter without required fields', () => {
    assert.throws(() => defineAdapter({ name: 'incomplete' }), /missing required fields/);
  });

  it('cannot add adapters at runtime (frozen)', () => {
    const registry = createAdapterRegistry([fakeAdapter]);
    assert.throws(() => registry.set('new', fakeAdapter));
  });
});

describe('output sanitizer', () => {
  it('redacts bearer tokens', () => {
    const result = sanitizeText('Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.sig');
    assert.ok(!result.includes('eyJhbGciOiJIUzI1NiIs'));
    assert.ok(result.includes('[REDACTED]') || result.includes('[JWT_REDACTED]'));
  });

  it('redacts AWS access keys', () => {
    const result = sanitizeText('key=AKIAIOSFODNN7EXAMPLE');
    assert.ok(!result.includes('AKIAIOSFODNN7EXAMPLE'));
    assert.ok(result.includes('[AWS_KEY_REDACTED]'));
  });

  it('redacts JWTs', () => {
    const jwt = 'eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.signature_here';
    const result = sanitizeText(`token=${jwt}`);
    assert.ok(!result.includes(jwt));
  });

  it('redacts credentials in URLs', () => {
    const result = sanitizeText('https://admin:p4ssw0rd@example.com/api');
    assert.ok(!result.includes('p4ssw0rd'));
    assert.ok(result.includes('[USER]:[PASS]'));
  });

  it('redacts GitHub tokens', () => {
    const result = sanitizeText('ghp_AbCdEfGhIjKlMnOpQrStUvWxYz1234567890');
    assert.ok(!result.includes('ghp_AbCdEfGhIjKlMnOpQrStUvWxYz1234567890'));
  });

  it('sanitizes nested objects', () => {
    const input = {
      headers: { authorization: 'Bearer sk-secret-token-here-123' },
      body: 'normal text',
      nested: { key: 'AKIAIOSFODNN7EXAMPLE' },
    };
    const clean = sanitizeObject(input);
    assert.ok(!JSON.stringify(clean).includes('sk-secret-token'));
    assert.ok(!JSON.stringify(clean).includes('AKIAIOSFODNN7'));
    assert.equal(clean.body, 'normal text');
  });

  it('preserves non-sensitive content', () => {
    const result = sanitizeText('This is normal output with no secrets');
    assert.equal(result, 'This is normal output with no secrets');
  });

  it('handles arrays of objects', () => {
    const items = [
      { token: 'ghp_AbCdEfGhIjKlMnOpQrStUvWxYz1234567890' },
      { value: 'safe' },
    ];
    const clean = sanitizeObject(items);
    assert.ok(!JSON.stringify(clean).includes('ghp_'));
    assert.equal(clean[1].value, 'safe');
  });
});
