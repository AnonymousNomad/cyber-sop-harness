# Edge Process Priority Management

## What To Do
Set terminal daemon to high priority (nice -10) and pin to performance cores (CPU 4-7) to ensure it gets resources before Android kills background processes.

## Why
Android's scheduler deprioritizes background apps. By setting our process to high priority and pinning to performance cores, we ensure the terminal gets CPU time even under load. Combined with foreground cgroup placement, this reduces the chance of being killed.

## Code Guidance

```javascript
// src/android/process-priority.mjs
import { execSync } from 'node:child_process';

export class ProcessPriority {
  // Set process to high priority
  setHighPriority() {
    try {
      execSync(`renice -n -10 -p ${process.pid}`, { stdio: 'ignore' });
      return true;
    } catch { return false; }
  }

  // Pin to performance cores (4-7)
  pinToPerformanceCores() {
    try {
      // Cortex-A720 cores are 4-7
      execSync(`taskset -p 0xF0 ${process.pid}`, { stdio: 'ignore' }); // 0xF0 = CPUs 4-7
      return true;
    } catch { return false; }
  }

  // Set I/O priority to high
  setIONice() {
    try {
      execSync(`ionice -p ${process.pid} -c 1 -n 0`, { stdio: 'ignore' }); // Realtime class
      return true;
    } catch { return false; }
  }

  // Set CPU governor to performance
  setCPUGovernor() {
    try {
      execSync('echo performance > /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor 2>/dev/null');
      return true;
    } catch { return false; }
  }

  // Move to foreground cgroup
  moveToForeground() {
    try {
      execSync('echo $$ > /dev/cpuset/tasks 2>/dev/null');
      return true;
    } catch { return false; }
  }

  // Get current process status
  getStatus() {
    try {
      const status = execSync(`ps -o pid,ni,psr,pcpu,pmem,comm -p ${process.pid}`).toString();
      return status;
    } catch { return 'unknown'; }
  }

  // Apply all optimizations
  optimizeAll() {
    return {
      nice: this.setHighPriority(),
      cpuAffinity: this.pinToPerformanceCores(),
      ioPriority: this.setIONice(),
      governor: this.setCPUGovernor(),
      cgroup: this.moveToForeground(),
    };
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| `renice` denied by seccomp | No priority boost | Try/catch, report failure |
| `taskset` denied | No CPU pinning | Fallback to cgroup cpuset |
| CPU governor write denied | Default powersave governor | Accept, focus on priority instead |

## Dependencies
- `renice`, `taskset`, `ionice` (available in Ubuntu container)
- `/sys/devices/system/cpu/` access (may be restricted)

## Pitfalls
- `nice -10` may be denied if not root — we are root so should work
- CPU governor is shared across all Android apps — may be overridden
- Performance cores generate more heat — monitor thermal throttling
- `ionice` realtime class may be denied by seccomp