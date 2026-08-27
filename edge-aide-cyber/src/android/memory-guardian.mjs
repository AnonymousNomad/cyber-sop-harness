/**
 * Memory Guardian — monitors RAM and takes action before OOM kill.
 */

import { execSync } from "node:child_process";

export class MemoryGuardian {
  #thresholdMB;
  #criticalMB;
  #checkpointMB;
  #checkIntervalMs;
  #timer = null;
  #onPressure = null;
  #lastStatus = null;

  constructor({ thresholdMB = 100, criticalMB = 50, checkpointMB = 30, checkIntervalMs = 5000 } = {}) {
    this.#thresholdMB = thresholdMB;
    this.#criticalMB = criticalMB;
    this.#checkpointMB = checkpointMB;
    this.#checkIntervalMs = checkIntervalMs;
  }

  start(onPressure) {
    this.#onPressure = onPressure;
    this.#check();
    this.#timer = setInterval(() => this.#check(), this.#checkIntervalMs);
  }

  stop() { if (this.#timer) clearInterval(this.#timer); }

  getStatus() { return this.#lastStatus; }

  #check() {
    const mem = this.getMemoryStatus();
    this.#lastStatus = mem;

    if (mem.freeMB < this.#checkpointMB) {
      this.#onPressure?.({ level: "emergency", ...mem });
      this.#emergencyAction();
    } else if (mem.freeMB < this.#criticalMB) {
      this.#onPressure?.({ level: "critical", ...mem });
      this.#criticalAction();
    } else if (mem.freeMB < this.#thresholdMB) {
      this.#onPressure?.({ level: "warning", ...mem });
    }
  }

  getMemoryStatus() {
    const data = this.#safeExec("cat /proc/meminfo");
    const total = this.#parse(data, "MemTotal");
    const free = this.#parse(data, "MemFree");
    const available = this.#parse(data, "MemAvailable");
    const swapTotal = this.#parse(data, "SwapTotal");
    const swapFree = this.#parse(data, "SwapFree");

    return {
      totalMB: Math.round(total / 1024),
      freeMB: Math.round(free / 1024),
      availableMB: Math.round(available / 1024),
      usedMB: Math.round((total - free) / 1024),
      swapUsedMB: Math.round((swapTotal - swapFree) / 1024),
      swapTotalMB: Math.round(swapTotal / 1024),
      pressure: available < this.#thresholdMB ? "high" : "normal",
    };
  }

  #safeExec(cmd) {
    try { return execSync(cmd, { timeout: 3000 }).toString(); }
    catch { return ""; }
  }

  #parse(data, key) {
    const match = data.match(new RegExp(`${key}:\\s+(\\d+)`));
    return match ? parseInt(match[1]) : 0;
  }

  #emergencyAction() {
    try { execSync("sync && echo 3 > /proc/sys/vm/drop_caches 2>/dev/null"); } catch {}
    if (global.gc) global.gc();
  }

  #criticalAction() {
    try { execSync("sync && echo 1 > /proc/sys/vm/drop_caches 2>/dev/null"); } catch {}
    if (global.gc) global.gc();
  }
}