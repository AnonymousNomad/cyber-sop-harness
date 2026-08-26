import { describe, it, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';

import { createFileBoundary } from '../src/lib/file-boundary.mjs';
import { createScopeEvaluator, cidrContains, domainMatches } from '../src/governance/scope-evaluator.mjs';
import { createPolicyEngine, DECISIONS, validateManifest, isManifestExpired } from '../src/governance/policy-engine.mjs';
import { createPermitIssuer } from '../src/governance/permit-issuer.mjs';
import { createEvidenceChain } from '../src/governance/evidence-chain.mjs';
import { createSecretVault, VaultError } from '../src/governance/secret-vault.mjs';

const FUTURE = new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString();
const PAST = new Date(Date.now() - 3600 * 1000).toISOString();

function makeManifest(overrides = {}) {
  return {
    id: 'test-engagement-001',
    operatorId: 'operator-test',
    expiresAt: FUTURE,
    scope: [
      { type: 'domain', value: '*.example.com' },
      { type: 'domain', value: '*.target.com' },
      { type: 'cidr', value: '10.0.0.0/8' },
      { type: 'url_prefix', value: 'https://api.example.com/v2' },
    ],
    allowedCapabilities: ['dns.reverse', 'http.headers', 'nmap.scan'],
    authorizedRiskLevels: ['R1', 'R2'],
    ...overrides,
  };
}

let tmpDir, boundary;

beforeEach(async () => {
  tmpDir = await fs.mkdtemp(path.join(os.tmpdir(), 'edge-gov-test-'));
  boundary = createFileBoundary(tmpDir);
});

// ─── Scope Evaluator ──────────────────────────────────────────────

describe('scope evaluator', () => {
  describe('cidrContains', () => {
    it('matches IP within /8 range', () => {
      assert.ok(cidrContains('10.0.0.0/8', '10.1.2.3'));
    });

    it('rejects IP outside range', () => {
      assert.ok(!cidrContains('10.0.0.0/8', '192.168.1.1'));
    });

    it('handles /32 exact match', () => {
      assert.ok(cidrContains('192.168.1.1/32', '192.168.1.1'));
      assert.ok(!cidrContains('192.168.1.1/32', '192.168.1.2'));
    });

    it('handles /0 matching everything IPv4', () => {
      assert.ok(cidrContains('0.0.0.0/0', '8.8.8.8'));
    });

    it('rejects IPv6 addresses', () => {
      assert.ok(!cidrContains('10.0.0.0/8', '::1'));
    });

    it('boundary check: first and last host in subnet', () => {
      assert.ok(cidrContains('192.168.1.0/24', '192.168.1.1'));
      assert.ok(cidrContains('192.168.1.0/24', '192.168.1.254'));
      assert.ok(!cidrContains('192.168.1.0/24', '192.168.2.1'));
    });
  });

  describe('domainMatches', () => {
    it('exact domain match', () => {
      assert.ok(domainMatches('example.com', 'example.com'));
    });

    it('wildcard matches subdomain', () => {
      assert.ok(domainMatches('*.example.com', 'sub.example.com'));
    });

    it('wildcard does not match root domain', () => {
      assert.ok(!domainMatches('*.example.com', 'example.com'));
    });

    it('case insensitive', () => {
      assert.ok(domainMatches('EXAMPLE.COM', 'example.COM'));
    });
  });

  describe('createScopeEvaluator', () => {
    const evaluator = createScopeEvaluator([
      { type: 'domain', value: '*.target.com' },
      { type: 'cidr', value: '172.16.0.0/12' },
    ]);

    it('allows in-scope domain', () => {
      const result = evaluator('app.target.com');
      assert.ok(result.allowed);
    });

    it('denies out-of-scope target', () => {
      const result = evaluator('evil.example.net');
      assert.ok(!result.allowed);
      assert.ok(result.reason.includes('no scope rule matches'));
    });

    it('allows in-scope CIDR', () => {
      const result = evaluator('172.20.1.5');
      assert.ok(result.allowed);
    });
  });
});

// ─── Policy Engine ────────────────────────────────────────────────

describe('policy engine', () => {
  it('validates a correct manifest', () => {
    assert.ok(validateManifest(makeManifest()));
  });

  it('rejects manifest missing required fields', () => {
    assert.throws(() => validateManifest({ id: 'x' }), /missing required field/);
  });

  it('detects expired manifest', () => {
    const expired = makeManifest({ expiresAt: PAST });
    assert.ok(isManifestExpired(expired));
    assert.ok(!isManifestExpired(makeManifest()));
  });

  it('allows action for in-scope target with authorized capability', () => {
    const engine = createPolicyEngine(makeManifest());
    const result = engine.evaluate({
      target: 'scan.target.com',
      tool: 'dns.reverse',
      riskLevel: 'R1',
    });
    assert.equal(result.decision, DECISIONS.ALLOW);
    assert.ok(result.permitRequired);
  });

  it('denies out-of-scope target', () => {
    const engine = createPolicyEngine(makeManifest());
    const result = engine.evaluate({
      target: 'unauthorized.net',
      tool: 'dns.reverse',
      riskLevel: 'R1',
    });
    assert.equal(result.decision, DECISIONS.DENY);
    assert.equal(result.reason, 'OUT_OF_SCOPE');
  });

  it('denies unauthorized capability', () => {
    const engine = createPolicyEngine(makeManifest());
    const result = engine.evaluate({
      target: 'scan.target.com',
      tool: 'sqlmap.inject',
      riskLevel: 'R1',
    });
    assert.equal(result.decision, DECISIONS.DENY);
    assert.equal(result.reason, 'CAPABILITY_NOT_AUTHORIZED');
  });

  it('requires approval for R3 even if R3 is not listed', () => {
    const engine = createPolicyEngine(makeManifest());
    const result = engine.evaluate({
      target: 'scan.target.com',
      tool: 'dns.reverse',
      riskLevel: 'R3',
    });
    assert.equal(result.decision, DECISIONS.APPROVAL_REQUIRED);
  });

  it('denies expired manifest', () => {
    const engine = createPolicyEngine(makeManifest({ expiresAt: PAST }));
    const result = engine.evaluate({
      target: 'scan.target.com',
      tool: 'dns.reverse',
      riskLevel: 'R1',
    });
    assert.equal(result.decision, DECISIONS.DENY);
    assert.equal(result.reason, 'MANIFEST_EXPIRED');
  });

  it('never throws — returns DENY on internal error', () => {
    const engine = createPolicyEngine(makeManifest());
    const result = engine.evaluate(null);
    assert.equal(result.decision, DECISIONS.DENY);
  });
});

// ─── Permit Issuer ────────────────────────────────────────────────

describe('permit issuer', () => {
  it('issues permit after ALLOW decision', () => {
    const issuer = createPermitIssuer({ ttlMs: 5000 });
    const permit = issuer.issue(
      { tool: 'dns.reverse', target: 'test.com', operatorId: 'op1', riskLevel: 'R1' },
      { decision: 'ALLOW' }
    );
    assert.ok(permit.id);
    assert.ok(permit.token);
    assert.ok(!permit.used);
    issuer.shutdown();
  });

  it('refuses to issue permit without ALLOW', () => {
    const issuer = createPermitIssuer();
    assert.throws(() =>
      issuer.issue({}, { decision: 'DENY' }),
      /cannot issue permit/
    );
    issuer.shutdown();
  });

  it('consumes permit exactly once', () => {
    const issuer = createPermitIssuer({ ttlMs: 60000 });
    const request = { tool: 'http.headers', target: 'https://test.com', operatorId: 'op1', riskLevel: 'R1' };
    const permit = issuer.issue(request, { decision: 'ALLOW' });

    const first = issuer.consume(permit.id, 'http.headers', 'https://test.com', 'op1');
    assert.ok(first.ok);

    const second = issuer.consume(permit.id, 'http.headers', 'https://test.com', 'op1');
    assert.ok(!second.ok);
    assert.equal(second.reason, 'PERMIT_ALREADY_USED');
    issuer.shutdown();
  });

  it('enforces tool mismatch', () => {
    const issuer = createPermitIssuer({ ttlMs: 60000 });
    const permit = issuer.issue(
      { tool: 'nmap.scan', target: '10.0.0.1', operatorId: 'op1', riskLevel: 'R2' },
      { decision: 'ALLOW' }
    );
    const result = issuer.consume(permit.id, 'dns.reverse', '10.0.0.1', 'op1');
    assert.ok(!result.ok);
    assert.equal(result.reason, 'TOOL_MISMATCH');
    issuer.shutdown();
  });

  it('expires permits after TTL', async () => {
    const issuer = createPermitIssuer({ ttlMs: 50 });
    const permit = issuer.issue(
      { tool: 'dns.reverse', target: 'slow.com', operatorId: 'op1', riskLevel: 'R1' },
      { decision: 'ALLOW' }
    );

    await new Promise(resolve => setTimeout(resolve, 80));
    const result = issuer.consume(permit.id, 'dns.reverse', 'slow.com', 'op1');
    assert.ok(!result.ok);
    assert.equal(result.reason, 'PERMIT_EXPIRED');
    issuer.shutdown();
  });

  it('sweeps expired permits from memory', async () => {
    const issuer = createPermitIssuer({ ttlMs: 30 });
    issuer.issue({ tool: 'a.b', target: 'x.com', operatorId: 'o', riskLevel: 'R1' }, { decision: 'ALLOW' });
    await new Promise(r => setTimeout(r, 50));
    issuer.sweepExpired();
    assert.equal(issuer.activeCount, 0);
    issuer.shutdown();
  });
});

// ─── Evidence Chain ───────────────────────────────────────────────

describe('evidence chain', () => {
  let chain;

  beforeEach(async () => {
    chain = createEvidenceChain(boundary);
    await chain.init();
  });

  it('starts with zero entries', async () => {
    assert.equal(chain.length, 0);
  });

  it('appends entries with hash chaining', async () => {
    const e1 = await chain.append('action.proposed', { tool: 'dns.reverse', target: 'test.com' });
    const e2 = await chain.append('action.executed', { tool: 'dns.reverse', result: 'ok' });

    assert.equal(e1.seq, 1);
    assert.equal(e2.seq, 2);
    assert.equal(e2.prevHash, e1.hash);
    assert.notEqual(e1.hash, e2.hash);
  });

  it('verifies intact chain', async () => {
    await chain.append('event.a', { data: 'a' });
    await chain.append('event.b', { data: 'b' });
    await chain.append('event.c', { data: 'c' });

    const verification = await chain.verify();
    assert.ok(verification.valid);
    assert.equal(verification.entries, 3);
  });

  it('detects tampering when entry is modified', async () => {
    await chain.append('original', { secret: 'value' });
    await chain.append('second', { data: 'b' });

    // Tamper with the file directly
    const rawPath = path.join(tmpDir, 'evidence', 'chain.jsonl');
    let content = await fs.readFile(rawPath, 'utf8');
    content = content.replace('"secret"', '"TAMPERED"');
    await fs.writeFile(rawPath, content);

    const verification = await chain.verify();
    assert.ok(!verification.valid);
  });

  it('sanitizes sensitive data before storing', async () => {
    const entry = await chain.append('tool.output', {
      headers: { authorization: 'Bearer sk-1234567890abcdef1234567890' },
      body: 'session_id=abc123xyz token=ghp_abcdefghijklmnopqrstuvwx',
    });

    const serialized = JSON.stringify(entry.data);
    assert.ok(!serialized.includes('sk-1234567890abcdef'));
    assert.ok(serialized.includes('[REDACTED]'));
  });

  it('retrieves entries in order', async () => {
    await chain.append('first', {});
    await chain.append('second', {});
    await chain.append('third', {});

    const entries = await chain.getEntries();
    assert.equal(entries[0].seq, 1);
    assert.equal(entries[2].seq, 3);
  });
});

// ─── Secret Vault ─────────────────────────────────────────────────

describe('secret vault', () => {
  let vault;

  beforeEach(async () => {
    await boundary.mkdir('.edge-cyber');
    vault = createSecretVault(boundary, 'test-passphrase-1234');
    await vault.init();
  });

  it('stores and retrieves a secret', async () => {
    await vault.setSecret('hackerone-api-key', 'super-secret-key-value');
    const retrieved = await vault.getSecret('hackerone-api-key');
    assert.equal(retrieved, 'super-secret-key-value');
  });

  it('encrypts at rest (stored value differs from plaintext)', async () => {
    const plaintext = 'my-api-token-value-12345';
    await vault.setSecret('token', plaintext);

    const rawContent = await boundary.readFile('.edge-cyber/secrets.vault');
    assert.ok(!rawContent.includes(plaintext));
  });

  it('fails to decrypt with wrong passphrase', async () => {
    await vault.setSecret('key1', 'secret-value');

    const wrongVault = createSecretVault(boundary, 'wrong-passphrase-here');
    await assert.rejects(() => wrongVault.getSecret('key1'), VaultError);
  });

  it('deletes secrets', async () => {
    await vault.setSecret('temp', 'value');
    await vault.deleteSecret('temp');
    assert.ok(!(await vault.hasSecret('temp')));
  });

  it('lists secret names without values', async () => {
    await vault.setSecret('alpha', 'val-a');
    await vault.setSecret('beta', 'val-b');
    const names = await vault.listSecrets();
    assert.deepEqual(names.sort(), ['alpha', 'beta']);
  });

  it('rejects short passphrases', () => {
    assert.throws(() => createSecretVault(boundary, 'short'), VaultError);
  });

  it('rejects missing secret name', async () => {
    await assert.rejects(() => vault.setSecret('', 'value'), VaultError);
  });

  it('returns NOT_FOUND for unknown secret', async () => {
    await assert.rejects(() => vault.getSecret('nonexistent'), (err) => err.code === 'NOT_FOUND');
  });
});
