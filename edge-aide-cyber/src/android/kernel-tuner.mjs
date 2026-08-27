/**
 * Kernel Tuner — optimizes VM parameters for edge device performance.
 */

import { execSync } from "node:child_process";

export class KernelTuner {
  #applied = [];
  #profile = null;

  tune(profile = "balanced") {
    this.#profile = profile;
    const profiles = {
      memory_saver: {
        "vm/swappiness": "10",
        "vm/dirty_ratio": "5",
        "vm/dirty_background_ratio": "2",
        "vm/dirty_expire_centisecs": "500",
      },
      balanced: {
        "vm/swappiness": "30",
        "vm/dirty_ratio": "10",
        "vm/dirty_background_ratio": "5",
      },
      io_performance: {
        "vm/swappiness": "10",
        "vm/dirty_ratio": "15",
        "vm/dirty_background_ratio": "5",
        "vm/dirty_writeback_centisecs": "500",
      },
    };

    const params = profiles[profile] || profiles.balanced;
    this.#applied = [];

    for (const [path, value] of Object.entries(params)) {
      try {
        execSync(`echo ${value} > /proc/sys/${path}`, { stdio: "ignore", timeout: 3000 });
        this.#applied.push({ path, value, ok: true });
      } catch (err) {
        this.#applied.push({ path, value, ok: false, error: err.message });
      }
    }

    return this.#applied;
  }

  getStatus() {
    const result = {};
    for (const { path, ok } of this.#applied) {
      if (ok) {
        try {
          result[path] = execSync(`cat /proc/sys/${path}`, { timeout: 3000 }).toString().trim();
        } catch { result[path] = "unreadable"; }
      }
    }
    return result;
  }

  get applied() { return this.#applied; }
  get activeProfile() { return this.#profile; }
}