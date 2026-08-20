namespace CyberSopHarness.Core;

public interface IContainedWorker
{
    string WorkerRef { get; }
    ContainmentAttestation Containment { get; }
    Task<WorkerResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken);
    Task StopAsync(string reason);
    Task ForceStopAsync(string reason);
}

public sealed class FixtureWorker : IContainedWorker
{
    private readonly Func<ActionRequest, CancellationToken, Task<WorkerResult>> _handler;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _active = new();
    private bool _stopped;

    public FixtureWorker(string workerRef, ContainmentAuthority authority, Func<ActionRequest, CancellationToken, Task<WorkerResult>> handler)
    {
        WorkerRef = workerRef;
        _handler = handler;
        Containment = authority.IssueFixture(workerRef, Canonicalization.Sha256Hex("fixture-worker:" + workerRef));
    }

    public string WorkerRef { get; }
    public ContainmentAttestation Containment { get; }

    public async Task<WorkerResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();
        CancellationTokenSource linked;
        lock (_gate)
        {
            if (_stopped) throw new InvalidOperationException("worker is stopped");
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _active.Add(operationId, linked);
        }
        try { return await _handler(request, linked.Token); }
        finally
        {
            lock (_gate)
            {
                if (_active.Remove(operationId, out var source)) source.Dispose();
            }
        }
    }

    public Task StopAsync(string reason)
    {
        lock (_gate)
        {
            _stopped = true;
            foreach (var source in _active.Values) source.Cancel();
        }
        return Task.CompletedTask;
    }

    public Task ForceStopAsync(string reason) => StopAsync(reason);
}

public sealed class WorkerSupervisor
{
    private sealed class ActiveOperation
    {
        public required IContainedWorker Worker { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public required TaskCompletionSource<WorkerResult> Completion { get; init; }
        public required RateLease Lease { get; init; }
        public required string Target { get; init; }
    }

    private readonly RateLimiter _rateLimiter;
    private readonly CapabilityRegistry _capabilities;
    private readonly ContainmentAuthority _containmentAuthority;
    private readonly RollbackLedger _rollbackLedger;
    private readonly CredentialVault _credentialVault;
    private readonly PermitIssuer _permitIssuer;
    private readonly string _manifestHash;
    private readonly Dictionary<Guid, ActiveOperation> _active = new();
    private readonly object _gate = new();
    private bool _stopping;

    public WorkerSupervisor(AuthorizationManifest manifest, CapabilityRegistry capabilities, ContainmentAuthority containmentAuthority, RollbackLedger rollbackLedger, CredentialVault credentialVault, PermitIssuer permitIssuer)
    {
        if (!capabilities.IsFrozen) throw new InvalidOperationException("capability registry must be frozen before worker supervisor construction");
        _rateLimiter = new RateLimiter(manifest.RateLimits);
        _capabilities = capabilities;
        _containmentAuthority = containmentAuthority;
        _rollbackLedger = rollbackLedger;
        _credentialVault = credentialVault;
        _permitIssuer = permitIssuer;
        _manifestHash = Canonicalization.AuthorizationHash(manifest);
    }

    public async Task<WorkerResult> ExecuteAsync(Permit permit, ActionRequest request, AuthorizationManifest manifest, IContainedWorker worker, CancellationToken cancellationToken, ApprovalRecord? approval = null)
    {
        ArgumentNullException.ThrowIfNull(permit);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(worker);
        var now = AuthoritativeClock.UtcNow;
        lock (_gate)
        {
            if (_stopping) throw new InvalidOperationException("worker supervisor is stopping");
        }
        if (manifest.EngagementMode != EngagementMode.Fixture) throw new InvalidOperationException("Phase 2 has no trusted authorized worker provider; live/authorized dispatch is blocked");
        if (_manifestHash != Canonicalization.AuthorizationHash(manifest)) throw new InvalidOperationException("worker manifest does not match supervisor manifest");
        if (!_containmentAuthority.Verify(worker.Containment, worker.WorkerRef, manifest.EngagementMode)) throw new InvalidOperationException("worker containment attestation is invalid");
        if (!_capabilities.TryGet(request.CapabilityRef, out var capability) || capability is null) throw new InvalidOperationException("worker capability is not registered");
        if (!string.Equals(capability.RequiredPrivilege, worker.Containment.PrivilegeLevel, StringComparison.Ordinal)) throw new InvalidOperationException("worker privilege does not satisfy capability");
        var payloadBytes = ParsePayloadBytes(request);
        var canonicalTarget = ScopeEvaluator.CanonicalHost(request.TargetRef);
        if (!_rateLimiter.TryAcquire(canonicalTarget, payloadBytes, out var lease) || lease is null) throw new InvalidOperationException("rate or concurrency limit denied action");
        if (!_permitIssuer.TryConsume(permit, request, manifest, worker.WorkerRef, approval))
        {
            _rateLimiter.Release(lease);
            throw new InvalidOperationException("permit is invalid, expired, replayed, or not bound to current policy");
        }

        var operation = new ActiveOperation
        {
            Worker = worker,
            Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken),
            Completion = new TaskCompletionSource<WorkerResult>(TaskCreationOptions.RunContinuationsAsynchronously),
            Lease = lease,
            Target = canonicalTarget
        };
        var permitRemaining = permit.ExpiresAt - now;
        var executionWindow = permitRemaining < capability.MaxDuration ? permitRemaining : capability.MaxDuration;
        if (executionWindow <= TimeSpan.Zero)
        {
            _rateLimiter.Release(lease);
            throw new InvalidOperationException("permit has no remaining execution window");
        }
        operation.Cancellation.CancelAfter(executionWindow);
        var operationId = Guid.NewGuid();
        lock (_gate)
        {
            if (_stopping)
            {
                operation.Cancellation.Dispose();
                _rateLimiter.Release(lease);
                throw new InvalidOperationException("relay-loss stop began before worker dispatch");
            }
            _active.Add(operationId, operation);
        }
        _ = RunOperationAsync(operationId, operation, request, capability);
        return await operation.Completion.Task;
    }

    private async Task RunOperationAsync(Guid operationId, ActiveOperation operation, ActionRequest request, CapabilityManifest capability)
    {
        try
        {
            var result = await operation.Worker.ExecuteAsync(request, operation.Cancellation.Token);
            if (operation.Cancellation.IsCancellationRequested || IsStopping()) throw new OperationCanceledException("worker stopped before result acceptance");
            if (result.OutputBytes > capability.MaxOutputBytes) throw new InvalidOperationException("worker output exceeded capability limit");
            operation.Completion.TrySetResult(result);
        }
        catch (OperationCanceledException exception)
        {
            operation.Completion.TrySetException(exception);
        }
        catch (Exception exception)
        {
            operation.Completion.TrySetException(exception);
        }
        finally
        {
            lock (_gate) _active.Remove(operationId);
            _rateLimiter.Release(operation.Lease);
            operation.Cancellation.Dispose();
        }
    }

    public void BeginStopping()
    {
        lock (_gate) _stopping = true;
    }

    public async Task StopAllAsync(string reason, TimeSpan? timeout = null)
    {
        BeginStopping();
        var safetyTimeout = timeout ?? TimeSpan.FromSeconds(5);
        ActiveOperation[] operations;
        lock (_gate) operations = _active.Values.ToArray();
        foreach (var operation in operations)
        {
            try { operation.Cancellation.Cancel(); }
            catch { }
        }
        _permitIssuer.HandleRelayLoss();
        var gracefulStops = Task.WhenAll(operations.Select(operation => SafeStopAsync(operation.Worker, reason, false)));
        var completion = Task.WhenAll(operations.Select(operation => operation.Completion.Task));
        var gracefulAll = Task.WhenAll(gracefulStops, completion);
        var converged = await Task.WhenAny(gracefulAll, Task.Delay(safetyTimeout)) == gracefulAll && gracefulStops.IsCompletedSuccessfully && completion.IsCompleted;
        if (!converged)
        {
            var forcedStops = Task.WhenAll(operations.Select(operation => SafeStopAsync(operation.Worker, reason, true)));
            var forcedAndCompletion = Task.WhenAll(forcedStops, completion);
            converged = await Task.WhenAny(forcedAndCompletion, Task.Delay(safetyTimeout)) == forcedAndCompletion && forcedStops.IsCompletedSuccessfully && completion.IsCompleted;
        }
        RollbackReport? rollback = null;
        try
        {
            var rollbackTask = _rollbackLedger.ExecuteAsync();
            if (await Task.WhenAny(rollbackTask, Task.Delay(safetyTimeout)) == rollbackTask) rollback = await rollbackTask;
            else converged = false;
        }
        finally
        {
            _credentialVault.RevokeAll();
        }
        if (!converged) throw new TimeoutException("worker stop or cleanup did not converge within the safety timeout");
        if (rollback is not null && rollback.Failed.Count > 0) throw new InvalidOperationException("rollback failed for: " + string.Join(",", rollback.Failed));
    }

    public int ActiveCount
    {
        get { lock (_gate) return _active.Count; }
    }

    private bool IsStopping()
    {
        lock (_gate) return _stopping;
    }

    private static long ParsePayloadBytes(ActionRequest request)
    {
        if (!request.Arguments.TryGetValue("payload_bytes", out var text))
        {
            if (request.RiskClass != RiskClass.R0) throw new InvalidOperationException("active actions require measured payload_bytes");
            return 0;
        }
        if (!long.TryParse(text, out var value) || value < 0) throw new InvalidOperationException("payload_bytes must be a non-negative integer");
        return value;
    }

    private static async Task SafeStopAsync(IContainedWorker worker, string reason, bool force)
    {
        try
        {
            if (force) await worker.ForceStopAsync(reason);
            else await worker.StopAsync(reason);
        }
        catch { }
    }
}

public sealed class RelayLossController
{
    private readonly PermitIssuer _permitIssuer;
    private readonly WorkerSupervisor _supervisor;

    public RelayLossController(PermitIssuer permitIssuer, WorkerSupervisor supervisor)
    {
        _permitIssuer = permitIssuer;
        _supervisor = supervisor;
    }

    public async Task HandleAsync()
    {
        _supervisor.BeginStopping();
        _permitIssuer.HandleRelayLoss();
        await _supervisor.StopAllAsync("relay-loss");
    }
}
