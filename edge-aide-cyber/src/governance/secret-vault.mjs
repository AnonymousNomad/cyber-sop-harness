import { createCipheriv, createDecipheriv, scryptSync, randomBytes } from 'node:crypto';

const ALGO = 'aes-256-gcm';
const IV_LENGTH = 12;
const AUTH_TAG_LENGTH = 16;

export class VaultError extends Error {
  constructor(code, message) {
    super(message);
    this.name = 'VaultError';
    this.code = code;
  }
}

export function createSecretVault(fileBoundary, passphrase) {
  if (!passphrase || typeof passphrase !== 'string' || passphrase.length < 8) {
    throw new VaultError('WEAK_PASSPHRASE', 'passphrase must be at least 8 characters');
  }

  const vaultFile = '.edge-cyber/secrets.vault';
  const key = scryptSync(passphrase, 'edge-aide-cyber-vault-salt-v1', 32);

  async function readVault() {
    try {
      const raw = await fileBoundary.readFile(vaultFile);
      return JSON.parse(raw);
    } catch {
      return {};
    }
  }

  async function writeVault(secrets) {
    await fileBoundary.writeFile(vaultFile, JSON.stringify(secrets, null, 2));
  }

  async function setSecret(name, plaintext) {
    if (!name || typeof name !== 'string') throw new VaultError('INVALID_NAME', 'secret name required');
    if (typeof plaintext !== 'string') throw new VaultError('INVALID_VALUE', 'secret value must be a string');

    const iv = randomBytes(IV_LENGTH);
    const cipher = createCipheriv(ALGO, key, iv);
    const encrypted = Buffer.concat([cipher.update(plaintext, 'utf8'), cipher.final()]);
    const authTag = cipher.getAuthTag();

    const payload = Buffer.concat([iv, authTag, encrypted]).toString('base64');
    const secrets = await readVault();
    secrets[name] = payload;
    await writeVault(secrets);
  }

  async function getSecret(name) {
    const secrets = await readVault();
    const encoded = secrets[name];
    if (!encoded) throw new VaultError('NOT_FOUND', `secret "${name}" not found`);

    try {
      const payload = Buffer.from(encoded, 'base64');
      const iv = payload.subarray(0, IV_LENGTH);
      const authTag = payload.subarray(IV_LENGTH, IV_LENGTH + AUTH_TAG_LENGTH);
      const encrypted = payload.subarray(IV_LENGTH + AUTH_TAG_LENGTH);

      const decipher = createDecipheriv(ALGO, key, iv);
      decipher.setAuthTag(authTag);
      return decipher.update(encrypted).toString('utf8') + decipher.final('utf8');
    } catch {
      throw new VaultError('DECRYPT_FAILED', `cannot decrypt secret "${name}" — wrong passphrase or corrupted vault`);
    }
  }

  async function deleteSecret(name) {
    const secrets = await readVault();
    if (!(name in secrets)) throw new VaultError('NOT_FOUND', `secret "${name}" not found`);
    delete secrets[name];
    await writeVault(secrets);
  }

  async function listSecrets() {
    const secrets = await readVault();
    return Object.keys(secrets);
  }

  async function hasSecret(name) {
    const secrets = await readVault();
    return name in secrets;
  }

  async function init() {
    await fileBoundary.mkdir('.edge-cyber');
  }

  return Object.freeze({
    setSecret,
    getSecret,
    deleteSecret,
    listSecrets,
    hasSecret,
    init,
  });
}
