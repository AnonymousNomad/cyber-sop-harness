/**
 * Coverage Ledger — tracks SOP step completion for engagements.
 *
 * Provides:
 *   - Per-SOP step status tracking (pending/running/completed/failed/skipped)
 *   - Completion percentage calculation
 *   - Evidence chain references for each step
 *   - Export as markdown or JSON report
 *   - Integrity: each entry includes a timestamp and optional evidence hash
 */

export class CoverageLedger {
  #cipher;
  #ledgers = new Map(); // sopId → { steps: Map<stepId, StepRecord>, startedAt, completedAt }

  constructor(cipherStateBus) {
    this.#cipher = cipherStateBus;
  }

  /**
   * Start tracking an SOP. Returns the active SOP reference.
   * @param {object} sop - The SOP definition
   * @returns {{ sop, currentStepIndex: number, currentStep: string|null, startedAt: string }}
   */
  startSOP(sop) {
    if (this.#ledgers.has(sop.id)) {
      // Resume existing ledger
      return { sop, currentStepIndex: 0, currentStep: null, startedAt: this.#ledgers.get(sop.id).startedAt };
    }

    const ledger = {
      steps: new Map(),
      startedAt: new Date().toISOString(),
      completedAt: null,
      totalSteps: sop.steps.length,
    };
    // Pre-register all steps as pending
    for (const step of sop.steps) {
      ledger.steps.set(step.id, {
        status: 'pending',
        timestamp: null,
        evidenceHash: null,
        error: null,
        previousStatus: null,
      });
    }
    this.#ledgers.set(sop.id, ledger);

    return { sop, currentStepIndex: 0, currentStep: null, startedAt: ledger.startedAt };
  }

  /**
   * Record the status of a step.
   * @param {string} sopId
   * @param {string} stepId
   * @param {'running'|'completed'|'failed'|'skipped'} status
   * @param {object} [meta] - Optional metadata (evidenceHash, error, etc.)
   */
  recordStep(sopId, stepId, status, meta = {}) {
    const ledger = this.#ledgers.get(sopId);
    if (!ledger) return;

    const existing = ledger.steps.get(stepId);
    const record = {
      status,
      timestamp: new Date().toISOString(),
      evidenceHash: meta.evidenceHash || null,
      error: meta.error || null,
      previousStatus: existing?.status || 'pending',
    };

    ledger.steps.set(stepId, record);

    // Append to cipher state for persistence
    if (this.#cipher) {
      this.#cipher.append({
        type: 'sop.step',
        sopId,
        stepId,
        status,
        timestamp: record.timestamp,
      }).catch(() => {});
    }
  }

  /**
   * Get the status of all steps for an SOP.
   * @returns {{ stepStatus: object, completed: number, total: number, percentage: number, steps: object[] }}
   */
  getStatus(sopId) {
    const ledger = this.#ledgers.get(sopId);
    if (!ledger) {
      return { stepStatus: {}, completed: 0, total: 0, percentage: 0, steps: [] };
    }

    const stepStatus = {};
    let completed = 0;
    let total = 0;
    const steps = [];

    for (const [stepId, record] of ledger.steps) {
      stepStatus[stepId] = record.status;
      if (record.status === 'completed' || record.status === 'skipped') completed++;
      total++;
      steps.push({ id: stepId, ...record });
    }

    const percentage = total > 0 ? Math.round((completed / total) * 100) : 0;

    return { stepStatus, completed, total, percentage, steps };
  }

  /**
   * Export a report for an SOP engagement.
   * @param {string} sopId
   * @param {'markdown'|'json'} format
   * @returns {string}
   */
  export(sopId, format = 'markdown') {
    const status = this.getStatus(sopId);
    const ledger = this.#ledgers.get(sopId);

    if (format === 'json') {
      return JSON.stringify({
        sopId,
        startedAt: ledger?.startedAt,
        completedAt: ledger?.completedAt,
        percentage: status.percentage,
        steps: status.steps,
      }, null, 2);
    }

    // Markdown report
    const lines = [
      `# SOP Coverage Report: ${sopId}`,
      '',
      `- Started: ${ledger?.startedAt || 'unknown'}`,
      `- Completed: ${ledger?.completedAt || 'in progress'}`,
      `- Coverage: ${status.percentage}% (${status.completed}/${status.total})`,
      '',
      '## Steps',
      '',
    ];

    for (const step of status.steps) {
      const icon = step.status === 'completed' ? '✅' :
                   step.status === 'failed' ? '❌' :
                   step.status === 'skipped' ? '⏭️' :
                   step.status === 'running' ? '🔄' : '⬜';
      lines.push(`${icon} **${step.id}** — ${step.status} (${step.timestamp})`);
      if (step.error) lines.push(`  - Error: ${step.error}`);
      if (step.evidenceHash) lines.push(`  - Evidence: \`${step.evidenceHash}\``);
    }

    return lines.join('\n');
  }

  /**
   * Check if all steps are completed/failed/skipped (no pending/running).
   */
  isComplete(sopId) {
    const ledger = this.#ledgers.get(sopId);
    if (!ledger) return false;
    for (const record of ledger.steps.values()) {
      if (record.status === 'pending' || record.status === 'running') return false;
    }
    return ledger.steps.size > 0;
  }

  /**
   * Mark an SOP as completed.
   */
  completeSOP(sopId) {
    const ledger = this.#ledgers.get(sopId);
    if (ledger) {
      ledger.completedAt = new Date().toISOString();
    }
  }
}
