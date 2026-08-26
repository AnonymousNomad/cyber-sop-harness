export function createAutoFixer({ modelProvider, fileBoundary, checkFile, evidenceChain }) {
  const pendingFixes = new Map();
  let autoMode = false;

  async function analyze(fileChanges) {
    const results = [];
    for (const change of fileChanges) {
      const check = await checkFile(fileBoundary.root, change.path);
      if (!check.clean && check.errors.length > 0) {
        results.push({
          path: change.path,
          changeType: change.type,
          errors: check.errors,
          severity: Math.max(...check.errors.map(e => e.severity === 'error' ? 2 : 1)),
        });
      }
    }
    return results;
  }

  async function buildFixPrompt(filePath, errors) {
    let source;
    try {
      source = await fileBoundary.readFile(filePath);
    } catch {
      return null;
    }

    const errorLines = errors.map(e =>
      `Line ${e.line || '?'}: ${e.message}`
    ).join('\n');

    const prompt = [
      `Fix the following errors in ${filePath}:`,
      '',
      errorLines,
      '',
      'File contents:',
      '```',
      source,
      '```',
      '',
      'Return ONLY a valid file with the fixes applied. No explanation needed.',
      'Do not change anything unrelated to the errors.',
    ].join('\n');

    return prompt;
  }

  async function attemptFix(filePath, errors) {
    if (!modelProvider.isReady) {
      return { ok: false, reason: 'model not available', filePath };
    }

    const prompt = await buildFixPrompt(filePath, errors);
    if (!prompt) {
      return { ok: false, reason: 'could not read file', filePath };
    }

    try {
      const fixedSource = await modelProvider.complete([
        { role: 'system', content: 'You are a code fixer. Return only valid code, no markdown fences, no explanation.' },
        { role: 'user', content: prompt },
      ], { maxTokens: 4096, temperature: 0.1 });

      const cleaned = fixedSource
        .replace(/^```(?:js|javascript|mjs|ts|json)?\n?/i, '')
        .replace(/\n?```\s*$/i, '')
        .trim();

      await fileBoundary.writeFile(filePath, cleaned);

      const recheck = await checkFile(fileBoundary.root, filePath);

      if (evidenceChain) {
        await evidenceChain.append('autodebug.fix', {
          file: filePath,
          errorsBefore: errors.length,
          errorsAfter: recheck.errors.length,
          fixed: recheck.clean,
        });
      }

      if (recheck.clean) {
        return { ok: true, filePath, errorsFixed: errors.length };
      } else {
        const original = await fileBoundary.readFile(filePath).catch(() => null);
        return { ok: false, filePath, remainingErrors: recheck.errors, reason: 'fix incomplete' };
      }
    } catch (err) {
      return { ok: false, filePath, reason: err.message };
    }
  }

  function registerPending(filePath, errors) {
    pendingFixes.set(filePath, { filePath, errors, timestamp: Date.now() });
  }

  function resolvePending(filePath) {
    pendingFixes.delete(filePath);
  }

  return {
    analyze,
    attemptFix,
    registerPending,
    resolvePending,
    get pendingCount() { return pendingFixes.size; },
    get pending() { return [...pendingFixes.values()]; },
    get autoMode() { return autoMode; },
    set autoMode(val) { autoMode = Boolean(val); },
  };
}
