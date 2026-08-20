using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CyberSopHarness.Core;

public sealed record EvidenceEvent(
    string Type,
    string RunId,
    string ActionId,
    string ActionRequestRef,
    string Reason,
    ProviderExecutionMetadata Provider,
    string ResultEventId,
    ToolResultStatus Status,
    string ToolRef,
    string ToolVersion,
    string WorkerRef,
    string TargetRef,
    string AuthorizationRef,
    string ScopeRef,
    string CapabilityRef,
    RiskClass RiskClass,
    IReadOnlyList<string> MethodologyRefs,
    PolicyDecision PolicyDecision,
    string PolicyRef,
    string PolicyVersion,
    string? PermitRef,
    string RawSha256,
    string? RedactedSha256,
    string RawArtifactRef,
    string? RedactedArtifactRef,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<string> ObservationRefs,
    string? ParentEventId,
    string? ApprovalRef,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    DateTimeOffset ObservedAt,
    int? ExitCode,
    string CleanupResult,
    string EventHash,
    string? PreviousEventHash);

public sealed record EvidenceEventDraft(
    string RunId,
    string ActionId,
    string ActionRequestRef,
    string Reason,
    ProviderExecutionMetadata Provider,
    ToolResultStatus Status,
    string ToolRef,
    string ToolVersion,
    string WorkerRef,
    string TargetRef,
    string AuthorizationRef,
    string ScopeRef,
    string CapabilityRef,
    RiskClass RiskClass,
    IReadOnlyList<string> MethodologyRefs,
    PolicyDecision PolicyDecision,
    string PolicyRef,
    string PolicyVersion,
    string? PermitRef,
    byte[] RawOutput,
    byte[]? RedactedOutput,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<string> ObservationRefs,
    string? ParentEventId,
    string? ApprovalRef,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    DateTimeOffset ObservedAt,
    int? ExitCode,
    string CleanupResult);

public sealed class ArtifactStore
{
    private readonly Dictionary<string, byte[]> _artifacts = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public string Put(string kind, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var safeKind = new string(kind.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').ToArray());
        if (safeKind.Length == 0) safeKind = "artifact";
        var reference = $"artifact_{safeKind}_{hash}";
        lock (_gate) _artifacts.TryAdd(reference, bytes.ToArray());
        return reference;
    }

    public bool TryGet(string reference, out byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            bytes = Array.Empty<byte>();
            return false;
        }
        lock (_gate)
        {
            if (!_artifacts.TryGetValue(reference, out var stored))
            {
                bytes = Array.Empty<byte>();
                return false;
            }
            bytes = stored.ToArray();
            return true;
        }
    }

    public int Count
    {
        get { lock (_gate) return _artifacts.Count; }
    }
}

public static class EvidenceCanonicalization
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Payload(EvidenceEvent evidence)
    {
        var payload = new
        {
            evidence.Type,
            evidence.RunId,
            evidence.ActionId,
            evidence.ActionRequestRef,
            evidence.Reason,
            Provider = new
            {
                ProviderRef = evidence.Provider.Descriptor.ProviderRef,
                ModelRef = evidence.Provider.Descriptor.ModelRef,
                ModelVersion = evidence.Provider.Descriptor.ModelVersion,
                ConfigurationHash = evidence.Provider.Descriptor.ConfigurationHash,
                ContextPolicy = evidence.Provider.Descriptor.ContextPolicy,
                DataRetentionPolicy = evidence.Provider.Descriptor.DataRetentionPolicy,
                ToolCallMode = evidence.Provider.Descriptor.ToolCallMode,
                evidence.Provider.OutputSha256,
                LatencyMilliseconds = evidence.Provider.Latency.TotalMilliseconds,
                evidence.Provider.TokenUsage,
                FailureClass = evidence.Provider.FailureClass.ToString()
            },
            evidence.ResultEventId,
            Status = evidence.Status.ToString().ToUpperInvariant(),
            evidence.ToolRef,
            evidence.ToolVersion,
            evidence.WorkerRef,
            evidence.TargetRef,
            evidence.AuthorizationRef,
            evidence.ScopeRef,
            evidence.CapabilityRef,
            RiskClass = evidence.RiskClass.ToString(),
            MethodologyRefs = evidence.MethodologyRefs.ToArray(),
            PolicyDecision = evidence.PolicyDecision.ToString().ToUpperInvariant(),
            evidence.PolicyRef,
            evidence.PolicyVersion,
            evidence.PermitRef,
            evidence.RawSha256,
            evidence.RedactedSha256,
            evidence.RawArtifactRef,
            evidence.RedactedArtifactRef,
            ArtifactRefs = evidence.ArtifactRefs.ToArray(),
            ObservationRefs = evidence.ObservationRefs.ToArray(),
            evidence.ParentEventId,
            evidence.ApprovalRef,
            StartedAt = evidence.StartedAt.ToUniversalTime().ToString("O"),
            FinishedAt = evidence.FinishedAt.ToUniversalTime().ToString("O"),
            ObservedAt = evidence.ObservedAt.ToUniversalTime().ToString("O"),
            evidence.ExitCode,
            evidence.CleanupResult,
            evidence.PreviousEventHash
        };
        return JsonSerializer.Serialize(payload, Options);
    }

    public static string Hash(EvidenceEvent evidence) => Canonicalization.Sha256Hex(Payload(evidence));
}

public sealed class EvidenceLedger
{
    private readonly ArtifactStore _artifacts;
    private readonly List<EvidenceEvent> _events = new();
    private readonly DurableEvidenceJournal? _journal;
    private readonly object _gate = new();

    public EvidenceLedger(ArtifactStore artifacts, DurableEvidenceJournal? journal = null)
    {
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        _journal = journal;
    }

    internal EvidenceEvent Append(EvidenceEventDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrWhiteSpace(draft.RunId) || string.IsNullOrWhiteSpace(draft.ActionId) || string.IsNullOrWhiteSpace(draft.ActionRequestRef)) throw new ArgumentException("evidence identity is incomplete", nameof(draft));
        if (string.IsNullOrWhiteSpace(draft.ToolRef) || string.IsNullOrWhiteSpace(draft.ToolVersion) || string.IsNullOrWhiteSpace(draft.WorkerRef)) throw new ArgumentException("tool/worker identity is incomplete", nameof(draft));
        if (draft.RawOutput is null) throw new ArgumentException("raw output is required", nameof(draft));
        if (draft.MethodologyRefs is null || draft.ObservationRefs is null || draft.ArtifactRefs is null || draft.ArtifactRefs.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("evidence collections are invalid", nameof(draft));
        if (draft.Status == ToolResultStatus.Blocked && (draft.PolicyDecision != PolicyDecision.Block || draft.PermitRef is not null)) throw new InvalidOperationException("blocked evidence must have a BLOCK policy result and no permit");
        if (draft.Status != ToolResultStatus.Blocked && (draft.PolicyDecision != PolicyDecision.Allow || string.IsNullOrWhiteSpace(draft.PermitRef))) throw new InvalidOperationException("dispatched evidence must have an ALLOW policy result and permit");
        if (draft.Status is ToolResultStatus.Success or ToolResultStatus.Partial && (draft.RedactedOutput is null || draft.ObservationRefs.Count == 0)) throw new InvalidOperationException("successful evidence requires redacted output and an observation");
        if (draft.ArtifactRefs.Any(reference => !_artifacts.TryGet(reference, out _))) throw new InvalidOperationException("evidence references an artifact that is not stored");
        var rawReference = _artifacts.Put("raw", draft.RawOutput);
        var rawHash = Convert.ToHexString(SHA256.HashData(draft.RawOutput)).ToLowerInvariant();
        var redactedReference = draft.RedactedOutput is null ? null : _artifacts.Put("redacted", draft.RedactedOutput);
        var redactedHash = draft.RedactedOutput is null ? null : Convert.ToHexString(SHA256.HashData(draft.RedactedOutput)).ToLowerInvariant();
        lock (_gate)
        {
            var previousHash = _events.Count == 0 ? null : _events[^1].EventHash;
            var artifactReferences = draft.ArtifactRefs.Concat(new[] { rawReference }).Concat(redactedReference is null ? Array.Empty<string>() : new[] { redactedReference }).Distinct(StringComparer.Ordinal).ToArray();
            var unsigned = new EvidenceEvent(
                "TOOL_RESULT",
                draft.RunId,
                draft.ActionId,
                draft.ActionRequestRef,
                draft.Reason,
                draft.Provider,
                "result_" + Guid.NewGuid().ToString("N"),
                draft.Status,
                draft.ToolRef,
                draft.ToolVersion,
                draft.WorkerRef,
                draft.TargetRef,
                draft.AuthorizationRef,
                draft.ScopeRef,
                draft.CapabilityRef,
                draft.RiskClass,
                draft.MethodologyRefs.ToArray(),
                draft.PolicyDecision,
                draft.PolicyRef,
                draft.PolicyVersion,
                draft.PermitRef,
                rawHash,
                redactedHash,
                rawReference,
                redactedReference,
                artifactReferences,
                draft.ObservationRefs.ToArray(),
                draft.ParentEventId,
                draft.ApprovalRef,
                draft.StartedAt,
                draft.FinishedAt,
                draft.ObservedAt,
                draft.ExitCode,
                draft.CleanupResult,
                string.Empty,
                previousHash);
            var evidence = unsigned with { EventHash = EvidenceCanonicalization.Hash(unsigned) };
            _events.Add(evidence);
            PersistToJournal(evidence);
            return evidence;
        }
    }

    private void PersistToJournal(EvidenceEvent evidence)
    {
        if (_journal is null) return;
        if (_journal.Artifacts is not null)
        {
            foreach (var reference in evidence.ArtifactRefs)
            {
                if (_artifacts.TryGet(reference, out var artifactBytes)) _journal.Artifacts.Put(reference, artifactBytes);
            }
        }
        _journal.Append(evidence);
    }

    public bool TryGet(string eventId, out EvidenceEvent? evidence)
    {
        lock (_gate)
        {
            evidence = _events.FirstOrDefault(item => item.ResultEventId == eventId);
            return evidence is not null;
        }
    }

    public bool TryReadArtifact(string reference, out byte[] bytes) => _artifacts.TryGet(reference, out bytes);

    public bool HasArtifact(string reference) => _artifacts.TryGet(reference, out _);

    public IReadOnlyList<EvidenceEvent> Snapshot()
    {
        lock (_gate) return _events.ToArray();
    }

    public bool VerifyIntegrity(IReadOnlyList<EvidenceEvent>? snapshot = null)
    {
        var events = snapshot ?? Snapshot();
        lock (_gate)
        {
            if (snapshot is not null && (snapshot.Count != _events.Count || (snapshot.Count > 0 && snapshot[^1].EventHash != _events[^1].EventHash))) return false;
        }
        string? previousHash = null;
        foreach (var evidence in events)
        {
            if (!string.Equals(evidence.PreviousEventHash, previousHash, StringComparison.Ordinal)) return false;
            if (!string.Equals(EvidenceCanonicalization.Hash(evidence), evidence.EventHash, StringComparison.Ordinal)) return false;
            if (!_artifacts.TryGet(evidence.RawArtifactRef, out var raw) || !string.Equals(Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant(), evidence.RawSha256, StringComparison.Ordinal)) return false;
            if (evidence.RedactedArtifactRef is not null && (!_artifacts.TryGet(evidence.RedactedArtifactRef, out var redacted) || evidence.RedactedSha256 is null || !string.Equals(Convert.ToHexString(SHA256.HashData(redacted)).ToLowerInvariant(), evidence.RedactedSha256, StringComparison.Ordinal))) return false;
            if (evidence.ArtifactRefs.Any(reference => !_artifacts.TryGet(reference, out _))) return false;
            previousHash = evidence.EventHash;
        }
        return true;
    }
}

public sealed record WorkflowAuditEntry(
    string EventId,
    string RunId,
    string Type,
    string Payload,
    string? PreviousEventHash,
    string EventHash);

public sealed class WorkflowAuditLog
{
    private readonly List<WorkflowAuditEntry> _entries = new();
    private readonly DurableEvidenceJournal? _journal;
    private readonly object _gate = new();

    public WorkflowAuditLog(DurableEvidenceJournal? journal = null)
    {
        _journal = journal;
    }

    internal WorkflowAuditEntry Append(string runId, string type, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(payload);
        lock (_gate)
        {
            var previous = _entries.Count == 0 ? null : _entries[^1].EventHash;
            var unsigned = new WorkflowAuditEntry("audit_" + Guid.NewGuid().ToString("N"), runId, type, payload, previous, string.Empty);
            var entry = unsigned with { EventHash = Canonicalization.Sha256Hex(string.Join("|", unsigned.EventId, unsigned.RunId, unsigned.Type, unsigned.Payload, unsigned.PreviousEventHash)) };
            _entries.Add(entry);
            if (_journal is not null) _journal.Append(entry);
            return entry;
        }
    }

    public bool Contains(string eventId)
    {
        lock (_gate) return _entries.Any(item => item.EventId == eventId);
    }

    public bool TryGet(string eventId, out WorkflowAuditEntry? entry)
    {
        lock (_gate)
        {
            entry = _entries.FirstOrDefault(item => item.EventId == eventId);
            return entry is not null;
        }
    }

    public bool VerifyIntegrity()
    {
        lock (_gate)
        {
            string? previous = null;
            foreach (var entry in _entries)
            {
                if (entry.PreviousEventHash != previous) return false;
                var expected = Canonicalization.Sha256Hex(string.Join("|", entry.EventId, entry.RunId, entry.Type, entry.Payload, entry.PreviousEventHash));
                if (entry.EventHash != expected) return false;
                previous = entry.EventHash;
            }
            return true;
        }
    }
}
