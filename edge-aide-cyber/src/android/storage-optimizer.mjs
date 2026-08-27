/**
 * Storage Optimizer — monitors and cleans disk usage.
 */

import { readdir, stat, unlink } from "node:fs/promises";
import { join } from "node:path";
import { execSync } from "node:child_process";

export class StorageOptimizer {
  #workspaceDir;
  #maxLogAgeHours;
  #maxEvidenceEntries;

  constructor(workspaceDir, { maxLogAgeHours = 48, maxEvidenceEntries = 1000 } = {}) {
    this.#workspaceDir = workspaceDir;
    this.#maxLogAgeHours = maxLogAgeHours;
    this.#maxEvidenceEntries = maxEvidenceEntries;
  }

  async getDiskUsage() {
    try {
      const out = execSync("df -BM / | tail -1", { timeout: 5000 }).toString();
      const parts = out.split(/\s+/);
      return {
        totalMB: parseInt(parts[1]) || 0,
        usedMB: parseInt(parts[2]) || 0,
        freeMB: parseInt(parts[3]) || 0,
        percentUsed: parseInt(parts[4]) || 0,
      };
    } catch { return { totalMB: 0, usedMB: 0, freeMB: 0, percentUsed: 0 }; }
  }

  async cleanup() {
    const actions = [];
    actions.push(...await this.#cleanOldLogs());
    actions.push(...await this.#rotateEvidence());
    return actions;
  }

  async #cleanOldLogs() {
    const actions = [];
    const logDir = join(this.#workspaceDir, "logs");
    try {
      const files = await readdir(logDir);
      for (const file of files) {
        const fp = join(logDir, file);
        try {
          const s = await stat(fp);
          const ageHours = (Date.now() - s.mtimeMs) / (1000 * 60 * 60);
          if (ageHours > this.#maxLogAgeHours) {
            await unlink(fp);
            actions.push(`cleaned: ${file}`);
          }
        } catch {}
      }
    } catch {}
    return actions;
  }

  async #rotateEvidence() {
    const evidenceDir = join(this.#workspaceDir, "evidence");
    try {
      const files = await readdir(evidenceDir);
      if (files.length > this.#maxEvidenceEntries) {
        const toDelete = files.sort().slice(0, files.length - this.#maxEvidenceEntries);
        for (const f of toDelete) await unlink(join(evidenceDir, f));
        return [`rotated ${toDelete.length} old evidence entries`];
      }
    } catch {}
    return [];
  }
}