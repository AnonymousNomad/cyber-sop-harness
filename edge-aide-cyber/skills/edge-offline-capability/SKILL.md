# Edge Offline Capability

## What To Do
Ensure the workbench functions fully offline. All tool outputs cached, SOPs local, model inference local, evidence stored locally. Network only needed for external tool execution and model sync.

## Why
Bug bounty testers often work from locations without reliable internet (coffee shops, co-working spaces, field). The core workflow must work offline with network as optional enhancement.

## Code Guidance
```javascript
// Cache tool results locally
class OfflineCache {
  async get(key) { /* check local JSONL cache */ }
  async set(key, value, ttlMs) { /* write to cache with expiry */ }
  async getOrFetch(key, fetchFn, ttlMs) {
    const cached = await this.get(key);
    if (cached) return cached;
    const fresh = await fetchFn();
    await this.set(key, fresh, ttlMs);
    return fresh;
  }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Stale cache data | Wrong recon results | TTL-based expiry |
| Cache grows unbounded | Disk exhaustion | LRU eviction, max 100MB |

## Dependencies
- File boundary module, cipher state bus

## Pitfalls
- DNS cache may point to old IPs — short TTLs for recon data
- Model inference must work fully offline (LFM2.5 local)
- Evidence chain must be append-only even offline