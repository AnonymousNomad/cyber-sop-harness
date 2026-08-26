import { createHash, randomBytes } from 'node:crypto';

const DEFAULT_TTL_MS = 30000;
const SWEEP_INTERVAL_MS = 60000;

export function createPermitIssuer(options = {}) {
  const ttlMs = options.ttlMs || DEFAULT_TTL_MS;
  const activePermits = new Map();
  let sweepTimer = null;

  function issue(request, policyDecision) {
    if (!policyDecision || policyDecision.decision !== 'ALLOW') {
      throw new Error(`cannot issue permit: policy decision is ${policyDecision?.decision || 'undefined'}`);
    }

    const id = randomBytes(16).toString('hex');
    const now = Date.now();

    const permit = {
      id,
      tool: request.tool,
      target: request.target,
      operatorId: request.operatorId,
      riskLevel: request.riskLevel,
      issuedAt: now,
      expiresAt: now + ttlMs,
      used: false,
    };

    const tokenPayload = `${id}:${permit.tool}:${permit.target}:${permit.operatorId}:${permit.expiresAt}`;
    const token = createHash('sha256').update(tokenPayload).digest('hex');

    activePermits.set(id, { ...permit, token });
    return { ...permit, token };
  }

  function consume(permitId, toolName, target, operatorId) {
    if (!permitId) return { ok: false, reason: 'MISSING_PERMIT_ID' };

    const permit = activePermits.get(permitId);
    if (!permit) return { ok: false, reason: 'PERMIT_NOT_FOUND' };

    const now = Date.now();
    if (now > permit.expiresAt) {
      activePermits.delete(permitId);
      return { ok: false, reason: 'PERMIT_EXPIRED' };
    }
    if (permit.used) {
      return { ok: false, reason: 'PERMIT_ALREADY_USED' };
    }
    if (permit.tool !== toolName) return { ok: false, reason: 'TOOL_MISMATCH', expected: permit.tool, got: toolName };
    if (permit.target !== target) return { ok: false, reason: 'TARGET_MISMATCH', expected: permit.target, got: target };
    if (permit.operatorId !== operatorId) return { ok: false, reason: 'OPERATOR_MISMATCH' };

    permit.used = true;

    return { ok: true, permit: Object.freeze({ ...permit }) };
  }

  function sweepExpired() {
    const now = Date.now();
    for (const [id, permit] of activePermits) {
      if (now > permit.expiresAt) activePermits.delete(id);
    }
  }

  function startSweep() {
    stopSweep();
    sweepTimer = setInterval(sweepExpired, SWEEP_INTERVAL_MS);
    sweepTimer.unref();
  }

  function stopSweep() {
    if (sweepTimer) {
      clearInterval(sweepTimer);
      sweepTimer = null;
    }
  }

  startSweep();

  return {
    issue,
    consume,
    sweepExpired,
    get activeCount() { return activePermits.size; },
    getActiveIds() { return [...activePermits.keys()]; },
    shutdown() { stopSweep(); },
  };
}
