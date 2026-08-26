# Edge Data Exfiltration Prevention

## What To Do
Ensure no sensitive data (credentials, findings, evidence) leaves the device without operator consent. All network outbound traffic is inspected and sensitive data is blocked or flagged.

## Why
Malicious tool output or compromised adapters could attempt to exfiltrate engagement data. Preventing unauthorized outbound data transfer protects the operator and the engagement.

## Code Guidance
```javascript
// src/opsec/exfil-guard.mjs
export class ExfilGuard {
  #blockedPatterns = [
    /password\s*[=:]\s*\S+/i,
    /api[_-]?key\s*[=:]\s*\S+/i,
    /token\s*[=:]\s*\S+/i,
    /secret\s*[=:]\s*\S+/i,
  ];

  inspect(data) {
    const str = typeof data === 'string' ? data : JSON.stringify(data);
    for (const pattern of this.#blockedPatterns) {
      if (pattern.test(str)) {
        return { blocked: true, reason: 'SENSITIVE_DATA_DETECTED', pattern: pattern.source };
      }
    }
    return { blocked: false };
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Tool output contains leaked credentials | Data exposure | Regex pattern matching |
| Evidence sent to external API | Engagement leak | All outbound requires consent |
| Model inference sends context to remote | Data leak | Local model preferred, consent gate |

## Dependencies
- OpSecManager, SecretVault

## Pitfalls
- Regex patterns may have false positives on normal output
- Base64-encoded secrets bypass simple patterns — decode first
- Some tool adapters need to send data to function (e.g., nuclei API) — whitelist per-tool