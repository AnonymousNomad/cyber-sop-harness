import fs from 'node:fs/promises';
import path from 'node:path';

const DEFAULT_POLL_MS = 2000;
const IGNORE_DIRS = ['node_modules', '.git', '.aide', 'dist', 'build', '__pycache__'];

export function createFileWatcher(fileBoundary, options = {}) {
  const pollInterval = options.pollInterval || DEFAULT_POLL_MS;
  const extensions = options.extensions || ['.js', '.mjs', '.ts', '.json', '.md', '.html', '.css'];
  const snapshot = new Map();
  let timer = null;
  let onChange = null;

  function shouldWatch(filePath) {
    const ext = path.extname(filePath).toLowerCase();
    return extensions.includes(ext);
  }

  function isIgnored(dirName) {
    return IGNORE_DIRS.includes(dirName);
  }

  async function scanDir(dir, relativeTo) {
    const changes = [];
    let entries;
    try {
      entries = await fs.readdir(dir, { withFileTypes: true });
    } catch { return changes; }

    for (const entry of entries) {
      if (isIgnored(entry.name)) continue;
      const fullPath = path.join(dir, entry.name);
      const relPath = path.relative(relativeTo, fullPath);

      if (entry.isDirectory()) {
        changes.push(...await scanDir(fullPath, relativeTo));
      } else if (entry.isFile() && shouldWatch(entry.name)) {
        try {
          const stat = await fs.stat(fullPath);
          const prev = snapshot.get(relPath);
          if (!prev) {
            changes.push({ type: 'created', path: relPath, mtime: stat.mtimeMs });
          } else if (stat.mtimeMs > prev.mtime) {
            changes.push({ type: 'modified', path: relPath, mtime: stat.mtimeMs });
          }
          snapshot.set(relPath, stat.mtimeMs);
        } catch {}
      }
    }
    return changes;
  }

  async function detectChanges() {
    const changes = [];
    const root = fileBoundary.root;
    let entries;
    try {
      entries = await fs.readdir(root, { withFileTypes: true });
    } catch { return changes; }

    for (const entry of entries) {
      if (isIgnored(entry.name)) continue;
      const fullPath = path.join(root, entry.name);
      if (entry.isDirectory()) {
        changes.push(...await scanDir(fullPath, root));
      } else if (entry.isFile() && shouldWatch(entry.name)) {
        const relPath = entry.name;
        try {
          const stat = await fs.stat(fullPath);
          const prev = snapshot.get(relPath);
          if (!prev) {
            changes.push({ type: 'created', path: relPath, mtime: stat.mtimeMs });
          } else if (stat.mtimeMs > prev.mtime) {
            changes.push({ type: 'modified', path: relPath, mtime: stat.mtimeMs });
          }
          snapshot.set(relPath, stat.mtimeMs);
        } catch {}
      }
    }
    return changes;
  }

  function start(callback) {
    onChange = callback;
    timer = setInterval(async () => {
      if (!onChange) return;
      try {
        const changes = await detectChanges();
        for (const change of changes) {
          onChange(change);
        }
      } catch {}
    }, pollInterval);
    if (timer.unref) timer.unref();
  }

  function stop() {
    if (timer) { clearInterval(timer); timer = null; }
    onChange = null;
  }

  return { start, stop, detectChanges, get fileCount() { return snapshot.size; } };
}
