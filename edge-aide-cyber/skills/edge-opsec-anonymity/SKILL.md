# Edge Operational Security & Anonymity Layer

## What To Do
Implement a comprehensive OpSec layer that ensures all network operations from the edge workbench are anonymized. This includes Tor routing for all outbound connections, DNS leak prevention, WebRTC leak blocking, and traffic fingerprint obfuscation. The operator's real IP must never touch the target during authorized testing.

## Why
Bug bounty hunters and pentesters operate under legal authorization but need to protect their identity, location, and infrastructure from targets (especially hardened ones that may log or counter-research). Anonymous operation prevents:
- Retaliation from compromised targets
- IP-based attribution to the tester
- Legal exposure from shared infrastructure
- Target-side detection of testing activity

## Architecture

```
┌──────────────────────────────────────┐
│         Edge AIDE Workbench          │
│  ┌────────────────────────────────┐  │
│  │      OpSec Manager             │  │
│  │  ┌──────┐ ┌──────┐ ┌───────┐  │  │
│  │  │ Tor  │ │ VPN  │ │ Proxy │  │  │
│  │  │ SOCKS│ │Wire- │ │ Chain │  │  │
│  │  │ 9050 │ │ guard│ │       │  │  │
│  │  └──┬───┘ └──┬───┘ └──┬────┘  │  │
│  │     └────────┼────────┘       │  │
│  │              ▼                │  │
│  │    DNS Leak Guard             │  │
│  │    WebRTC Blocker             │  │
│  │    Traffic Shaper             │  │
│  └──────────────┬───────────────┘  │
│                 ▼                  │
│        Tool Adapters              │
│  (all traffic routed through OpSec)│
└──────────────────────────────────────┘
```

## Code Guidance

```javascript
// src/opsec/manager.mjs
import { SocksProxyAgent } from 'socks-proxy-agent';
import { HttpsProxyAgent } from 'https-proxy-agent';

export class OpSecManager {
  #config = {
    mode: 'off',           // 'off' | 'tor' | 'vpn' | 'proxy-chain'
    torSocksPort: 9050,
    proxyChain: [],         // [{ host, port, protocol }]
    dnsOverTor: true,
    blockWebRTC: true,
    trafficShaping: false,
    minDelayMs: 100,
    maxDelayMs: 500,
  };

  #agent = null;
  #dnsResolver = null;

  async init(config = {}) {
    Object.assign(this.#config, config);

    if (this.#config.mode === 'tor') {
      await this.#initTor();
    } else if (this.#config.mode === 'proxy-chain') {
      await this.#initProxyChain();
    }

    if (this.#config.dnsOverTor) {
      this.#dnsResolver = this.#createTorDNSResolver();
    }
  }

  async #initTor() {
    // Verify Tor is running on SOCKS port
    try {
      const testUrl = `socks5h://127.0.0.1:${this.#config.torSocksPort}`;
      this.#agent = new SocksProxyAgent(testUrl);

      // Test connectivity
      const res = await fetch('https://check.torproject.org/api/ip', {
        agent: this.#agent,
        signal: AbortSignal.timeout(10000),
      });
      const data = await res.json();
      console.log(`  tor: connected via ${data.IP}`);
    } catch (err) {
      throw new Error(`Tor connection failed: ${err.message}. Start Tor first: tor &`);
    }
  }

  async #initProxyChain() {
    // Build nested proxy agents (outermost first)
    let currentAgent = null;
    for (const proxy of [...this.#config.proxyChain].reverse()) {
      if (proxy.protocol === 'socks5') {
        currentAgent = new SocksProxyAgent(
          `socks5h://${proxy.host}:${proxy.port}`,
          currentAgent
        );
      } else {
        currentAgent = new HttpsProxyAgent(
          `http://${proxy.host}:${proxy.port}`,
          currentAgent
        );
      }
    }
    this.#agent = currentAgent;
  }

  #createTorDNSResolver() {
    // DNS over Tor SOCKS — prevents DNS leaks
    return async (hostname) => {
      // Use Tor's DNS resolution (SOCKS5h handles this)
      const res = await fetch(`https://dns.google/resolve?name=${hostname}`, {
        agent: this.#agent,
        signal: AbortSignal.timeout(5000),
      });
      const data = await res.json();
      return data.Answer?.map(a => a.data) || [];
    };
  }

  // Get fetch options routed through OpSec layer
  getFetchOptions(options = {}) {
    const opts = { ...options };
    if (this.#agent) {
      opts.agent = this.#agent;
    }
    return opts;
  }

  // Get HTTP/HTTPS agent for use with tool adapters
  getAgent() {
    return this.#agent;
  }

  // Random delay for traffic shaping
  async shapedDelay() {
    if (!this.#config.trafficShaping) return;
    const delay = this.#config.minDelayMs +
      Math.random() * (this.#config.maxDelayMs - this.#config.minDelayMs);
    await new Promise(r => setTimeout(r, delay));
  }

  getStatus() {
    return {
      mode: this.#config.mode,
      connected: !!this.#agent,
      dnsOverTor: this.#config.dnsOverTor,
      trafficShaping: this.#config.trafficShaping,
    };
  }
}
```

## Threat Matrix

| Threat | Impact | Mitigation |
|---|---|---|
| Tor not running when mode=tor | All requests fail, operator exposed | Health check on init, fail-closed |
| DNS leak bypasses Tor | Real IP exposed to DNS servers | Use socks5h:// (Tor handles DNS) |
| WebRTC leak in browser UI | Real IP visible via STUN | Add CSP header blocking WebRTC |
| Traffic timing correlation | Operator fingerprinted by timing | Random delay jitter (100-500ms) |
| Proxy chain order wrong | Traffic goes through wrong exit | Validate chain order at init |
| Tor circuit too long | Latency kills usability | Max 3 hops, option to refresh circuit |

## Dependencies
- `socks-proxy-agent` npm package (for SOCKS5 routing)
- `https-proxy-agent` npm package (for HTTP proxy routing)
- Tor daemon running on device (Termux: `pkg install tor && tor &`)
- Node.js >= 18 for native fetch agent support

## Pitfalls & Bugs
- Android may kill Tor daemon on screen lock — implement PID monitoring + restart
- Tor SOCKS port 9050 is default; verify with `ss -tlnp | grep 9050`
- Some tool adapters may use `child_process.exec` for nmap/curl — those bypass the agent layer; need `proxychains` wrapper or env var injection
- Tor exit nodes are public — never send authentication tokens through Tor
- Traffic shaping adds latency; make it configurable per-operation
- SOCKS5h vs SOCKS5: always use `socks5h://` to route DNS through Tor
- `https-proxy-agent` doesn't support SOCKS; use `socks-proxy-agent` for Tor
- Test with `curl --socks5-hostname 127.0.0.1:9050 https://check.torproject.org/api/ip` before integrating
