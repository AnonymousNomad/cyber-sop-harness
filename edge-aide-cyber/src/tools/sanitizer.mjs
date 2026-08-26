const PATTERNS = [
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

export function sanitizeText(text) {
  let result = String(text);
  for (const { regex, replacement } of PATTERNS) {
    result = result.replace(regex, replacement);
  }
  return result;
}

export function sanitizeObject(obj) {
  if (typeof obj === 'string') return sanitizeText(obj);
  if (Array.isArray(obj)) return obj.map(sanitizeObject);
  if (obj && typeof obj === 'object') {
    const clean = {};
    for (const [key, value] of Object.entries(obj)) {
      clean[key] = sanitizeObject(value);
    }
    return clean;
  }
  return obj;
}
