import path from 'node:path';

const MAX_ENTRIES = 10000;
const MAX_FILE_BYTES = 5 * 1024 * 1024;

export function createCipherBus(fileBoundary) {
  const stateFile = '.edge-cyber/cipher-state.jsonl';
  let sequence = 0;

  async function append(event) {
    if (!event || typeof event !== 'object') return;
    sequence += 1;
    const entry = { ...event, seq: sequence, at: new Date().toISOString() };
    const line = JSON.stringify(entry) + '\n';
    await fileBoundary.appendFile(stateFile, line);
  }

  async function query({ type, since, limit = 100 } = {}) {
    let raw;
    try {
      raw = await fileBoundary.readFile(stateFile);
    } catch {
      return [];
    }

    const lines = raw.split('\n').filter(Boolean);
    const entries = [];

    for (const line of lines) {
      try {
        const entry = JSON.parse(line);
        if (type && entry.type !== type) continue;
        if (since && entry.at < since) continue;
        entries.push(entry);
      } catch {
        continue;
      }
    }

    return entries.slice(-limit).reverse();
  }

  async function getLearnedPreferences(minCount = 3, limit = 15) {
    const approvals = await query({ type: 'approval', limit: 500 });
    const rejections = await query({ type: 'rejection', limit: 500 });
    const patterns = {};

    for (const entry of [...approvals, ...rejections]) {
      if (!entry.pattern || !entry.decision) continue;
      if (!patterns[entry.pattern]) patterns[entry.pattern] = { count: 0, approved: 0 };
      patterns[entry.pattern].count += 1;
      if (entry.decision === 'approve') patterns[entry.pattern].approved += 1;
    }

    return Object.entries(patterns)
      .filter(([, stats]) => stats.count >= minCount && stats.approved / stats.count >= 0.6)
      .sort((a, b) => b[1].approved - a[1].approved)
      .slice(0, limit)
      .map(([pattern]) => `[learned] ${pattern}`);
  }

  async function rotateIfNeeded() {
    try {
      const stat = await fileBoundary.stat(stateFile);
      if (stat.size > MAX_FILE_BYTES) {
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        const archiveName = `.edge-cyber/cipher-state-${timestamp}.jsonl`;
        const content = await fileBoundary.readFile(stateFile);
        await fileBoundary.writeFile(archiveName, content);
        await fileBoundary.writeFile(stateFile, '');
        sequence = 0;
      }
    } catch {}
  }

  async function init() {
    await fileBoundary.mkdir('.edge-cyber');
    await rotateIfNeeded();
    const entries = await query({ limit: 1 });
    if (entries.length > 0) sequence = entries[0].seq || 0;
  }

  return { append, query, getLearnedPreferences, init };
}
