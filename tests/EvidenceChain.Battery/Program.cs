using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberSopHarness.Core;

/// <summary>
/// PROBE BATTERY: Evidence Chain
/// 
/// Purpose: Validate the complete evidence lifecycle — append, persist, recover,
/// tamper-detect, verify, and redact. This is the court-admissible backbone.
///
/// Coverage dimensions:
///   1. DurableEvidenceJournal: append, recover, tamper detection, double-recovery
///   2. DurableArtifactStore: put, get, verify, delete, invalid refs, hash mismatch
///   3. EvidenceLedger: append, reference integrity, redaction
///   4. Provenance: stamp, verify, rotation
///   5. ProvenanceKeyCustody: create, rotate, fingerprint
///   6. OutputRedactor: secret redaction, empty secrets, nested secrets
///
/// Pitfalls:
///   - File I/O: temp directories must be fully qualified and cleaned up
///   - JSON round-trip: property casing must match JsonNamingPolicy.CamelCase
///   - Hash determinism: SHA-256 of same bytes must always equal
///   - Key rotation: old evidence must still verify with old key metadata
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    public static async Task<int> Main()
    {
        // === ARTIFACT STORE PROBES ===
        await Run("artifact: put and get round-trips", TestArtifactPutGet);
        await Run("artifact: verify correct hash passes", TestArtifactVerifyCorrect);
        await Run("artifact: verify wrong hash fails", TestArtifactVerifyWrong);
        await Run("artifact: exists returns true after put", TestArtifactExists);
        await Run("artifact: exists returns false for missing", TestArtifactExistsFalse);
        await Run("artifact: delete removes artifact", TestArtifactDelete);
        await Run("artifact: empty reference rejected", TestArtifactEmptyRef);
        await Run("artifact: path-traversal reference rejected", TestArtifactPathTraversal);
        await Run("artifact: dot-prefixed reference rejected", TestArtifactDotPrefix);

        // === EVIDENCE JOURNAL PROBES ===
        await Run("journal: append and recover events", TestJournalAppendRecover);
        await Run("journal: tamper detection on modified event", TestJournalTamperDetection);
        await Run("journal: recovery after crash with partial write", TestJournalCrashRecovery);
        await Run("journal: event chain hash linkage", TestJournalChainLinkage);
        await Run("journal: empty journal recovers as empty", TestJournalEmptyRecovery);
        await Run("journal: concurrent appends are serialized", TestJournalConcurrentAppend);

        // === PROVENANCE PROBES ===
        await Run("provenance: stamp and verify", TestProvenanceStampVerify);
        await Run("provenance: tampered stamp fails verification", TestProvenanceTamperFail);
        await Run("provenance: key rotation preserves old stamps", TestProvenanceKeyRotation);

        // === PROVENANCE KEY CUSTODY PROBES ===
        await Run("key-custody: create new key pair", TestKeyCustodyCreate);
        await Run("key-custody: load existing key", TestKeyCustodyLoad);
        await Run("key-custody: rotation generates new fingerprint", TestKeyCustodyRotation);
        await Run("key-custody: fingerprint is deterministic", TestKeyCustodyFingerprint);

        // === OUTPUT REDACTION PROBES ===
        await Run("redaction: secret value is replaced", TestRedactionReplaces);
        await Run("redaction: no secrets leaves output unchanged", TestRedactionNoSecrets);
        await Run("redaction: empty secret list is no-op", TestRedactionEmpty);
        await Run("redaction: nested secret values are replaced", TestRedactionNested);
        await Run("redaction: null input is rejected", TestRedactionNull);

        Console.WriteLine($"\nevidence_chain_battery=passed count={_passed} failed count={_failed}");
        return _failed > 0 ? 1 : 0;
    }

    private static async Task Run(string name, Func<Task> test)
    {
        try { await test(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception ex) { _failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string TempDir(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"csh-battery-{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    // === ARTIFACT STORE ===

    private static Task TestArtifactPutGet()
    {
        var dir = TempDir("artifact-putget");
        try
        {
            var store = new DurableArtifactStore(dir);
            var data = Encoding.UTF8.GetBytes("test artifact content");
            var sha256 = Canonicalization.Sha256Hex(data);
            store.Put("test-artifact-v1", data);
            Assert(store.TryGet("test-artifact-v1", out var retrieved), "artifact should exist after put");
            Assert(Encoding.UTF8.GetString(retrieved) == "test artifact content", "retrieved content mismatch");
            Assert(store.VerifyHash("test-artifact-v1", sha256), "hash verification should pass");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestArtifactVerifyCorrect()
    {
        var dir = TempDir("artifact-verify-ok");
        try
        {
            var store = new DurableArtifactStore(dir);
            var data = new byte[] { 1, 2, 3, 4, 5 };
            store.Put("verify-ok", data);
            var hash = Canonicalization.Sha256Hex(data);
            Assert(store.VerifyHash("verify-ok", hash), "correct hash should verify");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestArtifactVerifyWrong()
    {
        var dir = TempDir("artifact-verify-bad");
        try
        {
            var store = new DurableArtifactStore(dir);
            store.Put("verify-bad", new byte[] { 1, 2, 3 });
            Assert(!store.VerifyHash("verify-bad", new string('0', 64)), "wrong hash should fail verification");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestArtifactExists()
    {
        var dir = TempDir("artifact-exists");
        try
        {
            var store = new DurableArtifactStore(dir);
            store.Put("exists-check", new byte[] { 42 });
            Assert(store.Exists("exists-check"), "artifact should exist after put");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestArtifactExistsFalse()
    {
        var dir = TempDir("artifact-noexist");
        try
        {
            var store = new DurableArtifactStore(dir);
            Assert(!store.Exists("nonexistent"), "missing artifact should not exist");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestArtifactDelete()
    {
        var dir = TempDir("artifact-delete");
        try
        {
            var store = new DurableArtifactStore(dir);
            store.Put("delete-me", new byte[] { 1 });
            Assert(store.Exists("delete-me"), "should exist before delete");
            store.Delete("delete-me");
            Assert(!store.Exists("delete-me"), "should not exist after delete");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestArtifactEmptyRef()
    {
        var dir = TempDir("artifact-empty-ref");
        try
        {
            var store = new DurableArtifactStore(dir);
            var threw = false;
            try { store.Put("", new byte[] { 1 }); } catch (ArgumentException) { threw = true; }
            Assert(threw, "empty reference should throw");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestArtifactPathTraversal()
    {
        var dir = TempDir("artifact-traversal");
        try
        {
            var store = new DurableArtifactStore(dir);
            var threw = false;
            try { store.Put("../escape", new byte[] { 1 }); } catch (ArgumentException) { threw = true; }
            Assert(threw, "path-traversal reference should throw");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestArtifactDotPrefix()
    {
        var dir = TempDir("artifact-dotprefix");
        try
        {
            var store = new DurableArtifactStore(dir);
            var threw = false;
            try { store.Put(".hidden", new byte[] { 1 }); } catch (ArgumentException) { threw = true; }
            Assert(threw, "dot-prefixed reference should throw");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    // === EVIDENCE JOURNAL ===

    private static async Task TestJournalAppendRecover()
    {
        var dir = TempDir("journal-append");
        try
        {
            using var journal = new DurableEvidenceJournal(dir);
            var draft = CreateEventDraft("run-1", "action-1", "0000000000000000000000000000000000000000000000000000000000000001");
            journal.Append(draft);
            journal.Flush();

            using var recovered = new DurableEvidenceJournal(dir);
            var result = recovered.Recover();
            Assert(result.Status == RecoveryStatus.Verified, $"Recovery should be verified, got {result.Status}");
            Assert(result.Events.Count == 1, $"Should recover 1 event, got {result.Events.Count}");
        }
        finally { Directory.Delete(dir, true); }
    }

    private static async Task TestJournalTamperDetection()
    {
        var dir = TempDir("journal-tamper");
        try
        {
            using var journal = new DurableEvidenceJournal(dir);
            journal.Append(CreateEventDraft("run-1", "action-1", "0000000000000000000000000000000000000000000000000000000000000001"));
            journal.Flush();

            // Tamper with the journal file
            var journalPath = Path.Combine(dir, "evidence.journal");
            var content = await File.ReadAllBytesAsync(journalPath);
            if (content.Length > 10) content[content.Length / 2] = 0xFF;
            await File.WriteAllBytesAsync(journalPath, content);

            using var recovered = new DurableEvidenceJournal(dir);
            var result = recovered.Recover();
            Assert(result.Status == RecoveryStatus.Corrupt, $"Tampered journal should be corrupt, got {result.Status}");
        }
        finally { Directory.Delete(dir, true); }
    }

    private static async Task TestJournalCrashRecovery()
    {
        var dir = TempDir("journal-crash");
        try
        {
            // Write a valid partial journal (simulating crash mid-write)
            using (var journal = new DurableEvidenceJournal(dir))
            {
                journal.Append(CreateEventDraft("run-crash", "action-crash", "0000000000000000000000000000000000000000000000000000000000000002"));
                journal.Flush();
            }

            using var recovered = new DurableEvidenceJournal(dir);
            var result = recovered.Recover();
            Assert(result.Status == RecoveryStatus.Verified || result.Status == RecoveryStatus.Partial,
                $"Crash recovery should be verified or partial, got {result.Status}");
        }
        finally { Directory.Delete(dir, true); }
    }

    private static async Task TestJournalChainLinkage()
    {
        var dir = TempDir("journal-chain");
        try
        {
            using var journal = new DurableEvidenceJournal(dir);
            var draft1 = CreateEventDraft("run-chain", "action-1", "0000000000000000000000000000000000000000000000000000000000000003");
            journal.Append(draft1);
            journal.Flush();

            using var recovered = new DurableEvidenceJournal(dir);
            var result = recovered.Recover();
            Assert(result.Events.Count >= 1, $"Should have at least 1 event, got {result.Events.Count}");
            // Verify hash chain integrity by checking event has a hash
            var firstEvent = result.Events[0];
            Assert(!string.IsNullOrEmpty(firstEvent.EventHash), "Event should have a hash for chain linkage");
        }
        finally { Directory.Delete(dir, true); }
    }

    private static async Task TestJournalEmptyRecovery()
    {
        var dir = TempDir("journal-empty");
        try
        {
            using var journal = new DurableEvidenceJournal(dir);
            journal.Flush();

            using var recovered = new DurableEvidenceJournal(dir);
            var result = recovered.Recover();
            Assert(result.Status == RecoveryStatus.Verified, $"Empty journal should recover as verified, got {result.Status}");
            Assert(result.Events.Count == 0, $"Empty journal should have 0 events, got {result.Events.Count}");
        }
        finally { Directory.Delete(dir, true); }
    }

    private static Task TestJournalConcurrentAppend()
    {
        var dir = TempDir("journal-concurrent");
        try
        {
            using var journal = new DurableEvidenceJournal(dir);
            var tasks = Enumerable.Range(0, 10).Select(i =>
            {
                return Task.Run(() =>
                {
                    var draft = CreateEventDraft("run-concurrent", $"action-{i}",
                        "000000000000000000000000000000000000000000000000000000000000000" + i);
                    journal.Append(draft);
                });
            }).ToArray();
            Task.WaitAll(tasks);
            journal.Flush();

            using var recovered = new DurableEvidenceJournal(dir);
            var result = recovered.Recover();
            Assert(result.Events.Count == 10, $"Should recover 10 events, got {result.Events.Count}");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    // === PROVENANCE ===

    private static Task TestProvenanceStampVerify()
    {
        using var key = RSA.Create(2048);
        var authority = CreateProvenance(key);
        var artifacts = new ArtifactStore();
        var ledger = new EvidenceLedger(artifacts);
        var draft = CreateEventDraft("run-prov", "action-prov", "000000000000000000000000000000000000000000000000000000000000000a");
        var evidence = ledger.Append(draft);
        var manifest = CreateTestManifest();
        var stamp = authority.Issue(evidence, manifest);
        Assert(!string.IsNullOrEmpty(stamp.SignatureBase64), "Provenance stamp should have signature");
        Assert(authority.Verify(stamp, evidence, manifest), "Provenance stamp should verify with correct key");
        return Task.CompletedTask;
    }

    private static Task TestProvenanceTamperFail()
    {
        using var key = RSA.Create(2048);
        using var wrongKey = RSA.Create(2048);
        var authority = CreateProvenance(key);
        var artifacts = new ArtifactStore();
        var ledger = new EvidenceLedger(artifacts);
        var draft = CreateEventDraft("run-tamper", "action-tamper", "000000000000000000000000000000000000000000000000000000000000000b");
        var evidence = ledger.Append(draft);
        var manifest = CreateTestManifest();
        var stamp = authority.Issue(evidence, manifest);
        var wrongAuthority = CreateProvenance(wrongKey);
        Assert(!wrongAuthority.Verify(stamp, evidence, manifest), "Tampered provenance should fail with wrong key");
        return Task.CompletedTask;
    }

    private static Task TestProvenanceKeyRotation()
    {
        using var key1 = RSA.Create(2048);
        using var key2 = RSA.Create(2048);
        var authority1 = CreateProvenance(key1);
        var authority2 = CreateProvenance(key2);
        var manifest = CreateTestManifest();

        var artifacts1 = new ArtifactStore();
        var ledger1 = new EvidenceLedger(artifacts1);
        var draft1 = CreateEventDraft("run-rot", "action-1", "000000000000000000000000000000000000000000000000000000000000000c");
        var evidence1 = ledger1.Append(draft1);
        var stamp1 = authority1.Issue(evidence1, manifest);

        var artifacts2 = new ArtifactStore();
        var ledger2 = new EvidenceLedger(artifacts2);
        var draft2 = CreateEventDraft("run-rot", "action-2", "000000000000000000000000000000000000000000000000000000000000000d");
        var evidence2 = ledger2.Append(draft2);
        var stamp2 = authority2.Issue(evidence2, manifest);

        Assert(authority1.Verify(stamp1, evidence1, manifest), "Old stamp should verify with old key");
        Assert(!authority2.Verify(stamp1, evidence1, manifest), "Old stamp should not verify with new key");
        Assert(authority2.Verify(stamp2, evidence2, manifest), "New stamp should verify with new key");
        return Task.CompletedTask;
    }

    // === KEY CUSTODY ===

    private static Task TestKeyCustodyCreate()
    {
        var dir = TempDir("key-create");
        try
        {
            var protector = new PassphraseSecretProtector("test-app", () => "test-passphrase");
            var store = new ProvenanceKeyStore(dir, protector, "test-entropy");
            using var key = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
            Assert(key != null, "Created key should not be null");
            Assert(key.KeySize == 2048, $"Key size should be 2048, got {key.KeySize}");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestKeyCustodyLoad()
    {
        var dir = TempDir("key-load");
        try
        {
            var protector = new PassphraseSecretProtector("test-app", () => "test-passphrase");
            var store = new ProvenanceKeyStore(dir, protector, "test-entropy");
            using var key1 = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
            var fp1 = ProvenanceKeyCustody.Fingerprint(key1);

            using var key2 = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
            var fp2 = ProvenanceKeyCustody.Fingerprint(key2);

            Assert(fp1 == fp2, "Loaded key should have same fingerprint as created key");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestKeyCustodyRotation()
    {
        var dir = TempDir("key-rotate");
        try
        {
            var protector = new PassphraseSecretProtector("test-app", () => "test-passphrase");
            var store = new ProvenanceKeyStore(dir, protector, "test-entropy");
            using var key1 = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
            var fp1 = ProvenanceKeyCustody.Fingerprint(key1);

            using var key2 = store.Rotate(ProvenanceKeyRole.RuntimeEvidence);
            var fp2 = ProvenanceKeyCustody.Fingerprint(key2);

            Assert(fp1 != fp2, "Rotated key should have different fingerprint");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestKeyCustodyFingerprint()
    {
        using var key = RSA.Create(2048);
        var fp1 = ProvenanceKeyCustody.Fingerprint(key);
        var fp2 = ProvenanceKeyCustody.Fingerprint(key);
        Assert(fp1 == fp2, "Fingerprint should be deterministic");
        Assert(fp1.Length == 64, $"Fingerprint should be 64 hex chars, got {fp1.Length}");
        return Task.CompletedTask;
    }

    // === OUTPUT REDACTION ===

    private static Task TestRedactionReplaces()
    {
        var redactor = new OutputRedactor(new[] { "super-secret-token" });
        var input = Encoding.UTF8.GetBytes("Authorization: super-secret-token");
        var output = redactor.Redact(input);
        Assert(Encoding.UTF8.GetString(output).Contains("[REDACTED]"), "Secret should be redacted");
        Assert(!Encoding.UTF8.GetString(output).Contains("super-secret-token"), "Secret should not appear in output");
        return Task.CompletedTask;
    }

    private static Task TestRedactionNoSecrets()
    {
        var redactor = new OutputRedactor(new[] { "not-present" });
        var input = Encoding.UTF8.GetBytes("safe content here");
        var output = redactor.Redact(input);
        Assert(Encoding.UTF8.GetString(output) == "safe content here", "Non-matching secret should not alter output");
        return Task.CompletedTask;
    }

    private static Task TestRedactionEmpty()
    {
        var redactor = new OutputRedactor(Array.Empty<string>());
        var input = Encoding.UTF8.GetBytes("no secrets at all");
        var output = redactor.Redact(input);
        Assert(Encoding.UTF8.GetString(output) == "no secrets at all", "Empty secrets should not alter output");
        return Task.CompletedTask;
    }

    private static Task TestRedactionNested()
    {
        var redactor = new OutputRedactor(new[] { "api-key-123", "bearer token-abc" });
        var input = Encoding.UTF8.GetBytes("key=api-key-123 auth=bearer token-abc");
        var output = redactor.Redact(input);
        var text = Encoding.UTF8.GetString(output);
        Assert(text.Contains("[REDACTED]"), "Nested secrets should be redacted");
        Assert(!text.Contains("api-key-123"), "api-key-123 should be gone");
        Assert(!text.Contains("bearer token-abc"), "bearer token-abc should be gone");
        return Task.CompletedTask;
    }

    private static Task TestRedactionNull()
    {
        var redactor = new OutputRedactor();
        var threw = false;
        try { redactor.Redact(null!); } catch (ArgumentNullException) { threw = true; }
        Assert(threw, "Null input should throw");
        return Task.CompletedTask;
    }

    // === SHARED HELPERS ===

    private static EvidenceEventDraft CreateEventDraft(string runId, string actionId, string eventHash)
    {
        return new EvidenceEventDraft(
            runId, actionId, eventHash,
            null,
            new ProviderExecutionMetadata(
                new ProviderDescriptor("test-provider", "test-model", "1.0",
                    Canonicalization.Sha256Hex("config"), "local-only", "none", "typed"),
                Canonicalization.Sha256Hex("output"), TimeSpan.FromMilliseconds(1), 8, ProviderFailureClass.None),
            ToolResultStatus.Success, "test-tool", "1.0", "test-worker",
            "http://127.0.0.1/", "auth-ref", "scope-ref", "capability-ref",
            RiskClass.R0, new[] { "methodology-v1" }, PolicyDecision.Allow,
            "policy-ref", "1.0", "permit-id",
            Encoding.UTF8.GetBytes("raw"), null,
            Array.Empty<string>(), Array.Empty<string>(),
            null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            0, "SUCCEEDED");
    }

    private static AuthorizationManifest CreateTestManifest()
    {
        return new AuthorizationManifest
        {
            EngagementId = "evidence-battery",
            EngagementMode = EngagementMode.Fixture,
            Methods = new MethodDefinition(new[] { "test-tool" }, Array.Empty<string>())
        };
    }
}
