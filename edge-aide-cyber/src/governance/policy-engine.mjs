import { createScopeEvaluator } from './scope-evaluator.mjs';

const DECISIONS = Object.freeze({
  ALLOW: 'ALLOW',
  DENY: 'DENY',
  APPROVAL_REQUIRED: 'APPROVAL_REQUIRED',
});

export function validateManifest(manifest) {
  if (!manifest || typeof manifest !== 'object') {
    throw new Error('manifest must be a non-null object');
  }
  const required = ['id', 'operatorId', 'expiresAt', 'scope', 'allowedCapabilities', 'authorizedRiskLevels'];
  for (const field of required) {
    if (!(field in manifest)) throw new Error(`manifest missing required field: ${field}`);
  }
  if (!Array.isArray(manifest.scope)) {
    throw new Error('manifest scope must be an array');
  }
  if (!Array.isArray(manifest.allowedCapabilities)) {
    throw new Error('manifest allowedCapabilities must be an array');
  }
  if (!Array.isArray(manifest.authorizedRiskLevels)) {
    throw new Error('manifest authorizedRiskLevels must be an array');
  }
  return true;
}

export function isManifestExpired(manifest, now = Date.now()) {
  return new Date(manifest.expiresAt).getTime() <= now;
}

export function createPolicyEngine(manifest) {
  try {
    validateManifest(manifest);
  } catch (err) {
    throw err;
  }

  const scopeEvaluator = createScopeEvaluator(manifest.scope);

  function evaluate(actionRequest) {
    try {
      return evaluateStrict(actionRequest);
    } catch (err) {
      return { decision: DECISIONS.DENY, reason: 'POLICY_ERROR', detail: err.message };
    }
  }

  function evaluateStrict(req) {
    if (!req || typeof req !== 'object') {
      return { decision: DECISIONS.DENY, reason: 'INVALID_REQUEST' };
    }

    const required = ['target', 'tool', 'riskLevel'];
    for (const field of required) {
      if (!req[field]) return { decision: DECISIONS.DENY, reason: `MISSING_FIELD:${field}` };
    }

    if (isManifestExpired(manifest)) {
      return { decision: DECISIONS.DENY, reason: 'MANIFEST_EXPIRED', expiresAt: manifest.expiresAt };
    }

    const targetStr = String(req.target).trim();
    if (!targetStr) return { decision: DECISIONS.DENY, reason: 'EMPTY_TARGET' };

    const scopeResult = scopeEvaluator(targetStr);
    if (!scopeResult.allowed) {
      return { decision: DECISIONS.DENY, reason: 'OUT_OF_SCOPE', detail: scopeResult.reason };
    }

    if (!manifest.authorizedRiskLevels.includes(req.riskLevel)) {
      if (req.riskLevel === 'R3') {
        return { decision: DECISIONS.APPROVAL_REQUIRED, reason: 'RISK_R3_REQUIRES_APPROVAL' };
      }
      return { decision: DECISIONS.DENY, reason: 'RISK_LEVEL_UNAUTHORIZED', riskLevel: req.riskLevel };
    }

    if (!manifest.allowedCapabilities.includes(req.tool)) {
      return { decision: DECISIONS.DENY, reason: 'CAPABILITY_NOT_AUTHORIZED', tool: req.tool };
    }

    return {
      decision: DECISIONS.ALLOW,
      permitRequired: true,
      matchedScopeRule: scopeResult.matchedRule,
    };
  }

  return Object.freeze({
    evaluate,
    get scopeRules() { return scopeEvaluator.getRules(); },
    get manifestId() { return manifest.id; },
    get expiresAt() { return manifest.expiresAt; },
    get isExpired() { return isManifestExpired(manifest); },
  });
}

export { DECISIONS };
