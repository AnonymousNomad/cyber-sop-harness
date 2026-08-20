namespace CyberSopHarness.Core;

public sealed class RollbackLedger
{
    private readonly List<(string Id, Func<Task> Action)> _actions = new();
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _running = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    public void Register(string id, Func<Task> rollback)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("rollback id is required", nameof(id));
        if (rollback is null) throw new ArgumentNullException(nameof(rollback));
        lock (_gate)
        {
            if (_actions.Any(item => item.Id == id)) throw new InvalidOperationException("rollback id is already registered");
            _actions.Add((id, rollback));
        }
    }

    public async Task<RollbackReport> ExecuteAsync()
    {
        await _executionGate.WaitAsync();
        try
        {
            var completed = new List<string>();
            var failed = new List<string>();
            (string Id, Func<Task> Action)[] actions;
            lock (_gate) actions = _actions.ToArray();
            foreach (var item in Enumerable.Reverse(actions))
            {
                lock (_gate)
                {
                    if (_completed.Contains(item.Id) || !_running.Add(item.Id)) continue;
                }
                try
                {
                    await item.Action();
                    lock (_gate)
                    {
                        _running.Remove(item.Id);
                        _completed.Add(item.Id);
                    }
                    completed.Add(item.Id);
                }
                catch
                {
                    lock (_gate) _running.Remove(item.Id);
                    failed.Add(item.Id);
                }
            }
            return new RollbackReport(completed, failed);
        }
        finally
        {
            _executionGate.Release();
        }
    }
}
