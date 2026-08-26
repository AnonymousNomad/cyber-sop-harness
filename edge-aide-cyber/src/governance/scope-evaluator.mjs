import { isIPv4 } from 'node:net';

function ipToLong(ip) {
  return ip.split('.').reduce((acc, octet) => (acc << 8) + parseInt(octet, 10), 0) >>> 0;
}

export function cidrContains(cidr, ip) {
  if (!isIPv4(ip)) return false;
  const slashIdx = cidr.indexOf('/');
  if (slashIdx === -1) return false;

  const network = cidr.slice(0, slashIdx);
  const bitsStr = cidr.slice(slashIdx + 1);
  const prefixLen = parseInt(bitsStr, 10);
  if (isNaN(prefixLen) || prefixLen < 0 || prefixLen > 32) return false;
  if (!isIPv4(network)) return false;

  const mask = prefixLen === 0 ? 0 : ((0xFFFFFFFF << (32 - prefixLen)) >>> 0);
  return (ipToLong(ip) & mask) === (ipToLong(network) & mask);
}

export function domainMatches(pattern, hostname) {
  const p = pattern.toLowerCase().trim();
  const h = hostname.toLowerCase().trim();
  if (!p || !h) return false;
  if (p === h) return true;
  if (p.startsWith('*.')) {
    const suffix = p.slice(2);
    return h.endsWith('.' + suffix) && h.length > suffix.length + 1;
  }
  return false;
}

export function urlInScope(basePattern, testUrl) {
  try {
    const base = new URL(basePattern.startsWith('http') ? basePattern : `https://${basePattern}`);
    const test = new URL(testUrl.startsWith('http') ? testUrl : `https://${testUrl}`);

    if (!domainMatches(base.hostname, test.hostname)) return false;

    const basePath = base.pathname.replace(/\/+$/, '');
    const testPath = test.pathname;
    if (basePath === '' || basePath === '/') return true;

    return testPath.startsWith(basePath);
  } catch {
    return false;
  }
}

export function createScopeEvaluator(scopeRules) {
  if (!Array.isArray(scopeRules)) throw new Error('scope rules must be an array');

  function evaluate(target) {
    for (const rule of scopeRules) {
      switch (rule.type) {
        case 'cidr':
          if (cidrContains(rule.value, target)) return { allowed: true, matchedRule: rule };
          break;
        case 'domain':
          if (domainMatches(rule.value, target)) return { allowed: true, matchedRule: rule };
          break;
        case 'url_prefix':
          if (urlInScope(rule.value, target)) return { allowed: true, matchedRule: rule };
          break;
        case 'ip':
          if (rule.value === target) return { allowed: true, matchedRule: rule };
          break;
      }
    }
    return { allowed: false, reason: `no scope rule matches "${target}"` };
  }

  evaluate.getRules = () => Object.freeze([...scopeRules]);
  return evaluate;
}
