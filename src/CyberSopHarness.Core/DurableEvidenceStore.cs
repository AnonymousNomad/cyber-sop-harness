using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CyberSopHarness.Core;

public enum RecoveryStatus
{
    Verified,
    Partial,
    Corrupt
}

public sealed record DurableRecoveryResult(
    RecoveryStatus Status,
    IReadOnlyList<EvidenceEvent> Events,
    IReadOnlyList<WorkflowAuditEntry> AuditEntries,
    int ValidRecordCount,
    string? Reason);

public static class AuditCanonicalization
{
    public static string Payload(WorkflowAuditEntry entry) => string.Join("|", entry.EventId, entry.RunId, entry.Type, entry.Payload, entry.PreviousEventHash);

    public static string Hash(WorkflowAuditEntry entry) => Canonicalization.Sha256Hex(Payload(entry));
}

public sealed class DurableArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _directory;

    public DurableArtifactStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) throw new ArgumentException("artifact directory must be an absolute path", nameof(directory));
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public string DirectoryPath => _directory;

    public string Put(string reference, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ValidateReference(reference);
        var path = GetPath(reference);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
        return reference;
    }

    public bool Exists(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return false;
        return File.Exists(GetPath(reference));
    }

    public bool TryGet(string reference, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(reference) || !File.Exists(GetPath(reference))) return false;
        bytes = File.ReadAllBytes(GetPath(reference));
        return true;
    }

    public bool VerifyHash(string reference, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256) || !Exists(reference)) return false;
        var actual = Canonicalization.Sha256Hex(File.ReadAllBytes(GetPath(reference)));
        return string.Equals(actual, expectedSha256, StringComparison.Ordinal);
    }

    public void Delete(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return;
        var path = GetPath(reference);
        if (File.Exists(path)) File.Delete(path);
    }

    private string GetPath(string reference) => Path.Combine(_directory, reference);

    private static void ValidateReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("artifact reference is required", nameof(reference));
        if (reference.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || reference.Contains('\\') || reference.Contains('/') || reference.StartsWith('.')) throw new ArgumentException("artifact reference is unsafe", nameof(reference));
    }
}

public sealed class DurableEvidenceJournal : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _journalPath;
    private readonly DurableArtifactStore? _artifacts;
    private string? _lastRecordHash;
    private readonly object _gate = new();

    public DurableEvidenceJournal(string journalPath, DurableArtifactStore? artifacts = null)
    {
        if (string.IsNullOrWhiteSpace(journalPath) || !Path.IsPathFullyQualified(journalPath)) throw new ArgumentException("journal path must be absolute", nameof(journalPath));
        _journalPath = journalPath;
        _artifacts = artifacts;
    }

    public string JournalPath => _journalPath;

    public DurableArtifactStore? Artifacts => _artifacts;

    public void Append(EvidenceEvent evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        AppendRecord("event", JsonSerializer.Serialize(evidence, JsonOptions));
    }

    public void Append(WorkflowAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        AppendRecord("audit", JsonSerializer.Serialize(entry, JsonOptions));
    }

    public DurableRecoveryResult Recover()
    {
        lock (_gate)
        {
            if (!File.Exists(_journalPath))
            {
                return new DurableRecoveryResult(RecoveryStatus.Verified, Array.Empty<EvidenceEvent>(), Array.Empty<WorkflowAuditEntry>(), 0, "journal is empty");
            }

            var events = new List<EvidenceEvent>();
            var audits = new List<WorkflowAuditEntry>();
            string? previousRecordHash = null;
            string? previousEventHash = null;
            string? previousAuditHash = null;
            int valid = 0;
            long validEndOffset = 0;

            var lines = ReadLinesWithOffsets();
            for (var i = 0; i < lines.Count; i++)
            {
                var (line, startOffset) = lines[i];
                var isLast = i == lines.Count - 1;
                var parsed = TryParseRecord(line, previousRecordHash, events, audits, ref previousEventHash, ref previousAuditHash, out var recordHash, out var reason);
                if (parsed)
                {
                    previousRecordHash = recordHash;
                    valid++;
                    validEndOffset = startOffset + line.Length + 1;
                    continue;
                }

                if (isLast)
                {
                    TruncateTo(validEndOffset);
                    var artifactIssue = VerifyArtifacts(events);
                    if (artifactIssue is not null) return new DurableRecoveryResult(RecoveryStatus.Corrupt, events, audits, valid, artifactIssue);
                    return new DurableRecoveryResult(RecoveryStatus.Partial, events, audits, valid, "trailing partial record discarded");
                }

                return new DurableRecoveryResult(RecoveryStatus.Corrupt, events, audits, valid, reason);
            }

            var verifiedIssue = VerifyArtifacts(events);
            if (verifiedIssue is not null) return new DurableRecoveryResult(RecoveryStatus.Corrupt, events, audits, valid, verifiedIssue);
            return new DurableRecoveryResult(RecoveryStatus.Verified, events, audits, valid, null);
        }
    }

    private void AppendRecord(string recordType, string payloadJson)
    {
        lock (_gate)
        {
            var previous = _lastRecordHash;
            var recordHash = Canonicalization.Sha256Hex(string.Join("|", recordType, payloadJson, previous ?? string.Empty));
            var line = JsonSerializer.Serialize(new RecordLine(recordType, payloadJson, recordHash, previous), JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            var directory = Path.GetDirectoryName(_journalPath) ?? throw new InvalidOperationException("journal directory is missing");
            Directory.CreateDirectory(directory);
            using var stream = new FileStream(_journalPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
            _lastRecordHash = recordHash;
        }
    }

    private void TruncateTo(long length)
    {
        using var stream = new FileStream(_journalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        if (stream.Length > length)
        {
            stream.SetLength(length);
            stream.Flush(true);
        }
    }

    private List<(string Line, long StartOffset)> ReadLinesWithOffsets()
    {
        var bytes = File.ReadAllBytes(_journalPath);
        var text = Encoding.UTF8.GetString(bytes);
        var result = new List<(string, long)>();
        long offset = 0;
        foreach (var line in text.Split('\n'))
        {
            result.Add((line, offset));
            offset += Encoding.UTF8.GetByteCount(line) + 1;
        }
        if (text.EndsWith('\n') && result.Count > 0) result.RemoveAt(result.Count - 1);
        return result;
    }

    private static bool TryParseRecord(string line, string? previousRecordHash, List<EvidenceEvent> events, List<WorkflowAuditEntry> audits, ref string? previousEventHash, ref string? previousAuditHash, out string recordHash, out string? reason)
    {
        recordHash = string.Empty;
        reason = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            reason = "blank journal line";
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("rt", out var typeElement) || !root.TryGetProperty("p", out var payloadElement) || !root.TryGetProperty("h", out var hashElement)) { reason = "journal line is missing record fields"; return false; }
            var recordType = typeElement.GetString();
            var payloadJson = payloadElement.GetString();
            recordHash = hashElement.GetString() ?? string.Empty;
            var previous = root.TryGetProperty("prev", out var prevElement) && prevElement.ValueKind == JsonValueKind.String ? prevElement.GetString() : null;
            if (recordType is null || payloadJson is null || !string.Equals(previous, previousRecordHash, StringComparison.Ordinal)) { reason = "record chain link mismatch"; return false; }
            if (!string.Equals(Canonicalization.Sha256Hex(string.Join("|", recordType, payloadJson, previous ?? string.Empty)), recordHash, StringComparison.Ordinal)) { reason = "record hash mismatch"; return false; }

            switch (recordType)
            {
                case "event":
                {
                    var evidence = JsonSerializer.Deserialize<EvidenceEvent>(payloadJson, JsonOptions);
                    if (evidence is null) { reason = "evidence record is empty"; return false; }
                    if (!string.Equals(evidence.PreviousEventHash, previousEventHash, StringComparison.Ordinal)) { reason = "evidence chain link mismatch"; return false; }
                    if (!string.Equals(EvidenceCanonicalization.Hash(evidence), evidence.EventHash, StringComparison.Ordinal)) { reason = "evidence hash mismatch"; return false; }
                    events.Add(evidence);
                    previousEventHash = evidence.EventHash;
                    break;
                }
                case "audit":
                {
                    var entry = JsonSerializer.Deserialize<WorkflowAuditEntry>(payloadJson, JsonOptions);
                    if (entry is null) { reason = "audit record is empty"; return false; }
                    if (!string.Equals(entry.PreviousEventHash, previousAuditHash, StringComparison.Ordinal)) { reason = "audit chain link mismatch"; return false; }
                    if (!string.Equals(AuditCanonicalization.Hash(entry), entry.EventHash, StringComparison.Ordinal)) { reason = "audit hash mismatch"; return false; }
                    audits.Add(entry);
                    previousAuditHash = entry.EventHash;
                    break;
                }
                default:
                    reason = "unknown record type";
                    return false;
            }
            return true;
        }
        catch (JsonException)
        {
            reason = "journal line is not valid JSON";
            return false;
        }
        catch (ArgumentException)
        {
            reason = "journal line has invalid data";
            return false;
        }
    }

    private string? VerifyArtifacts(IReadOnlyList<EvidenceEvent> events)
    {
        if (_artifacts is null) return null;
        foreach (var evidence in events)
        {
            foreach (var reference in evidence.ArtifactRefs)
            {
                if (!_artifacts.Exists(reference))
                {
                    return "evidence references a missing artifact: " + reference;
                }
                if (reference == evidence.RawArtifactRef && !_artifacts.VerifyHash(reference, evidence.RawSha256)) return "raw artifact hash mismatch: " + reference;
                if (reference == evidence.RedactedArtifactRef && (evidence.RedactedSha256 is null || !_artifacts.VerifyHash(reference, evidence.RedactedSha256))) return "redacted artifact hash mismatch: " + reference;
            }
        }
        return null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _lastRecordHash = null;
        }
    }

    private sealed record RecordLine(string Rt, string P, string H, string? Prev);
}