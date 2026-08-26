# Edge Device Profile Lock

## What To Do
Measure and freeze the target device's hardware capabilities before any code runs. Record CPU topology, available RAM, storage, thermal behavior, and Android process constraints. This becomes the immutable reference for all downstream sizing decisions.

## Why
Edge devices vary wildly. Without a locked profile, every subsequent decision (model size, context length, thread count, batch size) is a guess. A measured profile turns guessing into engineering.

## Code Guidance
```javascript
import os from 'node:os';
import { execFileSync } from 'node:child_process';

export function captureDeviceProfile() {
  const cpus = os.cpus();
  return {
    arch: os.arch(),
    platform: os.platform(),
    totalMemBytes: os.totalmem(),
    freeMemBytes: os.freemem(),
    cpuCount: cpus.length,
    cpuModel: cpus[0]?.model || 'unknown',
    cpuSpeedMhz: cpus[0]?.speed || 0,
    hostname: os.hostname(),
    uptimeSeconds: os.uptime(),
    nodeVersion: process.version,
    pid: process.pid,
    allowedCpus: readAllowedCpus(),
  };
}

function readAllowedCpus() {
  try {
    const mask = require('fs').readFileSync('/sys/fs/cgroup/cpuset/cpuset.cpus', 'utf8').trim();
    return mask;
  } catch { return 'unknown'; }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Profile captured on wrong device | Wrong sizing decisions | Include device fingerprint in profile |
| Free memory fluctuates during measurement | Overcommitted allocations | Sample free memory 3 times, use minimum |
| Android kills background process | Lost state | Write profile to disk immediately |
| CPU affinity restrictions differ by app | Threads on wrong cores | Read cgroup cpuset, not just os.cpus() |

## Dependencies
- Node.js >= 18 (for `os` module stability)
- Access to `/sys/fs/cgroup/` for CPU affinity info

## Pitfalls & Bugs
- `os.freemem()` on Android reports system-wide free memory, not what's available to this app's cgroup. Cross-reference with `/proc/meminfo`.
- ARM big.LITTLE topology is not exposed by standard Node.js APIs; parse `/sys/devices/system/cpu/cpu*/cpufreq/cpuinfo_max_freq` to identify performance cores.
- Storage space can change rapidly on shared devices. Re-check before each model load.
- Thermal throttling changes effective performance over time. Record initial readings and implement periodic re-checks.
