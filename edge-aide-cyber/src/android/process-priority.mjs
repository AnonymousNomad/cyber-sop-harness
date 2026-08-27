/**
 * Process Priority — sets high priority and CPU affinity for the daemon.
 */

import { execSync } from "node:child_process";

export class ProcessPriority {
  setHighPriority() {
    try {
      execSync(`renice -n -10 -p ${process.pid}`, { stdio: "ignore", timeout: 3000 });
      return true;
    } catch { return false; }
  }

  pinToPerformanceCores() {
    try {
      execSync(`taskset -p 0xF0 ${process.pid}`, { stdio: "ignore", timeout: 3000 });
      return true;
    } catch { return false; }
  }

  setIONice() {
    try {
      execSync(`ionice -p ${process.pid} -c 1 -n 0`, { stdio: "ignore", timeout: 3000 });
      return true;
    } catch { return false; }
  }

  optimizeAll() {
    return {
      nice: this.setHighPriority(),
      cpuAffinity: this.pinToPerformanceCores(),
      ioPriority: this.setIONice(),
    };
  }

  getStatus() {
    try {
      return execSync(`ps -o pid=,ni=,psr=,pcpu=,pmem= -p ${process.pid}`, { timeout: 3000 }).toString().trim();
    } catch { return "unknown"; }
  }
}