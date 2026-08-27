# Edge Kernel Parameter Tuning

## What To Do
Tune kernel parameters accessible from inside the container to optimize memory management, reduce swap pressure, and improve I/O performance for the terminal.

## Why
Even inside a container, we can tune some `/proc/sys/vm/` parameters to reduce memory pressure. Lowering swappiness tells the kernel to prefer keeping processes in RAM over swapping. Adjusting dirty ratios controls how much data can buffer before writing to disk.

## Available Parameters (Inside Container)

| Parameter | Current | Recommended | Effect |
|---|---|---|---|
| `vm.swappiness` | 60 (default) | 10-20 | Use swap less aggressively |
| `vm.dirty_ratio` | 20 | 5 | Flush dirty pages sooner |
| `vm.dirty_background_ratio` | 10 | 2 | Start background flush earlier |
| `vm.dirty_expire_centisecs` | 3000 | 500 | Expire dirty pages faster |
| `vm.min_free_kbytes` | varies | 32768 | Reserve more free memory |

## Code Guidance

```javascript
// src/android/kernel-tuner.mjs
import { execSync } from 'node:child_process';

export class KernelTuner {
  #applied = [];

  tune(profile = 'balanced') {
    const profiles = {
      memory_saver: {
        'vm/swappiness': '10',
        'vm/dirty_ratio': '5',
        'vm/dirty_background_ratio': '2',
        'vm/dirty_expire_centisecs': '500',
      },
      balanced: {
        'vm/swappiness': '30',
        'vm/dirty_ratio': '10',
        'vm/dirty_background_ratio': '5',
      },
      io_performance: {
        'vm/swappiness': '10',
        'vm/dirty_ratio': '15',
        'vm/dirty_background_ratio': '5',
        'vm/dirty_writeback_centisecs': '500',
      },
    };

    const params = profiles[profile] || profiles.balanced;

    for (const [path, value] of Object.entries(params)) {
      try {
        execSync(`echo ${value} > /proc/sys/${path}`, { stdio: 'ignore' });
        this.#applied.push({ path, value, ok: true });
      } catch (err) {
        this.#applied.push({ path, value, ok: false, error: err.message });
      }
    }

    return this.#applied;
  }

  getStatus() {
    const result = {};
    for (const { path, ok, value } of this.#applied) {
      if (ok) {
        try {
          result[path] = execSync(`cat /proc/sys/${path}`).toString().trim();
        } catch {
          result[path] = 'unreadable';
        }
      }
    }
    return result;
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Parameter write denied by seccomp | Tuning silently fails | Always try/catch, report failures |
| Wrong swappiness value | More swapping, worse perf | Test incrementally, monitor free memory |
| Dirty ratio too low | Excessive disk writes | Don't go below 2 for battery life |

## Dependencies
- `/proc/sys/vm/` write access (partially available — some params denied)

## Pitfalls
- `vm.overcommit_memory` is denied — cannot change OOM behavior
- `vm.min_free_kbytes` may be denied — test before relying on it
- Changes are per-container, reset on container restart
- Samsung may override these at Android level
- ZRAM swap behavior is controlled by Android, not by swappiness alone