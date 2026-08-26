/**
 * Exfil Guard — blocks sensitive data from leaving the device.
 */

const SENSITIVE_PATTERNS = [
  /password\s*[=:]\s*\S+/gi,
  /api[_-]?key\s*[=:]\s*\S+/gi,
  /token\s*[=:]\s*\S+/gi,
  /secret\s*[=:]\s*\S+/gi,
  /authorization\s*[=:]\s*\S+/gi,
  /cookie\s*[=:]\s*\S+/gi,
  /"password"\s*:\s*"[^"]+"/gi,
  /"api[_-]?key"\s*:\s*"[^"]+"/gi,
  /"token"\s*:\s*"[^"]+"/gi,
  /"secret"\s*:\s*"[^"]+"/gi,
];

export class ExfilGuard {
  #whitelistedTools = new Set();

  constructor({ whitelistedTools = [] } = {}) {
    this.#whitelistedTools = new Set(whitelistedTools);
  }

  inspect(data, toolName) {
    if (this.#whitelistedTools.has(toolName)) return { blocked: false };
    const str = typeof data === "string" ? data : JSON.stringify(data);
    for (const pattern of SENSITIVE_PATTERNS) {
      pattern.lastIndex = 0;
      if (pattern.test(str)) {
        return { blocked: true, reason: "SENSITIVE_DATA_DETECTED", pattern: pattern.source };
      }
    }
    return { blocked: false };
  }

  whitelist(toolName) { this.#whitelistedTools.add(toolName); }
  blacklist(toolName) { this.#whitelistedTools.delete(toolName); }
}
