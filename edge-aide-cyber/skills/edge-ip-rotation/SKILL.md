# Edge IP Rotation

## What To Do
Implement automatic IP rotation for长时间扫描 sessions. Cycle through Tor circuits, VPN servers, or proxy chains to prevent IP-based rate limiting and fingerprinting.

## Why
Long-running bug bounty engagements (hours/days) risk IP bans if the same IP scans continuously. IP rotation distributes scanning across multiple IPs.

## Code Guidance
```javascript
// src/opsec/ip-rotator.mjs
export class IPRotator {
  #rotators = []; // [torCircuitRefresh, vpnSwitch, proxyRotate]
  #intervalMs;
  #currentIP;

  constructor({ intervalMs = 600000, mode = 'tor' }) { // 10min default
    this.#intervalMs = intervalMs;
  }

  async startRotation() {
    setInterval(async () => {
      await this.rotate();
    }, this.#intervalMs);
  }

  async rotate() {
    // Refresh Tor circuit or switch VPN server
    // Verify new IP
    // Log rotation event to evidence chain
  }

  getCurrentIP() { return this.#currentIP; }
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| Rotation during active connection | Broken session | Drain connections before rotate |
| New IP also banned | No benefit | Check IP reputation before use |
| Rotation too frequent | Slow scanning | Minimum 5min between rotations |

## Dependencies
- TorManager (for circuit refresh) or VPNManager (for server switch)

## Pitfalls
- Tor NEWNYM signal takes ~10s to take effect
- Some VPN providers have limited server count
- IP rotation should pause during active tool execution