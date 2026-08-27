# Edge Storage Optimizer

## What To Do
Monitor and optimize storage usage. The device is 91% full (11GB free). Clean old logs, compress evidence, rotate checkpoints, and use tmpfs for hot data.

## Why
With only 11GB free and 42MB/s write speed, storage is a bottleneck. Old logs, large evidence files, and model checkpoints can fill the disk quickly. The optimizer runs periodically to keep storage healthy.

## Code Guidance

```javascript
// src/android/storage-optimizer.mjs
import { readdir, stat, unlink, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { execSync } from 'node:child_process';

export class StorageOptimizer {
  #workspaceDir;
  #maxLogSizeMB = 50;
  #maxEvidenceEntries = 1000;
  #tmpfsDir = '/dev/shm';

  constructor(workspaceDir) {
    this.#workspaceDir = workspaceDir;
  }

  async getDiskUsage() {
    const out = execSync('df -BM / | tail -1').toString();
    const parts = out.split(/\s+/);
    return {
      totalMB: parseInt(parts[1]) || 0,
      usedMB: parseInt(parts[2]) || 0,
      freeMB: parseInt(parts[3]) || 0,
      percentUsed: parseInt(parts[4]) || 0,
    };
  }

  async cleanup() {
    const actions = [];
    actions.push(...await this.#cleanOldLogs());
    actions.push(...await this.#rotateEvidence());
    actions.push(...await this.#cleanTempFiles());
    actions.push(...await this.#compressLargeFiles());
    return actions;
  }

  async #cleanOldLogs() {
    const actions = [];
    const logDir = join(this.#workspaceDir, 'logs');
    try {
      const files = await readdir(logDir);
      for (const file of files) {
        const fp = join(logDir, file);
        const s = await stat(fp);
        const ageHours = (Date.now() - s.mtimeMs) / (1000 * 60 * 60);
        if (ageHours > 48) {
          await unlink(fp);
          actions.push(`cleaned old log: ${file}`);
        }
      }
    } catch {}
    return actions;
  }

  async #rotateEvidence() {
    const evidenceDir = join(this.#workspaceDir, 'evidence');
    try {
      const files = await readdir(evidenceDir);
      if (files.length > this.#maxEvidenceEntries) {
        const toDelete = files.sort().slice(0, files.length - this.#maxEvidenceEntries);
        for (const f of toDelete) {
          await unlink(join(evidenceDir, f));
        }
        return [`rotated ${toDelete.length} old evidence entries`];
      }
    } catch {}
    return [];
  }

  async #cleanTempFiles() {
    const tempDir = join(this.#workspaceDir, '.tmp');
    try {
      const files = await readdir(tempDir);
      for (const f of files) {
        await unlink(join(tempDir, f));
      }
      return files.length > 0 ? [`cleaned ${files.length} temp files`] : [];
    } catch {}
    return [];
  }

  async #compressLargeFiles() {
    // Find files > 1MB and gzip them
    const actions = [];
    try {
      const out = execSync(
        `find ${this.#workspaceDir} -type f -size +1M -name "*.log" -o -name "*.json" 2>/dev/null | head -5`
      ).toString().trim().split('\n').filter(Boolean);
      for (const f of out) {
        try {
          execSync(`gzip -f "${f}"`, { stdio: 'ignore' });
          actions.push(`compressed: ${f}`);
        } catch {}
      }
    } catch {}
    return actions;
  }

  // Use tmpfs for hot data (fast writes, lost on reboot)
  getTmpfsPath(subpath) {
    return join(this.#tmpfsDir, 'edge-cyber', subpath);
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Cleanup deletes active evidence | Audit trail broken | Never delete current engagement data |
| tmpfs fills up | Write failures | Monitor tmpfs usage |
| Compression fails on binary files | Disk still full | Skip non-text files |

## Dependencies
- `df`, `find`, `gzip` (available)
- Node.js fs module

## Pitfalls
- tmpfs shares root disk space — don't store too much there
- Evidence rotation must respect engagement scope
- Log cleanup should preserve last 48 hours minimum
- Compression adds CPU overhead — only for files >1MB