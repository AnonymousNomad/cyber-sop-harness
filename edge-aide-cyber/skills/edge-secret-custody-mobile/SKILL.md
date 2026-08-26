# Edge Secret Custody (Mobile)

## What To Do
Implement AES-256-GCM encryption for API keys and secrets using a passphrase-derived key. No plaintext secrets on disk. DPAPI is unavailable outside Windows; passphrase-based encryption is universal.

## Why
Bug bounty operators need to store API tokens (HackerOne, Bugcrowd, GitHub). On a shared tablet, these must be encrypted at rest. AES-GCM provides authenticated encryption so tampered ciphertext fails decryption.

## Code Guidance
```javascript
import { createCipheriv, createDecipheriv, scryptSync, randomBytes, createHash } from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';

const ALGO = 'aes-256-gcm';
const IV_LENGTH = 12; // GCM standard nonce size

export function createSecretVault(vaultDir, passphrase) {
  const vaultFile = path.join(vaultDir, '.edge-vault');
  const key = scryptSync(passphrase, 'edge-aide-cyber-salt', 32);

  async function setSecret(name, value) {
    const iv = randomBytes(IV_LENGTH);
    const cipher = createCipheriv(ALGO, key, iv);
    const encrypted = Buffer.concat([cipher.update(value, 'utf8'), cipher.final()]);
    const authTag = cipher.getAuthTag();

    const payload = Buffer.concat([iv, authTag, encrypted]);
    const secrets = await readVault();
    secrets[name] = payload.toString('base64');
    await fs.writeFile(vaultFile, JSON.stringify(secrets));
  }

  async function getSecret(name) {
    const secrets = await readVault();
    const encoded = secrets[name];
    if (!encoded) throw new Error(`secret not found: ${name}`);

    const payload = Buffer.from(encoded, 'base64');
    const iv = payload.subarray(0, IV_LENGTH);
    const authTag = payload.subarray(IV_LENGTH, IV_LENGTH + 16);
    const encrypted = payload.subarray(IV_LENGTH + 16);

    const decipher = createDecipheriv(ALGO, key, iv);
    decipher.setAuthTag(authTag);
    return decipher.update(encrypted) + decipher.final('utf8');
  }

  async function deleteSecret(name) {
    const secrets = await readVault();
    delete secrets[name];
    await fs.writeFile(vaultFile, JSON.stringify(secrets));
  }

  async function listSecrets() {
    const secrets = await readVault();
    return Object.keys(secrets);
  }

  async function readVault() {
    try {
      return JSON.parse(await fs.readFile(vaultFile, 'utf8'));
    } catch { return {}; }
  }

  return { setSecret, getSecret, deleteSecret, listSecrets };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Weak passphrase brute-forced | All secrets exposed | Enforce minimum 12-char passphrase; document best practices |
| Nonce reuse in AES-GCM | Complete encryption breakdown | Always generate fresh random IV per encryption operation |
| Vault file read by other apps | Secret exposure | File permissions set to 600; document Termux limitations |
| Key derived with weak salt | Rainbow table attack | Static salt is acceptable for this use case; per-device salt would be better |
| Secrets logged accidentally | Plaintext in logs | Never log secret values; wrap in class that redacts toString() |

## Dependencies
- Node.js built-in `crypto`, `fs/promises`, `path`

## Pitfalls & Bugs
- `scryptSync` is CPU-intensive; on low-power devices it may take several seconds. Consider caching the derived key.
- AES-GCM's authentication tag is 16 bytes; getting the subarray offsets wrong causes silent corruption.
- If the passphrase changes, previously encrypted secrets become undecryptable. Implement re-encryption migration.
- Android may backup app data to Google Drive, potentially exposing the vault file. Document how to exclude it.
- `Buffer.toString('base64')` adds padding characters that are safe in JSON but should be handled consistently.
