# Edge Auto-Debug System

## What To Do
Implement a silent background debugger that watches workspace files for changes, detects syntax errors via `node --check` and stderr pattern matching, pushes notifications to the terminal UI, and optionally auto-fixes via the model. No NLP involved — pure deterministic checking.

## Why
Developers lose time to typos and syntax errors that a machine catches instantly. A silent auto-debugger catches these before the developer even looks at the terminal. The "auto-fix" path uses the model only when the user confirms, keeping governance intact.

## Architecture
```
File saved → Watcher (3s poll) → Detector (node --check)
  → Errors found → Notifier (WS push to terminal)
    → User confirms → Fixer (model reads file, applies fix, re-verifies)
      → Evidence chain records the fix
```

## Code Guidance
- `src/autodebug/watcher.mjs`: Polls filesystem using `readdir` with `{ withFileTypes: true }`. Stores mtime snapshot in a Map. Ignores `node_modules`, `.git`, `.aide`, `dist`, `build`, `__pycache__`. Extensions: `.js`, `.mjs`, `.ts`, `.json`, `.md`, `.html`, `.css`.
- `src/autodebug/detector.mjs`: Runs `node --check` for JS/MJS. Captures stdout+stderr. Checks for `SyntaxError`, `Unexpected`, `Missing`, `Unterminated`, `Expected` patterns. Parses line/column from standard Node error format.
- `src/autodebug/fixer.mjs`: Reads file content, builds error summary, sends to model with "return only valid code" instruction. Strips markdown fences from response. Writes fixed file. Re-runs detector to verify. Records to evidence chain.
- `src/autodebug/notifier.mjs`: Sends `autodebug.detected`, `autodebug.fixed`, `autodebug.fix_failed`, `autodebug.status` messages via WebSocket to all connected clients.

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Model fix introduces new errors | Broken code | Re-verify after fix; roll back if still broken |
| Model fix changes unrelated code | Silent behavior change | Prompt says "do not change anything unrelated to the errors" |
| Watcher consumes excessive CPU | Battery drain on tablet | 3s poll interval; skip hidden dirs; extension filter |
| Auto-fix without confirmation | Unintended changes | autoMode defaults to off; requires `/autodebug auto on` |
| File read during write (race) | Corrupted content | Try/catch around file operations; log failures |

## Dependencies
- Node.js built-in `child_process.execFile` for syntax checking
- Node.js built-in `fs/promises` for file watching
- Model provider (for auto-fix path only — not required for detection)
- WebSocket protocol (for notification push)

## Pitfalls
- `node --check` for `.mjs` files exits 0 even with syntax errors (outputs as warnings). Must capture stderr and check for error patterns, not rely on exit code.
- Some syntax errors produce `UnhandledPromiseRejectionWarning` format, not standard `file:line:col` format. Parser handles both.
- File mtime resolution varies by filesystem. On Android tmpfs, sub-millisecond resolution exists. On some FUSE mounts, resolution is 1 second. Use creation detection (new files) as primary, modification as secondary.
- Model may return markdown-fenced code. Always strip ```` ```js ... ``` ```` before writing.
