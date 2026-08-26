export function createNotifier(wsProtocol) {
  function notifyClients(clients, type, payload) {
    for (const client of clients) {
      if (client.readyState === 1) {
        try {
          client.send(JSON.stringify({ type, payload, at: new Date().toISOString() }));
        } catch {}
      }
    }
  }

  function notifyErrorsDetected(clients, issues) {
    const summary = issues.map(i =>
      `  ${i.path}: ${i.errors.length} error(s) — ${i.errors.map(e => e.message).join(', ')}`
    ).join('\n');

    notifyClients(clients, 'autodebug.detected', {
      count: issues.length,
      summary,
      issues,
    });
  }

  function notifyFixResult(clients, result) {
    if (result.ok) {
      notifyClients(clients, 'autodebug.fixed', {
        file: result.filePath,
        errorsFixed: result.errorsFixed,
        message: `✓ Fixed ${result.errorsFixed} error(s) in ${result.filePath}`,
      });
    } else {
      notifyClients(clients, 'autodebug.fix_failed', {
        file: result.filePath,
        reason: result.reason,
        remainingErrors: result.remainingErrors,
        message: `✗ Could not fix ${result.filePath}: ${result.reason}`,
      });
    }
  }

  function notifyStatus(clients, status) {
    notifyClients(clients, 'autodebug.status', status);
  }

  return { notifyClients, notifyErrorsDetected, notifyFixResult, notifyStatus };
}
