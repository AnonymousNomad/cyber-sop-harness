using System.Text;
using System.Text.Json;

namespace CyberSopHarness.Core;

public sealed class OutputRedactor
{
    private readonly string[] _secrets;

    public OutputRedactor(IEnumerable<string>? secrets = null)
    {
        _secrets = (secrets ?? Array.Empty<string>()).Where(secret => !string.IsNullOrEmpty(secret)).Distinct(StringComparer.Ordinal).ToArray();
    }

    public byte[] Redact(byte[] raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var value = Encoding.UTF8.GetString(raw);
        foreach (var secret in _secrets) value = value.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(value);
    }
}

public sealed record ToolExecutionOutcome(bool Dispatched, EvidenceEvent Evidence, string? FailureReason, ProvenanceStamp Provenance);

public sealed class ToolBroker
{
    private readonly ToolRegistry _registry;
    private readonly EvidenceLedger _evidence;
    private readonly PermitIssuer _permitIssuer;
    private readonly ProvenanceAuthority _provenance;
    private readonly OutputRedactor _redactor;

    public ToolBroker(ToolRegistry registry, EvidenceLedger evidence, PermitIssuer permitIssuer, ProvenanceAuthority provenance, OutputRedactor? redactor = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _permitIssuer = permitIssuer ?? throw new ArgumentNullException(nameof(permitIssuer));
        _provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        _redactor = redactor ?? new OutputRedactor();
    }

    public async Task<ToolExecutionOutcome> ExecuteAsync(ActionEnvelope envelope, AuthorizationManifest manifest, PolicyResult policy, Permit? permit, string workerRef, ApprovalRecord? approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Request);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerRef);
        var started = AuthoritativeClock.UtcNow;
        var actionValidation = ActionRequestValidator.Validate(envelope.Request);
        if (!actionValidation.IsValid) throw new InvalidOperationException("action envelope is malformed: " + string.Join("; ", actionValidation.Errors));
        var envelopeValidation = ActionEnvelopeValidator.Validate(envelope);
        if (!envelopeValidation.IsValid) return Blocked(envelope, manifest, policy, workerRef, "action envelope metadata is invalid", started);
        if (policy.Decision != PolicyDecision.Allow) return Blocked(envelope, manifest, policy, workerRef, "policy did not allow tool dispatch", started);
        if (permit is null) return Blocked(envelope, manifest, policy, workerRef, "tool dispatch requires a permit", started);
        if (!_registry.IsFrozen) return Blocked(envelope, manifest, policy, workerRef, "tool registry must be frozen before dispatch", started);
        if (policy.ActionHash != envelope.ActionHash || policy.ManifestHash != Canonicalization.AuthorizationHash(manifest) || policy.AuthorizationRef != envelope.Request.AuthorizationRef || policy.CapabilityRef != envelope.Request.CapabilityRef) return Blocked(envelope, manifest, policy, workerRef, "policy is not bound to the current action envelope", started);
        if (!_registry.TryGet(envelope.Request.CapabilityRef, out var registration) || registration is null) return Blocked(envelope, manifest, policy, workerRef, "tool capability is unknown", started);
        if (manifest.EngagementMode == EngagementMode.Authorized)
        {
            if (registration.Adapter is not IContainedNetworkToolAdapter)
                return Blocked(envelope, manifest, policy, workerRef, "authorized dispatch requires a contained network tool", started);
            if (!NetworkToolGuard.IsTargetAllowed(envelope.Request.TargetRef, registration.Manifest.NetworkDestinations))
                return Blocked(envelope, manifest, policy, workerRef, "target origin is outside the tool allowlist", started);
        }
        else if (registration.Adapter is not ILocalFixtureToolAdapter)
        {
            return Blocked(envelope, manifest, policy, workerRef, "fixture mode permits only fixture tools", started);
        }
        if (!_permitIssuer.TryClaimConsumed(permit, envelope.Request, manifest, workerRef, policy, approval)) return Blocked(envelope, manifest, policy, workerRef, "permit is expired, invalid, replayed, or not bound to the current tool dispatch", started);
        var context = new ToolExecutionContext(envelope, manifest, policy, permit, registration.Manifest, workerRef);
        var providerMetadata = new ProviderExecutionMetadata(envelope.Provider, envelope.ProviderOutputSha256, envelope.ProviderLatency, envelope.ProviderTokenUsage, envelope.ProviderFailureClass);
        ToolAdapterResult result;
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        executionCancellation.CancelAfter(registration.Manifest.MaxDuration);
        try
        {
            var adapterTask = registration.Adapter.ExecuteAsync(context, executionCancellation.Token);
            var timeoutTask = Task.Delay(registration.Manifest.MaxDuration);
            var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            var completed = await Task.WhenAny(adapterTask, timeoutTask, cancellationTask);
            if (completed == timeoutTask)
            {
                executionCancellation.Cancel();
                result = new ToolAdapterResult(ToolResultStatus.Timeout, null, Array.Empty<byte>(), Array.Empty<string>(), Array.Empty<string>(), "FAILED");
            }
            else if (completed == cancellationTask)
            {
                executionCancellation.Cancel();
                result = new ToolAdapterResult(ToolResultStatus.Unknown, null, Array.Empty<byte>(), Array.Empty<string>(), Array.Empty<string>(), "FAILED");
            }
            else
            {
                result = await adapterTask;
            }
        }
        catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            result = new ToolAdapterResult(ToolResultStatus.Timeout, null, Array.Empty<byte>(), Array.Empty<string>(), Array.Empty<string>(), "FAILED");
        }
        catch (OperationCanceledException)
        {
            result = new ToolAdapterResult(ToolResultStatus.Unknown, null, Array.Empty<byte>(), Array.Empty<string>(), Array.Empty<string>(), "FAILED");
        }
        catch (Exception)
        {
            result = new ToolAdapterResult(ToolResultStatus.ToolError, null, Array.Empty<byte>(), Array.Empty<string>(), Array.Empty<string>(), "FAILED");
        }
        if (result is null) result = new ToolAdapterResult(ToolResultStatus.ToolError, null, Array.Empty<byte>(), Array.Empty<string>(), Array.Empty<string>(), "FAILED");
        if (executionCancellation.IsCancellationRequested && (result.Status is ToolResultStatus.Success or ToolResultStatus.Partial)) result = result with { Status = cancellationToken.IsCancellationRequested ? ToolResultStatus.Unknown : ToolResultStatus.Timeout, CleanupResult = "FAILED" };
        if (!Enum.IsDefined(result.Status) || result.Status == ToolResultStatus.Blocked || result.RawOutput is null || result.RawOutput.Length > registration.Manifest.MaxOutputBytes || result.ObservationRefs is null || result.ArtifactRefs is null) result = result with { Status = ToolResultStatus.ToolError, RawOutput = Array.Empty<byte>(), ObservationRefs = Array.Empty<string>(), ArtifactRefs = Array.Empty<string>(), CleanupResult = "FAILED" };
        if ((result.Status == ToolResultStatus.Success && (result.ExitCode is null || result.ExitCode != 0)) || (result.Status == ToolResultStatus.Partial && result.ExitCode is not null && result.ExitCode != 0)) result = result with { Status = ToolResultStatus.ToolError, CleanupResult = "FAILED" };
        if (result.ArtifactRefs.Any(reference => string.IsNullOrWhiteSpace(reference) || !_evidence.HasArtifact(reference))) result = result with { Status = ToolResultStatus.ToolError, ArtifactRefs = Array.Empty<string>(), CleanupResult = "FAILED" };
        if (result.ObservationRefs.Any(string.IsNullOrWhiteSpace)) result = result with { Status = ToolResultStatus.ToolError, ObservationRefs = Array.Empty<string>(), CleanupResult = "FAILED" };
        if (result.ArtifactRefs.Count > 0) result = result with { Status = ToolResultStatus.ToolError, ArtifactRefs = Array.Empty<string>(), CleanupResult = "FAILED" };
        if (result.CleanupResult is not ("PENDING" or "SUCCEEDED" or "FAILED" or "NOT_APPLICABLE")) result = result with { CleanupResult = "FAILED" };
        try
        {
            using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var cleanupTask = registration.Adapter.CleanupAsync(context, result, cleanupCancellation.Token);
            if (await Task.WhenAny(cleanupTask, Task.Delay(TimeSpan.FromSeconds(1))) != cleanupTask) result = result with { CleanupResult = "FAILED" };
            else
            {
                var receipt = await cleanupTask;
                result = result with { CleanupResult = receipt == "CLEANUP_OK|" + envelope.ActionHash ? "SUCCEEDED" : "FAILED" };
            }
        }
        catch
        {
            result = result with { CleanupResult = "FAILED" };
        }
        if (registration.Manifest.CleanupRequired && result.CleanupResult != "SUCCEEDED") result = result with { CleanupResult = "FAILED" };
        var finished = AuthoritativeClock.UtcNow;
        var redacted = _redactor.Redact(result.RawOutput);
        var evidence = _evidence.Append(new EvidenceEventDraft(
            envelope.Request.RunId,
            envelope.Request.ActionId,
            envelope.ActionHash,
            string.Empty,
            providerMetadata,
            result.Status,
            registration.Manifest.ToolRef,
            registration.Manifest.ToolVersion,
            workerRef,
            envelope.Request.TargetRef,
            envelope.Request.AuthorizationRef,
            envelope.Request.ScopeRef,
            envelope.Request.CapabilityRef,
            envelope.Request.RiskClass,
            envelope.Request.MethodologyRefs,
            policy.Decision,
            policy.PolicyRef,
            policy.PolicyVersion,
            permit.PermitId,
            result.RawOutput,
            redacted,
            result.ArtifactRefs,
            result.ObservationRefs,
            envelope.Request.ParentEventId,
            envelope.Request.ApprovalRef,
            started,
            finished,
            finished,
            result.ExitCode,
            result.CleanupResult));
        return new(true, evidence, null, _provenance.Issue(evidence, manifest));
    }

    private ToolExecutionOutcome Blocked(ActionEnvelope envelope, AuthorizationManifest manifest, PolicyResult policy, string workerRef, string reason, DateTimeOffset started)
    {
        var now = AuthoritativeClock.UtcNow;
        var evidence = _evidence.Append(new EvidenceEventDraft(
            envelope.Request.RunId,
            envelope.Request.ActionId,
            envelope.ActionHash,
            reason,
            SafeProviderMetadata(envelope),
            ToolResultStatus.Blocked,
            "blocked",
            "none",
            workerRef,
            envelope.Request.TargetRef,
            envelope.Request.AuthorizationRef,
            envelope.Request.ScopeRef,
            envelope.Request.CapabilityRef,
            envelope.Request.RiskClass,
            envelope.Request.MethodologyRefs,
            PolicyDecision.Block,
            policy.PolicyRef,
            policy.PolicyVersion,
            null,
            Array.Empty<byte>(),
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            envelope.Request.ParentEventId,
            envelope.Request.ApprovalRef,
            started,
            now,
            now,
            null,
            "NOT_APPLICABLE"));
        return new(false, evidence, reason, _provenance.Issue(evidence, manifest));
    }

    private static ProviderExecutionMetadata SafeProviderMetadata(ActionEnvelope envelope)
    {
        var descriptor = envelope.Provider ?? new ProviderDescriptor("invalid", "invalid", "invalid", new string('0', 64), "invalid", "invalid", "invalid");
        var outputHash = envelope.ProviderOutputSha256 is not null && envelope.ProviderOutputSha256.Length == 64 ? envelope.ProviderOutputSha256 : new string('0', 64);
        return new ProviderExecutionMetadata(descriptor, outputHash, envelope.ProviderLatency < TimeSpan.Zero ? TimeSpan.Zero : envelope.ProviderLatency, Math.Max(0, envelope.ProviderTokenUsage), envelope.ProviderFailureClass);
    }
}

public sealed class WorkflowRun
{
    public WorkflowRun(string runId, string actionId, string actionHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionHash);
        RunId = runId;
        ActionId = actionId;
        ActionHash = actionHash;
    }

    public string RunId { get; }
    public string ActionId { get; }
    public string ActionHash { get; }
    public WorkflowState State { get; internal set; } = WorkflowState.Ready;
    public string? LastEvidenceEventId { get; internal set; }
    public string? LastAuditEventId { get; internal set; }
}

public sealed record StateTransitionResult(bool Allowed, WorkflowState State, string Reason);

public sealed record VerificationRecord(
    string VerificationEventId,
    string ResultEventId,
    string VerifierRef,
    bool Passed,
    string ObservationRef,
    string Notes,
    DateTimeOffset VerifiedAt);

public sealed record ReportDecision(
    string ReportEventId,
    string FindingRef,
    string EvidenceEventId,
    string VerificationEventId,
    bool Allowed,
    string Reason,
    DateTimeOffset DecidedAt);

public sealed class WorkflowStateMachine
{
    private readonly EvidenceLedger _evidence;
    private readonly WorkflowAuditLog _audit;

    public WorkflowStateMachine(EvidenceLedger evidence, WorkflowAuditLog audit)
    {
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public StateTransitionResult Transition(WorkflowRun run, WorkflowState target, string? evidenceEventId = null, VerificationRecord? verification = null, ReportDecision? report = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!_evidence.VerifyIntegrity() || !_audit.VerifyIntegrity()) return Freeze(run, "evidence or workflow audit integrity failed");
        if (!CanTransition(run.State, target)) return new(false, run.State, $"transition {run.State}->{target} is not allowed");
        EvidenceEvent? evidence = null;
        if (evidenceEventId is not null && !_evidence.TryGet(evidenceEventId, out evidence)) return new(false, run.State, "required evidence event is missing");
        if (target == WorkflowState.Allowed && (evidence is null || !MatchesRun(run, evidence) || evidence.PolicyDecision != PolicyDecision.Allow || string.IsNullOrWhiteSpace(evidence.PermitRef))) return new(false, run.State, "ALLOWED requires an allow policy event and permit");
        if (target == WorkflowState.Running && (evidence is null || !MatchesRun(run, evidence) || string.IsNullOrWhiteSpace(evidence.PermitRef))) return new(false, run.State, "RUNNING requires a permit event");
        if (target == WorkflowState.Observed && (evidence is null || !MatchesRun(run, evidence) || evidence.Type != "TOOL_RESULT" || evidence.Status is ToolResultStatus.Timeout or ToolResultStatus.ToolError or ToolResultStatus.Unknown or ToolResultStatus.Blocked || evidence.CleanupResult is not ("SUCCEEDED" or "NOT_APPLICABLE"))) return new(false, run.State, "OBSERVED requires a successful or partial current action result event");
        if (target == WorkflowState.Blocked && (evidence is null || !MatchesRun(run, evidence) || evidence.Status != ToolResultStatus.Blocked || evidence.PolicyDecision != PolicyDecision.Block)) return new(false, run.State, "BLOCKED requires a blocked policy event");
        if (target == WorkflowState.Verified && (verification is null || !verification.Passed || evidence is null || !MatchesRun(run, evidence) || verification.ResultEventId != evidence.ResultEventId || !IsVerifierEvent(verification, run))) return new(false, run.State, "VERIFIED requires an independent verifier event linked to the result");
        if (target == WorkflowState.Reportable && (report is null || !report.Allowed || !IsReportEvent(report, run))) return new(false, run.State, "REPORTABLE requires an allowed report-policy event");
        var audit = _audit.Append(run.RunId, "STATE_TRANSITION", string.Join("|", run.ActionId, run.State, target, evidenceEventId, verification?.VerificationEventId, report?.ReportEventId));
        run.State = target;
        run.LastEvidenceEventId = evidenceEventId;
        run.LastAuditEventId = audit.EventId;
        return new(true, target, "transition accepted by host state machine");
    }

    public StateTransitionResult VerifySnapshot(WorkflowRun run, IReadOnlyList<EvidenceEvent> snapshot)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(snapshot);
        return _evidence.VerifyIntegrity(snapshot) && _audit.VerifyIntegrity() ? new(true, run.State, "evidence snapshot is intact") : Freeze(run, "evidence snapshot or workflow audit integrity failed");
    }

    private bool MatchesRun(WorkflowRun run, EvidenceEvent evidence) => evidence.RunId == run.RunId && evidence.ActionId == run.ActionId && evidence.ActionRequestRef == run.ActionHash;

    private bool IsVerifierEvent(VerificationRecord verification, WorkflowRun run)
    {
        if (!_audit.TryGet(verification.VerificationEventId, out var entry) || entry is null || entry.Type != "VERIFIER_RESULT" || entry.RunId != run.RunId) return false;
        var parts = entry.Payload.Split('|');
        return parts.Length >= 4 && parts[0] == verification.ResultEventId && parts[1] == verification.VerifierRef && bool.TryParse(parts[2], out var passed) && passed && parts[3] == verification.ObservationRef;
    }

    private bool IsReportEvent(ReportDecision report, WorkflowRun run)
    {
        if (!_audit.TryGet(report.ReportEventId, out var entry) || entry is null || entry.Type != "REPORT_POLICY" || entry.Payload.Length == 0) return false;
        var parts = entry.Payload.Split('|');
        return entry.RunId == run.RunId && parts.Length >= 4 && parts[0] == report.FindingRef && parts[1] == report.EvidenceEventId && parts[2] == report.VerificationEventId && bool.TryParse(parts[3], out var allowed) && allowed;
    }

    private StateTransitionResult Freeze(WorkflowRun run, string reason)
    {
        run.State = WorkflowState.Stopped;
        var audit = _audit.Append(run.RunId, "INTEGRITY_FAILURE", reason);
        run.LastAuditEventId = audit.EventId;
        return new(false, WorkflowState.Stopped, reason);
    }

    private static bool CanTransition(WorkflowState from, WorkflowState to) => (from, to) switch
    {
        (WorkflowState.Ready, WorkflowState.Planned) => true,
        (WorkflowState.Planned, WorkflowState.Proposed) => true,
        (WorkflowState.Proposed, WorkflowState.Allowed or WorkflowState.Blocked) => true,
        (WorkflowState.Allowed, WorkflowState.Running or WorkflowState.Stopped) => true,
        (WorkflowState.Running, WorkflowState.Observed or WorkflowState.Unknown or WorkflowState.Stopped or WorkflowState.Stopping) => true,
        (WorkflowState.Stopping, WorkflowState.Stopped) => true,
        (WorkflowState.Observed, WorkflowState.Verified or WorkflowState.Unknown) => true,
        (WorkflowState.Verified, WorkflowState.Reportable or WorkflowState.Planned) => true,
        (WorkflowState.Unknown, WorkflowState.Planned or WorkflowState.Stopped) => true,
        (WorkflowState.Blocked, WorkflowState.Planned or WorkflowState.Stopped) => true,
        _ => false
    };
}

public sealed class IndependentFixtureVerifier
{
    private readonly EvidenceLedger _evidence;
    private readonly WorkflowAuditLog _audit;

    public IndependentFixtureVerifier(EvidenceLedger evidence, WorkflowAuditLog audit)
    {
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public VerificationRecord Verify(string resultEventId, byte[] expectedRaw, string expectedObservationRef, string verifierRef = "phase3-independent-fixture-verifier")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultEventId);
        ArgumentNullException.ThrowIfNull(expectedRaw);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedObservationRef);
        EvidenceEvent? result = null;
        var passed = _evidence.VerifyIntegrity() && _evidence.TryGet(resultEventId, out result) && result is not null;
        if (passed)
        {
            passed = (result!.Status is ToolResultStatus.Success or ToolResultStatus.Partial) && result.CleanupResult == "SUCCEEDED";
            passed = passed && result.ObservationRefs.Contains(expectedObservationRef, StringComparer.Ordinal);
            passed = passed && _evidence.TryReadArtifact(result.RawArtifactRef, out var actualRaw) && actualRaw.SequenceEqual(expectedRaw);
        }
        if (verifierRef != "phase3-independent-fixture-verifier") throw new InvalidOperationException("unregistered verifier reference");
        var eventRecord = _audit.Append(result?.RunId ?? "unknown", "VERIFIER_RESULT", string.Join("|", resultEventId, verifierRef, passed, expectedObservationRef));
        return new(eventRecord.EventId, resultEventId, verifierRef, passed, expectedObservationRef, passed ? "fixture observation reproduced" : "fixture observation did not reproduce", AuthoritativeClock.UtcNow);
    }
}

public sealed class ReplayCatalog
{
    private readonly Dictionary<string, string> _fixtures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _environments = new(StringComparer.Ordinal);
    private bool _frozen;

    public bool IsFrozen => _frozen;

    public void RegisterFixture(string reference, byte[] contents) => Register(_fixtures, reference, Canonicalization.Sha256Hex(contents));
    public void RegisterEnvironment(string reference, byte[] contents) => Register(_environments, reference, Canonicalization.Sha256Hex(contents));
    public void Freeze() => _frozen = true;

    public bool Contains(string fixtureReference, string environmentReference) => _frozen && _fixtures.ContainsKey(fixtureReference) && _environments.ContainsKey(environmentReference);

    public bool TryGet(string fixtureReference, string environmentReference, out string fixtureHash, out string environmentHash)
    {
        if (_frozen && _fixtures.TryGetValue(fixtureReference, out fixtureHash!) && _environments.TryGetValue(environmentReference, out environmentHash!)) return true;
        fixtureHash = string.Empty;
        environmentHash = string.Empty;
        return false;
    }

    private void Register(Dictionary<string, string> catalog, string reference, string contentHash)
    {
        if (_frozen) throw new InvalidOperationException("replay catalog is frozen");
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (!ProviderProposalValidator.IsSha256(contentHash)) throw new ArgumentException("replay identity hash is invalid", nameof(contentHash));
        if (!catalog.TryAdd(reference, contentHash)) throw new InvalidOperationException("replay identity is already registered");
    }
}

public sealed record ReplayPackage(
    string RunId,
    string ActionId,
    string ResultEventId,
    string ToolRef,
    string ToolVersion,
    string FixtureRef,
    string EnvironmentRef,
    string FixtureHash,
    string EnvironmentHash,
    Replayability Replayability);

public sealed record ReplayValidation(bool Valid, Replayability Replayability, string Reason);

public static class ReplayEngine
{
    public static ReplayPackage Build(EvidenceLedger evidence, ReplayCatalog catalog, string runId, string actionId, string resultEventId, string fixtureRef, string environmentRef)
    {
        if (!evidence.TryGet(resultEventId, out var result) || result is null || result.RunId != runId || result.ActionId != actionId) return new(runId, actionId, resultEventId, string.Empty, string.Empty, fixtureRef, environmentRef, string.Empty, string.Empty, Replayability.NonReplayable);
        var known = catalog.TryGet(fixtureRef, environmentRef, out var fixtureHash, out var environmentHash);
        var label = evidence.VerifyIntegrity() && known ? Replayability.Replayable : Replayability.PartiallyReplayable;
        return new(runId, actionId, resultEventId, result.ToolRef, result.ToolVersion, fixtureRef, environmentRef, fixtureHash, environmentHash, label);
    }

    public static ReplayValidation Validate(EvidenceLedger evidence, ReplayCatalog catalog, ReplayPackage package)
    {
        if (!evidence.VerifyIntegrity()) return new(false, Replayability.NonReplayable, "evidence integrity failed");
        if (!evidence.TryGet(package.ResultEventId, out var result) || result is null) return new(false, Replayability.NonReplayable, "result event is missing");
        if (result.RunId != package.RunId || result.ActionId != package.ActionId || result.ToolRef != package.ToolRef || result.ToolVersion != package.ToolVersion) return new(false, Replayability.NonReplayable, "replay package does not match result event");
        var known = catalog.TryGet(package.FixtureRef, package.EnvironmentRef, out var fixtureHash, out var environmentHash);
        if (package.FixtureHash != fixtureHash || package.EnvironmentHash != environmentHash) return new(false, Replayability.PartiallyReplayable, "replay identity hashes do not match the frozen catalog");
        var expected = known ? Replayability.Replayable : Replayability.PartiallyReplayable;
        if (package.Replayability != expected) return new(false, expected, "replayability label does not match available package identity");
        return new(true, expected, "replay package is internally consistent");
    }
}

public sealed class FindingRecord
{
    public FindingRecord(string findingRef, string runId, string actionId, string actionHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(findingRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionHash);
        FindingRef = findingRef;
        RunId = runId;
        ActionId = actionId;
        ActionHash = actionHash;
    }

    public string FindingRef { get; }
    public string RunId { get; }
    public string ActionId { get; }
    public string ActionHash { get; }
    public FindingState State { get; internal set; } = FindingState.Hypothesis;
    public string? EvidenceEventId { get; internal set; }
    public string? VerificationEventId { get; internal set; }
    public string? ReportEventId { get; internal set; }
}

public sealed class FindingLifecycle
{
    private readonly EvidenceLedger _evidence;
    private readonly WorkflowAuditLog _audit;

    public FindingLifecycle(EvidenceLedger evidence, WorkflowAuditLog audit)
    {
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public bool TryAdvance(FindingRecord finding, FindingState target, string? evidenceEventId = null, string? verificationEventId = null, string? reportEventId = null)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (!CanTransition(finding.State, target)) return false;
        if (!_evidence.VerifyIntegrity() || !_audit.VerifyIntegrity()) return false;
        if (target is FindingState.Reproducible or FindingState.Verified or FindingState.Reportable && (evidenceEventId is null || !_evidence.TryGet(evidenceEventId, out var evidence) || evidence is null || evidence.RunId != finding.RunId || evidence.ActionId != finding.ActionId || evidence.ActionRequestRef != finding.ActionHash || evidence.Status is not (ToolResultStatus.Success or ToolResultStatus.Partial) || evidence.CleanupResult != "SUCCEEDED")) return false;
        if (target is FindingState.Verified or FindingState.Reportable && (verificationEventId is null || !IsVerifierEvent(verificationEventId, evidenceEventId!, finding.RunId))) return false;
        if (target == FindingState.Reportable && (reportEventId is null || evidenceEventId is null || verificationEventId is null || !IsReportEvent(reportEventId, finding.FindingRef, finding.RunId, evidenceEventId, verificationEventId))) return false;
        var audit = _audit.Append(finding.RunId, "FINDING_TRANSITION", string.Join("|", finding.FindingRef, finding.State, target, evidenceEventId, verificationEventId, reportEventId));
        finding.State = target;
        finding.EvidenceEventId = evidenceEventId ?? finding.EvidenceEventId;
        finding.VerificationEventId = verificationEventId ?? finding.VerificationEventId;
        finding.ReportEventId = reportEventId ?? finding.ReportEventId;
        return _audit.Contains(audit.EventId);
    }

    private bool IsVerifierEvent(string eventId, string resultEventId, string runId)
    {
        if (!_audit.TryGet(eventId, out var entry) || entry is null || entry.Type != "VERIFIER_RESULT" || entry.RunId != runId) return false;
        var parts = entry.Payload.Split('|');
        return parts.Length >= 4 && parts[0] == resultEventId && parts[1] == "phase3-independent-fixture-verifier" && bool.TryParse(parts[2], out var passed) && passed && _evidence.TryGet(resultEventId, out var evidence) && evidence is not null && evidence.ObservationRefs.Contains(parts[3], StringComparer.Ordinal);
    }

    private bool IsReportEvent(string eventId, string findingRef, string runId, string evidenceEventId, string verificationEventId)
    {
        if (!_audit.TryGet(eventId, out var entry) || entry is null || entry.Type != "REPORT_POLICY" || entry.RunId != runId) return false;
        var parts = entry.Payload.Split('|');
        return parts.Length >= 4 && parts[0] == findingRef && parts[1] == evidenceEventId && parts[2] == verificationEventId && bool.TryParse(parts[3], out var allowed) && allowed;
    }

    private static bool CanTransition(FindingState from, FindingState to) => (from, to) switch
    {
        (FindingState.Hypothesis, FindingState.Candidate) => true,
        (FindingState.Candidate, FindingState.Reproducible or FindingState.Unknown or FindingState.Rejected) => true,
        (FindingState.Reproducible, FindingState.Verified or FindingState.Unknown or FindingState.Blocked) => true,
        (FindingState.Verified, FindingState.Reportable or FindingState.Rejected) => true,
        (FindingState.Unverified, FindingState.Unknown or FindingState.Blocked or FindingState.Rejected) => true,
        _ => false
    };
}

public sealed record ReportArtifact(string FindingRef, string EvidenceEventId, string VerificationEventId, string ReportEventId);

public sealed class ReportPolicy
{
    private readonly EvidenceLedger _evidence;
    private readonly WorkflowAuditLog _audit;

    public ReportPolicy(EvidenceLedger evidence, WorkflowAuditLog audit)
    {
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public ReportDecision Decide(FindingRecord finding)
    {
        var allowed = finding.State == FindingState.Verified && finding.EvidenceEventId is not null && finding.VerificationEventId is not null && _evidence.TryGet(finding.EvidenceEventId, out var evidence) && evidence is not null && evidence.RunId == finding.RunId && evidence.ActionId == finding.ActionId && evidence.ActionRequestRef == finding.ActionHash && (evidence.Status is ToolResultStatus.Success or ToolResultStatus.Partial) && evidence.CleanupResult == "SUCCEEDED" && IsVerifierEvent(finding.VerificationEventId, finding.EvidenceEventId, finding.RunId, evidence);
        var evidenceEventId = finding.EvidenceEventId ?? string.Empty;
        var verificationEventId = finding.VerificationEventId ?? string.Empty;
        var audit = _audit.Append(finding.RunId, "REPORT_POLICY", string.Join("|", finding.FindingRef, evidenceEventId, verificationEventId, allowed));
        return new(audit.EventId, finding.FindingRef, evidenceEventId, verificationEventId, allowed, allowed ? "verified evidence is reportable" : "finding is not independently verified", AuthoritativeClock.UtcNow);
    }

    private bool IsVerifierEvent(string eventId, string resultEventId, string runId, EvidenceEvent result)
    {
        if (!_audit.TryGet(eventId, out var entry) || entry is null || entry.Type != "VERIFIER_RESULT" || entry.RunId != runId) return false;
        var parts = entry.Payload.Split('|');
        return parts.Length >= 4 && parts[0] == resultEventId && parts[1] == "phase3-independent-fixture-verifier" && bool.TryParse(parts[2], out var passed) && passed && result.ObservationRefs.Contains(parts[3], StringComparer.Ordinal);
    }

    public ReportArtifact Build(FindingRecord finding, ReportDecision decision)
    {
        if (!decision.Allowed || decision.FindingRef != finding.FindingRef || decision.EvidenceEventId != finding.EvidenceEventId || decision.VerificationEventId != finding.VerificationEventId || finding.State != FindingState.Reportable || finding.EvidenceEventId is null || finding.VerificationEventId is null || !_evidence.VerifyIntegrity() || !_audit.VerifyIntegrity() || !_audit.TryGet(decision.ReportEventId, out var entry) || entry is null || entry.Type != "REPORT_POLICY" || entry.RunId != finding.RunId || !entry.Payload.StartsWith(finding.FindingRef + "|" + finding.EvidenceEventId + "|" + finding.VerificationEventId + "|True", StringComparison.Ordinal)) throw new InvalidOperationException("report requires a reportable independently verified finding");
        return new(finding.FindingRef, finding.EvidenceEventId, finding.VerificationEventId, decision.ReportEventId);
    }
}
