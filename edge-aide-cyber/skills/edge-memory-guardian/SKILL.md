# Edge Memory Guardian

## What To Do
Implement a memory watchdog that monitors RAM usage, detects pressure before OOM kill, and takes protective action (compress, checkpoint, reduce footprint). This is the #1 defense against crashes.

## Why
The device has 7.2GB RAM with 6.5GB used at rest. Android's OOM killer will kill the Linux container when free memory drops below ~50MB. The memory guardian detects pressure early and takes action before the killer strikes.

## Code Guidance

```javascript
// src/android/memory-guardian.mjs
import { execSync } from 'node:child_process';

export class MemoryGuardian {
  #thresholdMB = 100;    # Alert when free < 100MB
  #criticalMB = 50;      # Take action when free < 50MB
  #checkpointMB = 30;    # Emergency checkpoint when free < 30MB
  #checkIntervalMs = 5000;
  #timer = null;
  #onPressure = null;

  constructor({ thresholdMB = 100, criticalMB = 50 } = {}) {
    this.#thresholdMB = thresholdMB;
    this.#criticalMB = criticalMB;
  }

  start(onPressure) {
    this.#onPressure = onPressure;
    this.#timer = setInterval(() => this.#check(), this.#checkIntervalMs);
  }

  stop() {
    if (this.#timer) clearInterval(this.#timer);
  }

  #check() {
    const mem = this.getMemoryStatus();

    if (mem.freeMB < this.#checkpointMB) {
      this.#onPressure?.({ level: 'emergency', ...mem });
      this.#emergencyAction();
    } else if (mem.freeMB < this.#criticalMB) {
      this.#onPressure?.({ level: 'critical', ...mem });
      this.#criticalAction();
    } else if (mem.freeMB < this.#thresholdMB) {
      this.#onPressure?.({ level: 'warning', ...mem });
    }
  }

  getMemoryStatus() {
    const data = execSync('cat /proc/meminfo').toString();
    const total = this.#parse(data, 'MemTotal');
    const free = this.#parse(data, 'MemFree');
    const available = this.#parse(data, 'MemAvailable');
    const swapTotal = this.#parse(data, 'SwapTotal');
    const swapFree = this.#parse(data, 'SwapFree');

    return {
      totalMB: Math.round(total / 1024),
      freeMB: Math.round(free / 1024),
      availableMB: Math.round(available / 1024),
      usedMB: Math.round((total - free) / 1024),
      swapUsedMB: Math.round((swapTotal - swapFree) / 1024),
      swapTotalMB: Math.round(swapTotal / 1024),
      pressure: available < this.#thresholdMB ? 'high' : 'normal',
    };
  }

  #parse(data, key) {
    const match = data.match(new RegExp(`${key}:\s+(\d+)`));
    return match ? parseInt(match[1]) : 0;
  }

  #emergencyAction() {
    // Drop filesystem caches
    try { execSync('sync && echo 3 > /proc/sys/vm/drop_caches'); } catch {}
    // Kill background processes we don't need
    try { execSync('pkill -f "node.*background" 2>/dev/null'); } catch {}
    // Trigger state checkpoint
    this.#onPressure?.({ level: 'checkpoint' });
  }

  #criticalAction() {
    // Drop caches
    try { execSync('sync && echo 1 > /proc/sys/vm/drop_caches'); } catch {}
    // Reduce Node.js heap
    if (global.gc) global.gc();
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| OOM kill mid-operation | Data loss, corruption | Emergency checkpoint before kill |
| Memory leak in Node.js | Gradual pressure increase | RSS monitoring, forced GC |
| Swap thrashing | System becomes unresponsive | Detect swap usage, reduce footprint |
| Multiple Node processes | Competition for memory | Single daemon, process count monitoring |

## Dependencies
- `/proc/meminfo` access (available)
- `/proc/sys/vm/drop_caches` (may need try/catch — container restrictions)
- `global.gc()` (Node.js with --expose-gc flag)

## Pitfalls
- `drop_caches` may be denied by seccomp — wrap in try/catch
- Forcing GC is not instant — may not help in emergency
- Memory check interval too frequent wastes CPU; too slow misses window
- Samsung's ZRAM swap is aggressive — may already be compressed
- Android may have its own memory monitoring that conflicts