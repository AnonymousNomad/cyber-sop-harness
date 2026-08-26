const SENSITIVE_HEADERS = new Set([
  'authorization', 'cookie', 'set-cookie',
  'x-api-key', 'x-auth-token', 'proxy-authorization',
]);

const REQUEST_TIMEOUT_MS = 10000;
const MAX_REDIRECTS = 0;

export function createHttpHeadersAdapter() {
  return {
    name: 'http.headers',
    capability: 'web.recon',
    riskLevel: 'R1',
    async execute(params) {
      let url = String(params.url || '').trim();
      if (!url) return { ok: false, error: 'EMPTY_URL' };

      try {
        const parsed = new URL(url.startsWith('http') ? url : `https://${url}`);
        url = parsed.toString();
      } catch {
        return { ok: false, error: 'INVALID_URL' };
      }

      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

      try {
        const res = await fetch(url, {
          method: 'HEAD',
          redirect: 'manual',
          signal: controller.signal,
          headers: { 'User-Agent': 'EdgeAideCyber/0.1 (authorized-testing)' },
        });

        const headers = {};
        res.headers.forEach((value, key) => {
          headers[key] = SENSITIVE_HEADERS.has(key.toLowerCase()) ? '[REDACTED]' : value;
        });

        return {
          ok: true,
          data: {
            status: res.status,
            statusText: res.statusText,
            headers,
            url,
          },
        };
      } catch (err) {
        if (err.name === 'AbortError') return { ok: false, error: 'TIMEOUT' };
        return { ok: false, error: 'FETCH_FAILED', detail: err.message };
      } finally {
        clearTimeout(timer);
      }
    },
  };
}
