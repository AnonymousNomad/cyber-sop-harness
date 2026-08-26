const MAX_OUTPUT_BYTES = 65536;

export function defineAdapter({ name, capability, riskLevel, execute, maxOutputBytes = MAX_OUTPUT_BYTES }) {
  if (!name || !capability || !riskLevel || typeof execute !== 'function') {
    throw new Error(`adapter "${name}" missing required fields`);
  }
  return Object.freeze({ name, capability, riskLevel, execute, maxOutputBytes });
}

export function createAdapterRegistry(adapters) {
  const map = new Map(adapters.map(a => [a.name, a]));
  const frozen = Object.freeze(map);

  return Object.freeze({
    get(name) { return frozen.get(name) || null; },
    has(name) { return frozen.has(name); },
    list() {
      return [...frozen.values()].map(a => ({
        name: a.name,
        capability: a.capability,
        riskLevel: a.riskLevel,
      }));
    },
    get size() { return frozen.size; },
  });
}
