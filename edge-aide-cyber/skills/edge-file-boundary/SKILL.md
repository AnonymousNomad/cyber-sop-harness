# Edge File System Boundary

## What To Do
Implement a workspace jail that confines all file reads/writes to a designated project root directory. Every path from user input, model output, or tool parameters must be resolved and verified against this boundary.

## Why
Path traversal is the most common vulnerability in file-handling applications. On a security tool running on a personal tablet, escaping the workspace could expose photos, messages, credentials, or system files.

## Code Guidance
```javascript
import path from 'node:path';
import fs from 'node:fs/promises';

export function createFileBoundary(rootDir) {
  const resolvedRoot = path.resolve(rootDir);

  function safeJoin(...segments) {
    const resolved = path.resolve(resolvedRoot, ...segments);
    if (!resolved.startsWith(resolvedRoot + path.sep) && resolved !== resolvedRoot) {
      throw new Error(`path escapes workspace boundary: ${resolved}`);
    }
    return resolved;
  }

  return {
    readFile: async (...seg) => fs.readFile(safeJoin(...seg), 'utf8'),
    writeFile: async (...args) => {
      const filePath = safeJoin(args[0]);
      await fs.mkdir(path.dirname(filePath), { recursive: true });
      return fs.writeFile(filePath, args[1], 'utf8');
    },
    listDir: async (...seg) => fs.readdir(safeJoin(...seg)),
    stat: async (...seg) => fs.stat(safeJoin(...seg)),
    resolve: safeJoin,
    get root() { return resolvedRoot; },
  };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Path traversal (`../../etc/passwd`) | Read arbitrary files | Resolve then prefix-check against root |
| Symlink escape | Bypass via symbolic link | Use `fs.realpath()` after join; verify still under root |
| Null byte injection | Path truncation | Strip `\x00` from all input paths |
| Unicode normalization bypass | Different encoding reaches filesystem | Normalize to NFC before resolution |
| Race between check and open | TOCTOU vulnerability | Open with `O_NOFOLLOW` where available; re-verify |

## Dependencies
- Node.js built-in `path`, `fs/promises`

## Pitfalls & Bugs
- `path.resolve()` on Windows uses backslashes; on Linux/Termux it's forward slashes. Always use `path.sep` for comparisons.
- Symlinks inside the workspace pointing outside it will pass `startsWith` but read external files. Call `fs.realpath()` on the final path before reading.
- URL-encoded paths (`%2e%2e%2f`) arrive as literal strings; decode before resolving.
- Double-encoded paths require two decode rounds; normalize iteratively until stable.
- Empty string as a segment resolves to the root itself, which is valid but should be logged.
