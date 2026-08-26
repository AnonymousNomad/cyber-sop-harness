# Edge Session Security & Credential Management

## What To Do
Implement encrypted session state, credential vault rotation, session timeout, and secure wipe on exit.

## Why
Bug bounty sessions contain sensitive data. If the device is lost/seized, data must be protected. Session security ensures encrypted data at rest and memory wipe on exit.

## Code Guidance
```javascript
// src/opsec/session-manager.mjs
export class SessionManager {
  async saveState(state) { /* encrypt + write */ }
  async loadState() { /* read + decrypt */ }
  isExpired() { return (Date.now() - this.#lastActivity) > this.#timeoutMs; }
  async secureWipe() { /* overwrite with random data + delete */ }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Session file unencrypted | Data theft if seized | AES-256-GCM encryption |
| Memory not wiped | Forensic recovery | secureWipe overwrites + deletes |
| Session timeout too long | Unattended exposure | Default 1hr, configurable |

## Dependencies
- Secret vault module, File boundary module, node:crypto

## Pitfalls
- RAM contents persist after file wipe — use process exit handler
- Android kills processes without cleanup — register SIGTERM
- Never store passphrase in visible env var