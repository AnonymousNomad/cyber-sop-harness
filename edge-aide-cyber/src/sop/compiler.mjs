/**
 * SOP Compiler — validates and indexes Standard Operating Procedures.
 *
 * SOPs are JSON DAGs describing security testing methodology steps.
 * The compiler:
 *   1. Validates structure (required fields, step IDs unique)
 *   2. Validates DAG: no cycles, all dependsOn references exist
 *   3. Validates tool references against the adapter registry
 *   4. Topological sort for execution order
 *   5. Indexes by ID for lookup
 */

export class SOPCompiler {
  #registry;
  #sops = new Map();

  constructor(toolRegistry) {
    this.#registry = toolRegistry;
  }

  /**
   * Load and validate an SOP definition.
   * @param {object} sop - { id, name, steps: [{ id, name, tool, riskLevel, params, dependsOn, approvalRequired }] }
   * @returns {{ ok: boolean, errors?: string[] }}
   */
  load(sop) {
    const errors = this.validate(sop);
    if (errors.length > 0) {
      return { ok: false, errors };
    }
    this.#sops.set(sop.id, sop);
    return { ok: true };
  }

  /**
   * Validate an SOP definition without loading it.
   * @returns {string[]} Array of error messages (empty = valid)
   */
  validate(sop) {
    const errors = [];

    if (!sop || typeof sop !== 'object') {
      return ['SOP must be a non-null object'];
    }
    if (!sop.id || typeof sop.id !== 'string') {
      errors.push('SOP must have a string "id"');
    }
    if (!sop.name || typeof sop.name !== 'string') {
      errors.push('SOP must have a string "name"');
    }
    if (!Array.isArray(sop.steps)) {
      errors.push('SOP must have a "steps" array');
      return errors;
    }
    if (sop.steps.length === 0) {
      errors.push('SOP must have at least one step');
      return errors;
    }

    const stepIds = new Set();
    for (let i = 0; i < sop.steps.length; i++) {
      const step = sop.steps[i];
      const prefix = `step[${i}]`;

      if (!step.id || typeof step.id !== 'string') {
        errors.push(`${prefix}: must have a string "id"`);
        continue;
      }
      if (stepIds.has(step.id)) {
        errors.push(`${prefix}: duplicate step id "${step.id}"`);
      }
      stepIds.add(step.id);

      if (!step.name || typeof step.name !== 'string') {
        errors.push(`${prefix}: must have a string "name"`);
      }

      if (step.tool && typeof step.tool !== 'string') {
        errors.push(`${prefix}: "tool" must be a string`);
      }

      if (step.tool && this.#registry && !this.#registry.has(step.tool)) {
        errors.push(`${prefix}: tool "${step.tool}" not in adapter registry`);
      }

      if (step.riskLevel && !/^R[0-4]$/.test(step.riskLevel)) {
        errors.push(`${prefix}: riskLevel must be R0-R4, got "${step.riskLevel}"`);
      }

      if (step.dependsOn && !Array.isArray(step.dependsOn)) {
        errors.push(`${prefix}: "dependsOn" must be an array`);
      }
    }

    // Validate dependsOn references
    for (let i = 0; i < sop.steps.length; i++) {
      const step = sop.steps[i];
      if (!step.dependsOn) continue;
      for (const dep of step.dependsOn) {
        if (!stepIds.has(dep)) {
          errors.push(`step[${i}] (${step.id}): dependsOn "${dep}" not found in steps`);
        }
      }
    }

    // Cycle detection via DFS
    if (errors.length === 0) {
      const cycle = this.#detectCycle(sop.steps);
      if (cycle) {
        errors.push(`cycle detected: ${cycle.join(' → ')}`);
      }
    }

    return errors;
  }

  /**
   * Get an SOP by ID.
   */
  get(id) {
    return this.#sops.get(id) || null;
  }

  /**
   * List all loaded SOPs (summary).
   */
  list() {
    return Array.from(this.#sops.values()).map(s => ({
      id: s.id,
      name: s.name,
      steps: s.steps,
    }));
  }

  /**
   * Get topological sort order for an SOP's steps.
   * @returns {string[]} Step IDs in execution order
   */
  topoSort(sopId) {
    const sop = this.#sops.get(sopId);
    if (!sop) return [];

    const stepMap = new Map(sop.steps.map(s => [s.id, s]));
    const visited = new Set();
    const order = [];

    const visit = (stepId) => {
      if (visited.has(stepId)) return;
      visited.add(stepId);
      const step = stepMap.get(stepId);
      if (step?.dependsOn) {
        for (const dep of step.dependsOn) {
          visit(dep);
        }
      }
      order.push(stepId);
    };

    for (const step of sop.steps) {
      visit(step.id);
    }

    return order;
  }

  /**
   * Check if an SOP is currently loaded (locked during active engagement).
   */
  has(id) {
    return this.#sops.has(id);
  }

  /**
   * Detect cycle in step dependency graph via DFS.
   * @returns {string[]|null} Cycle path or null if no cycle
   */
  #detectCycle(steps) {
    const stepMap = new Map(steps.map(s => [s.id, s]));
    const WHITE = 0, GRAY = 1, BLACK = 2;
    const color = new Map(steps.map(s => [s.id, WHITE]));
    const parent = new Map();

    const dfs = (nodeId) => {
      color.set(nodeId, GRAY);
      const step = stepMap.get(nodeId);
      if (step?.dependsOn) {
        for (const dep of step.dependsOn) {
          if (!stepMap.has(dep)) continue;
          if (color.get(dep) === GRAY) {
            // Reconstruct cycle
            const cycle = [dep, nodeId];
            let cur = nodeId;
            while (cur !== dep) {
              cur = parent.get(cur);
              if (cur === undefined) break;
              cycle.unshift(cur);
            }
            return cycle;
          }
          if (color.get(dep) === WHITE) {
            parent.set(dep, nodeId);
            const result = dfs(dep);
            if (result) return result;
          }
        }
      }
      color.set(nodeId, BLACK);
      return null;
    };

    for (const step of steps) {
      if (color.get(step.id) === WHITE) {
        const result = dfs(step.id);
        if (result) return result;
      }
    }
    return null;
  }
}
