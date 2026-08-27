/**
 * Device Environment — detects and profiles the Samsung Linux container.
 */

import { execSync } from "node:child_process";

export class DeviceEnvironment {
  #profile = null;

  async detect() {
    this.#profile = {
      kernel: this.#safeExec("uname -r"),
      arch: this.#safeExec("uname -m"),
      cpuCount: parseInt(this.#safeExec("nproc") || "1"),
      totalMemKB: this.#parseMemInfo("MemTotal"),
      availableMemKB: this.#parseMemInfo("MemAvailable"),
      swapTotalKB: this.#parseMemInfo("SwapTotal"),
      swapFreeKB: this.#parseMemInfo("SwapFree"),
      storageFreeGB: this.#getStorageFree(),
      seccompMode: this.#getSeccomp(),
      appArmorCtx: this.#safeExec("cat /proc/self/attr/current"),
      cgroupPath: this.#safeExec("cat /proc/self/cgroup").trim(),
      nodeVersion: this.#safeExec("node --version"),
      pythonVersion: this.#safeExec("python3 --version"),
      gccVersion: this.#safeExec("gcc --version | head -1"),
      hasApt: this.#safeExec("which apt") !== "",
    };
    return this.#profile;
  }

  #safeExec(cmd) {
    try { return execSync(cmd, { timeout: 5000 }).toString().trim(); }
    catch { return ""; }
  }

  #parseMemInfo(key) {
    const data = this.#safeExec("cat /proc/meminfo");
    const match = data.match(new RegExp(`${key}:\\s+(\\d+)`));
    return match ? parseInt(match[1]) : 0;
  }

  #getStorageFree() {
    try {
      const out = execSync("df -BG / | tail -1", { timeout: 5000 }).toString();
      const parts = out.split(/\s+/);
      return parseInt(parts[3]) || 0;
    } catch { return 0; }
  }

  #getSeccomp() {
    try {
      const status = execSync("cat /proc/self/status", { timeout: 5000 }).toString();
      const match = status.match(/Seccomp:\s+(\d+)/);
      return match ? parseInt(match[1]) : -1;
    } catch { return -1; }
  }

  get profile() { return this.#profile; }

  summarize() {
    const p = this.#profile;
    if (!p) return "Not detected";
    return [
      `Kernel: ${p.kernel}`,
      `CPU: ${p.cpuCount} cores (${p.arch})`,
      `RAM: ${Math.round(p.totalMemKB / 1024)}MB total, ${Math.round(p.availableMemKB / 1024)}MB available`,
      `Swap: ${Math.round(p.swapTotalKB / 1024)}MB total, ${Math.round((p.swapTotalKB - p.swapFreeKB) / 1024)}MB used`,
      `Storage: ${p.storageFreeGB}GB free`,
      `Seccomp: mode ${p.seccompMode}`,
      `AppArmor: ${p.appArmorCtx}`,
      `Node: ${p.nodeVersion} | Python: ${p.pythonVersion} | GCC: ${p.gccVersion}`,
    ].join("\n");
  }
}