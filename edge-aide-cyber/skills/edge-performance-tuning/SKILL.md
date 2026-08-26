# Edge Performance Tuning

## What To Do
Profile and optimize the server for edge device constraints: reduce memory footprint, optimize WebSocket message handling, and minimize startup time.

## Why
Edge devices have limited RAM (7.2GB) and slow storage. Every millisecond of startup time and every megabyte of RAM matters when running alongside other apps.

## Code Guidance
```javascript
// Lazy-load heavy modules
const lazyLoad = (path) => {
  let mod = null;
  return () => mod ??= import(path);
};

// Lazy-load adapters
const nmapLoader = lazyLoad('./adapters/nmap-scan.mjs');

// WebSocket message batching
class MessageBatcher {
  #queue = [];
  #flushInterval = 50; // ms
  flush() {
    if (this.#queue.length === 0) return;
    const batch = this.#queue.splice(0);
    // Send batched
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Memory leak in long sessions | OOM kill | Monitor RSS, enforce limits |
| WebSocket queue grows unbounded | Browser freeze | Cap queue at 1000 messages |

## Dependencies
- Node.js performance hooks, server.mjs

## Pitfalls
- Android may kill background Node.js process — keep RSS under 512MB
- WebSocket binary mode is faster than JSON text mode
- Lazy loading adds first-use latency — pre-load critical modules