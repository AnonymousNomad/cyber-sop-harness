# Edge Web Testing Adapters

## What To Do
Implement HTTP header inspection and bounded URL fetching as typed adapters. These operate within strict size limits and never follow arbitrary redirects.

## Why
Web application testing starts with understanding headers, response codes, and content types. These adapters provide that intelligence without giving the model unrestricted HTTP client capabilities.

## Code Guidance
```javascript
const SENSITIVE_HEADERS = ['authorization', 'cookie', 'set-cookie', 'x-api-key', 'x-auth-token'];

export function createHttpHeaderAdapter() {
  return {
    name: 'http.headers',
    capability: 'web.recon',
    riskLevel: 'R1',
    async execute(params, permit, scopeEval) {
      const url = new URL(params.url);
      // Force HTTPS unless explicitly allowed HTTP for local testing
      if (url.protocol === 'http:' && !params.allowHttp) {
        url.protocol = 'https:';
      }

      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 10000);

      try {
        const res = await fetch(url.toString(), {
          method: 'HEAD',
          redirect: 'manual', // Never auto-follow
          signal: controller.signal,
          headers: { 'User-Agent': 'EdgeAideCyber/1.0 (authorized-testing)' },
        });

        const headers = {};
        res.headers.forEach((value, key) => {
          headers[key] = SENSITIVE_HEADERS.includes(key.toLowerCase()) ? '[REDACTED]' : value;
        });

        return {
          ok: true,
          data: {
            status: res.status,
            statusText: res.statusText,
            headers,
            redirected: false,
            url: url.toString(),
          },
        };
      } finally {
        clearTimeout(timeout);
      }
    },
  };
}

export function createUrlFetchAdapter(maxBodyBytes = 16384) {
  return {
    name: 'http.fetch',
    capability: 'web.recon',
    riskLevel: 'R2',
    async execute(params, permit, scopeEval) {
      const url = new URL(params.url);
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 15000);

      try {
        const res = await fetch(url.toString(), {
          method: 'GET',
          redirect: 'manual',
          signal: controller.signal,
        });

        const reader = res.body.getReader();
        let body = '';
        let bytesRead = 0;

        while (bytesRead < maxBodyBytes) {
          const { done, value } = await reader.read();
          if (done) break;
          const chunk = new TextDecoder().decode(value);
          body += chunk;
          bytesRead += value.length;
          if (bytesRead >= maxBodyBytes) break;
        }
        reader.cancel();

        return {
          ok: true,
          data: {
            status: res.status,
            contentType: res.headers.get('content-type'),
            bodyLength: bytesRead,
            truncated: bytesRead >= maxBodyBytes,
            body: body.slice(0, maxBodyBytes),
          },
        };
      } finally {
        clearTimeout(timeout);
      }
    },
  };
}
```

## Threat Matrix
| Threat | Impact | Mitigation |
|---|---|---|
| SSRF via URL pointing to internal services | Access internal network | Scope evaluator restricts targets; loopback blocked |
| Redirect chain crosses scope boundary | Unintended target accessed | `redirect: 'manual'` prevents auto-following |
| Response bomb (zip bomb, huge body) | Memory exhaustion | Stream reading with hard byte limit |
| Credential leak in response headers | Token exposure in evidence | Sensitive headers redacted before storage |
| TLS certificate not validated | MITM attack | Node.js validates by default; do NOT disable |

## Dependencies
- Node.js built-in `fetch` (Node >= 18)

## Pitfalls & Bugs
- `fetch` follows redirects by default; always set `redirect: 'manual'`.
- HEAD requests may not include all headers that GET would return.
- Some servers reject HEAD with 405; fall back to GET with immediate abort after headers.
- Response body streams may not respect the byte limit exactly due to chunk boundaries; truncate the string too.
- `AbortController.abort()` throws an `AbortError`; catch it specifically to distinguish from network errors.
