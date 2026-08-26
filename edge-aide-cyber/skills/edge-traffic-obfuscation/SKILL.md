# Edge Traffic Obfuscation & Shaping

## What To Do
Implement traffic shaping and obfuscation to make tool execution traffic blend with normal user activity. Randomize request timing, vary User-Agent strings, and add noise to prevent fingerprinting.

## Why
Even with Tor, sophisticated targets can fingerprint scanning traffic by timing patterns, request frequency, and User-Agent consistency. Traffic shaping makes automated scanning look like legitimate browsing.

## Code Guidance

```javascript
// src/opsec/traffic-shaper.mjs

const USER_AGENTS = [
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/125.0.0.0 Safari/537.36',
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 Safari/605.1.15',
  'Mozilla/5.0 (X11; Linux x86_64; rv:128.0) Gecko/20100101 Firefox/128.0',
  'Mozilla/5.0 (iPad; CPU OS 17_5 like Mac OS X) AppleWebKit/605.1.15 Safari/605.1.15',
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:128.0) Gecko/20100101 Firefox/128.0',
];

export class TrafficShaper {
  #minDelay;
  #maxDelay;
  #burstLimit;
  #recentRequests = [];

  constructor({ minDelayMs = 200, maxDelayMs = 2000, burstLimit = 5 } = {}) {
    this.#minDelay = minDelayMs;
    this.#maxDelay = maxDelayMs;
    this.#burstLimit = burstLimit;
  }

  async throttle() {
    const now = Date.now();

    // Clean old requests (sliding window 60s)
    this.#recentRequests = this.#recentRequests.filter(t => now - t < 60000);

    // If burst limit hit, add longer delay
    if (this.#recentRequests.length >= this.#burstLimit) {
      const waitTime = 3000 + Math.random() * 5000;
      await new Promise(r => setTimeout(r, waitTime));
    }

    // Random delay
    const delay = this.#minDelay + Math.random() * (this.#maxDelay - this.#minDelay);
    await new Promise(r => setTimeout(r, delay));
    this.#recentRequests.push(Date.now());
  }

  getRandomUA() {
    return USER_AGENTS[Math.floor(Math.random() * USER_AGENTS.length)];
  }

  getHeaders() {
    return {
      'User-Agent': this.getRandomUA(),
      'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
      'Accept-Language': 'en-US,en;q=0.9',
      'Accept-Encoding': 'gzip, deflate, br',
      'Connection': 'keep-alive',
      'Upgrade-Insecure-Requests': '1',
    };
  }

  getStatus() {
    return {
      minDelay: this.#minDelay,
      maxDelay: this.#maxDelay,
      burstLimit: this.#burstLimit,
      recentRequests: this.#recentRequests.length,
    };
  }
}
```

## Threat Matrix

| Threat | Impact | Mitigation |
|---|---|---|
| Consistent timing reveals automation | Fingerprinted as scanner | Random jitter 200-2000ms |
| Same User-Agent across requests | Correlated as single tool | Random UA from pool per request |
| Burst of requests triggers WAF | IP banned, testing blocked | Sliding window burst limiter |
| Request patterns match known tools | Signature-based detection | Randomize header order |

## Dependencies
- None (pure JavaScript)

## Pitfalls & Bugs
- Too much delay makes testing impractical — allow operator to override
- Some targets use aggressive rate limiting (1 req/sec) — detect 429 and back off
- Random UA should match the browser/OS context (don't use mobile UA from desktop)
- Header order matters for TLS fingerprinting — randomize carefully
- Burst limit should be per-target, not global
