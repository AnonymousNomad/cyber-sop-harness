# Samsung Linux Environment Integration

## What To Do
Understand and work within Samsung's Linux container on Android. Map the container boundaries, available syscalls, cgroup constraints, and Android integration points. Build the terminal to coexist with Android's lifecycle management rather than fight it.

## Device Profile
- **Device:** Samsung Galaxy A10 FE 5G
- **Kernel:** 6.6.77-android15-8 (aarch64)
- **RAM:** 7.2GB total, 6.5GB used, ~126MB free at rest
- **Swap:** 11GB ZRAM, ~5GB used
- **CPU:** 8 cores (4x Cortex-A520 @1.95GHz + 4x Cortex-A720 @2.5GHz)
- **Storage:** 106GB total, 91% used, ~42MB/s write
- **Container:** Ubuntu 24.04 in Android app sandbox
- **Security:** AppArmor `untrusted_app`, Seccomp filter (2 filters)
- **Capabilities:** All zeros — container has no real capabilities
- **cgroup:** `apps/uid_10333/pid_*` (Android app lifecycle)

## Why
Samsung's Linux environment is just another Android app. Android treats it as expendable — kills it when memory is low, restricts its syscalls via seccomp, and limits its cgroup resources. To build a reliable terminal, we must work WITH Android's lifecycle, not against it.

## What We CAN Do (Root Inside Container)
- Install packages via apt (Ubuntu 24.04 repos)
- Run Node.js, Python, GCC, git, make
- Access `/proc/sys/vm/` for memory tuning (partial)
- Use `am` (Activity Manager) to interact with Android
- Use tmpfs for fast I/O (106GB available)
- Set process priority via nice/ionice
- Use cpuset cgroup for CPU pinning

## What We CANNOT Do
- Load kernel modules (seccomp + no capabilities)
- Modify AppArmor policy (read-only)
- Change Android's OOM killer behavior directly
- Access `/proc/sys/vm/overcommit_memory` (permission denied)
- Run iptables/nftables (no capabilities)
- Access real block devices (dm-71 only)

## Code Guidance
```javascript
// src/android/device-environment.mjs
import { execSync } from 'node:child_process';

export class DeviceEnvironment {
  #profile = null;

  async detect() {
    this.#profile = {
      kernel: execSync('uname -r').toString().trim(),
      arch: execSync('uname -m').toString().trim(),
      cpuCount: parseInt(execSync('nproc').toString().trim()),
      totalMemKB: this.#parseMemInfo('MemTotal'),
      freeMemKB: this.#parseMemInfo('MemAvailable'),
      swapTotalKB: this.#parseMemInfo('SwapTotal'),
      swapFreeKB: this.#parseMemInfo('SwapFree'),
      storageFreeGB: this.#getStorageFree(),
      seccompMode: this.#getSeccompMode(),
      appArmorCtx: this.#getAppArmorContext(),
      cgroupPath: this.#getCgroupPath(),
    };
    return this.#profile;
  }

  #parseMemInfo(key) {
    const data = execSync('cat /proc/meminfo').toString();
    const match = data.match(new RegExp(`${key}:\s+(\d+)`));
    return match ? parseInt(match[1]) : 0;
  }

  #getStorageFree() {
    try {
      const out = execSync('df -BG / | tail -1').toString();
      const parts = out.split(/\s+/);
      return parseInt(parts[3]) || 0;
    } catch { return 0; }
  }

  #getSeccompMode() {
    try {
      const status = execSync('cat /proc/self/status').toString();
      const match = status.match(/Seccomp:\s+(\d+)/);
      return match ? parseInt(match[1]) : -1;
    } catch { return -1; }
  }

  #getAppArmorContext() {
    try {
      return execSync('cat /proc/self/attr/current').toString().trim();
    } catch { return 'unknown'; }
  }

  #getCgroupPath() {
    try {
      return execSync('cat /proc/self/cgroup').toString().trim();
    } catch { return 'unknown'; }
  }

  get profile() { return this.#profile; }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Android kills Linux process | All work lost | OOM resilience daemon + state checkpoint |
| Seccomp blocks syscalls | Tool failures | Detect available syscalls, fallback gracefully |
| Storage fills up | Cannot write logs/evidence | Monitor disk usage, auto-cleanup |
| App killed on screen off | Sessions interrupted | Foreground service notification via `am` |

## Dependencies
- Node.js 22 (available)
- apt package manager (Ubuntu 24.04)
- `/proc` filesystem access (available)

## Pitfalls
- Samsung's Linux environment resets on reboot — persist state to /data
- `apt update` may be slow on cellular — cache packages locally
- Container has no real capabilities — cannot modify kernel parameters directly
- Android 15 may change container behavior in updates
- tmpfs shares root filesystem space — don't fill it up