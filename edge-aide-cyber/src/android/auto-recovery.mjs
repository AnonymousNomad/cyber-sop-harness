/**
 * Auto-Recovery — restores state from checkpoints after crash.
 */

import { readdir, readFile } from "node:fs/promises";
import { join } from "node:path";

export class AutoRecovery {
  #checkpointDir;
  #recoveryCount = 0;
  #maxAgeMs;

  constructor(checkpointDir, { maxAgeMs = 300000 } = {}) {
    this.#checkpointDir = checkpointDir;
    this.#maxAgeMs = maxAgeMs;
  }

  async attemptRecovery() {
    const checkpoint = await this.#loadLatest();
    if (!checkpoint) return null;

    const age = Date.now() - checkpoint.timestamp;
    if (age > this.#maxAgeMs) return null;

    this.#recoveryCount++;
    return { state: checkpoint.state, age, recoveryCount: this.#recoveryCount };
  }

  async #loadLatest() {
    try {
      const files = await readdir(this.#checkpointDir);
      const cps = files.filter(f => f.startsWith("cp-") && f.endsWith(".json")).sort().reverse();
      for (const file of cps) {
        try {
          const data = await readFile(join(this.#checkpointDir, file), "utf8");
          return JSON.parse(data);
        } catch { continue; }
      }
    } catch {}
    return null;
  }

  get recoveryCount() { return this.#recoveryCount; }
}