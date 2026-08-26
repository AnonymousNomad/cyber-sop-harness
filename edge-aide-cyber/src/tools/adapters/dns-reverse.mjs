import dns from 'node:dns/promises';

export function createDnsReverseAdapter() {
  return {
    name: 'dns.reverse',
    capability: 'network.recon',
    riskLevel: 'R1',
    async execute(params) {
      const target = String(params.target || '').trim();
      if (!target) return { ok: false, error: 'EMPTY_TARGET' };

      const records = {};
      const lookups = [
        ['a', () => dns.resolve4(target)],
        ['aaaa', () => dns.resolve6(target)],
        ['mx', () => dns.resolveMx(target)],
        ['txt', () => dns.resolveTxt(target)],
        ['ns', () => dns.resolveNs(target)],
      ];

      for (const [key, fn] of lookups) {
        try {
          records[key] = await fn();
        } catch {
          records[key] = [];
        }
      }

      return { ok: true, data: { target, records } };
    },
  };
}
