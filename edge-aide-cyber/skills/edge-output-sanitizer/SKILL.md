# Edge Output Sanitizer

## What To Do
Strip credentials, tokens, session identifiers, API keys, and other sensitive patterns from all tool adapter outputs before they enter the evidence chain or reach the model context.

## Why
Security tool outputs frequently contain authentication tokens, session cookies, API keys, and internal URLs with embedded credentials. Storing these in evidence creates a secondary breach risk.

## Code Guidance
```javascript
const PATTERNS = [
  // Bearer tokens
  { regex: /Bearer\s+[A-Za-z0-9\-._~+/]+=*/gi, replacement: 'Bearer [REDACTED]' },
  // Generic API keys (common formats)
  { regex: /(?:api[_-]?key|apikey)\s*[:=]\s*['"]?[A-Za-z0-9\-_]{20,}['"]?/gi, replacement: '[API_KEY_REDACTED]' },
  // AWS credentials
  { regex: /AKIA[0-9A-Z]{16}/g, replacement: '[AWS_KEY_REDACTED]' },
  { regex: /(?:aws)?_?secret[_-]?access[_-]?key\s*[:=]\s*\S+/gi, replacement: '[AWS_SECRET_REDACTED]' },
  // Session cookies
  { regex: /(?:session|sess)[_-]?id\s*[:=]\s*[A-Za-z0-9\-_]+/gi, replacement: '[SESSION_ID_REDACTED]' },
  // JWT tokens
  { regex: /eyJ[A-Za-z0-9\-_]+\.eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]*/g, replacement: '[JWT_REDACTED]' },
  // Private keys
  { regex: /-----BEGIN (?:RSA )?PRIVATE KEY-----[\s\S]*?-----END (?:RSA )?PRIVATE KEY-----/g, replacement: '[PRIVATE_KEY_REDACTED]' },
  // Passwords in URLs
  { regex: /:\/\/([^:@\/]+):([^@\/]+)@/g, replacement: '://[USER]:[PASS]@' },
  // GitHub tokens
  { regex: /gh[pousr]_[A-Za-z0-9]{36,}/g, replacement: '[GH_TOKEN_REDACTED]' },
];

export function sanitizeText(text) {
  let sanitized = text;
  for (const { regex, replacement } of PATTERNS) {
    sanitized = sanitized.replace(regex, replacement);
  }
  return sanitized;
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
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Unknown credential format not caught | Secret leaks into evidence | Maintain pattern library; add custom patterns per tool |
| Base64-encoded secrets pass through | Encoded secret exposed | Decode base64 segments and scan before re-encoding |
| Sanitizer itself logs unsanitized input | Bypass through side channel | Never log raw inputs; process in memory only |
| Over-redaction destroys useful evidence | Finding cannot be verified | Log sanitization events so analyst knows what was redacted |
| Regex catastrophic backtracking | ReDoS on large outputs | Use non-greedy quantifiers; limit input size before sanitizing |

## Dependencies
- None (pure functions)

## Pitfalls & Bugs
- Regex patterns should be compiled once at module level, not inside the function, for performance.
- `String.replace` with a global regex modifies the original string; ensure you work on a copy.
- JSON.stringify of objects with circular references will throw; handle this case.
- The sanitizer cannot catch everything; document that evidence consumers should assume partial redaction.
- Custom credential formats (e.g., specific bug bounty platform tokens) need additional patterns added.
