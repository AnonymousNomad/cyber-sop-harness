import { createHash } from 'node:crypto';

const SENSITIVE_PATTERNS = [
  { regex: /Bearer\s+[A-Za-z0-9\-._~+/]+=*/gi, replacement: 'Bearer [REDACTED]' },
  { regex: /(?:api[_-]?key|apikey)\s*[:=]\s*['"]?[A-Za-z0-9\-_]{20,}['"]?/gi, replacement: '[API_KEY_REDACTED]' },
  { regex: /AKIA[0-9A-Z]{16}/g, replacement: '[AWS_KEY_REDACTED]' },
  { regex: /(?:aws)?_?secret[_-]?access[_-]?key\s*[:=]\s*\S+/gi, replacement: '[AWS_SECRET_REDACTED]' },
  { regex: /(?:session|sess)[_-]?id\s*[:=]\s*[A-Za-z0-9\-_]+/gi, replacement: '[SESSION_ID_REDACTED]' },
  { regex: /eyJ[A-Za-z0-9\-_]+\.eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]*/g, replacement: '[JWT_REDACTED]' },
  { regex: /-----BEGIN (?:RSA )?PRIVATE KEY-----[\s\S]*?-----END (?:RSA )?PRIVATE KEY-----/g, replacement: '[PRIVATE_KEY_REDACTED]' },
  { regex: /:\/\/([^:@\/]+):([^@\/]+)@/g, replacement: '://[USER]:[PASS]@' },
  { regex: /gh[pousr]_[A-Za-z0-9]{36,}/g, replacement: '[GH_TOKEN_REDACTED]' },
];

function sanitizeValue(value) {
  if (typeof value === 'string') {
    let sanitized = value;
    for (const { regex, replacement } of SENSITIVE_PATTERNS) {
      sanitized = sanitized.replace(regex, replacement);
    }
    return sanitized;
  }
  if (Array.isArray(value)) return value.map(sanitizeValue);
  if (value && typeof value === 'object') {
    const clean = {};
    for (const [key, val] of Object.entries(value)) {
      clean[key] = sanitizeValue(val);
    }
    return clean;
  }
  return value;
}

export function createEvidenceChain(fileBoundary) {
  const evidencePath = 'evidence/chain.jsonl';
  let lastHash = null;
  let lastSeq = 0;
  let initialized = false;

  async function init() {
    await fileBoundary.mkdir('evidence');
    try {
      const raw = await fileBoundary.readFile(evidencePath);
      const lines = raw.split('\n').filter(Boolean);
      if (lines.length > 0) {
        const lastEntry = JSON.parse(lines[lines.length - 1]);
        lastHash = lastEntry.hash;
        lastSeq = lastEntry.seq || 0;
      }
    } catch {}
    initialized = true;
  }

  function hashEntry(entry) {
    const canonical = JSON.stringify({
      seq: entry.seq,
      at: entry.at,
      type: entry.type,
      data: entry.data,
      prevHash: entry.prevHash,
    });
    return createHash('sha256').update(canonical).digest('hex');
  }

  async function append(type, data) {
    if (!initialized) await init();

    const entry = {
      seq: lastSeq + 1,
      at: new Date().toISOString(),
      type,
      data: sanitizeValue(data),
    };

    entry.prevHash = lastHash;
    entry.hash = hashEntry(entry);

    const line = JSON.stringify(entry) + '\n';
    await fileBoundary.appendFile(evidencePath, line);

    lastHash = entry.hash;
    lastSeq = entry.seq;

    return Object.freeze({ ...entry });
  }

  async function verify() {
    let raw;
    try {
      raw = await fileBoundary.readFile(evidencePath);
    } catch {
      return { valid: true, entries: 0, message: 'empty chain' };
    }

    const lines = raw.split('\n').filter(Boolean);
    let expectedPrevHash = null;

    for (let i = 0; i < lines.length; i++) {
      let entry;
      try {
        entry = JSON.parse(lines[i]);
      } catch {
        return { valid: false, breakAt: i + 1, reason: 'JSON parse error' };
      }

      const recomputedHash = hashEntry(entry);
      if (entry.prevHash !== expectedPrevHash) {
        return { valid: false, breakAt: entry.seq, reason: `prev hash mismatch at seq ${entry.seq}` };
      }
      if (entry.hash !== recomputedHash) {
        return { valid: false, breakAt: entry.seq, reason: `hash mismatch at seq ${entry.seq}` };
      }

      expectedPrevHash = entry.hash;
    }

    return { valid: true, entries: lines.length };
  }

  async function getEntries(limit = 50) {
    try {
      const raw = await fileBoundary.readFile(evidencePath);
      const lines = raw.split('\n').filter(Boolean);
      return lines.slice(-limit).map(line => JSON.parse(line));
    } catch {
      return [];
    }
  }

  return {
    init,
    append,
    verify,
    getEntries,
    get lastHash() { return lastHash; },
    get length() { return lastSeq; },
  };
}
