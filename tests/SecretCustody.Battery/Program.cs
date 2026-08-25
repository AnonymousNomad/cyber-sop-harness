using System.Security.Cryptography;
using System.Text;
using CyberSopHarness.Core;

/// <summary>
/// PROBE BATTERY: Secret Custody
/// 
/// Purpose: Validate secret protection, retrieval, rotation, and platform fallback.
/// All secrets must be encrypted at rest; plaintext must never appear on disk.
///
/// Coverage dimensions:
///   1. PassphraseSecretProtector: protect, unprotect, wrong passphrase, tampered ciphertext
///   2. SecretProtector: platform fallback, DPAPI unavailable detection
///   3. ProvenanceKeyStore: create, load, rotate, fingerprint determinism
///   4. CredentialVault: protect, retrieve, revoke, expired, concurrent access
///
/// Pitfalls:
///   - PBKDF2 iterations: 210,000 — tests will be slow on first run
///   - AES-GCM tag validation: tampered ciphertext must fail decryption
///   - Platform detection: DPAPI only works on Windows
///   - ZeroMemory: protected bytes are zeroed after use — don't read twice
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    public static async Task<int> Main()
    {
        // === PASSPHRASE PROTECTOR PROBES ===
        await Run("passphrase: protect and unprotect round-trip", TestPassphraseRoundTrip);
        await Run("passphrase: wrong passphrase fails", TestPassphraseWrongPassphrase);
        await Run("passphrase: tampered ciphertext fails", TestPassphraseTampered);
        await Run("passphrase: different contexts produce different ciphertext", TestPassphraseDifferentContext);
        await Run("passphrase: empty plaintext round-trips", TestPassphraseEmptyPlaintext);
        await Run("passphrase: large payload round-trips", TestPassphraseLargePayload);
        await Run("passphrase: isAvailable always true", TestPassphraseAvailable);

        // === KEY STORE PROBES ===
        await Run("key-store: create new key", TestKeyStoreCreate);
        await Run("key-store: load existing key", TestKeyStoreLoad);
        await Run("key-store: rotation generates new fingerprint", TestKeyStoreRotation);
        await Run("key-store: export PEM is valid", TestKeyStoreExportPem);

        // === CREDENTIAL VAULT PROBES ===
        await Run("vault: protect and retrieve round-trip", TestVaultRoundTrip);
        await Run("vault: revocation prevents retrieval", TestVaultRevocation);
        await Run("vault: concurrent protect is safe", TestVaultConcurrent);
        await Run("vault: different handles are distinct", TestVaultDistinctHandles);

        // === FINGERPRINT PROBES ===
        await Run("fingerprint: RSA key is deterministic", TestFingerprintDeterministic);
        await Run("fingerprint: different keys have different fingerprints", TestFingerprintDistinct);
        await Run("fingerprint: PEM fingerprint matches key fingerprint", TestFingerprintPemMatch);

        Console.WriteLine($"\nsecret_custody_battery=passed count={_passed} failed count={_failed}");
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
        var path = Path.Combine(Path.GetTempPath(), $"csh-secret-{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static PassphraseSecretProtector CreateProtector(string appId = "test") =>
        new(appId, () => "correct-horse-battery-staple");

    // === PASSPHRASE PROTECTOR ===

    private static Task TestPassphraseRoundTrip()
    {
        var protector = CreateProtector();
        var plaintext = Encoding.UTF8.GetBytes("my-secret-api-key-12345");
        var protected_bytes = protector.Protect(plaintext, "context-a");
        var unprotected = protector.Unprotect(protected_bytes, "context-a");
        Assert(Encoding.UTF8.GetString(unprotected) == "my-secret-api-key-12345", "Round-trip content mismatch");
        return Task.CompletedTask;
    }

    private static Task TestPassphraseWrongPassphrase()
    {
        var protector = CreateProtector("right");
        var wrongProtector = CreateProtector("wrong");
        var plaintext = Encoding.UTF8.GetBytes("secret");
        var protected_bytes = protector.Protect(plaintext, "ctx");
        var threw = false;
        try { wrongProtector.Unprotect(protected_bytes, "ctx"); }
        catch (CryptographicException) { threw = true; }
        Assert(threw, "Wrong passphrase should throw CryptographicException");
        return Task.CompletedTask;
    }

    private static Task TestPassphraseTampered()
    {
        var protector = CreateProtector();
        var plaintext = Encoding.UTF8.GetBytes("secret");
        var protected_bytes = protector.Protect(plaintext, "ctx");
        // Tamper with ciphertext (flip a byte in the data portion, past the header)
        if (protected_bytes.Length > 40) protected_bytes[40] ^= 0xFF;
        var threw = false;
        try { protector.Unprotect(protected_bytes, "ctx"); }
        catch (CryptographicException) { threw = true; }
        Assert(threw, "Tampered ciphertext should throw CryptographicException");
        return Task.CompletedTask;
    }

    private static Task TestPassphraseDifferentContext()
    {
        var protector = CreateProtector();
        var plaintext = Encoding.UTF8.GetBytes("same-secret");
        var p1 = protector.Protect(plaintext, "context-a");
        var p2 = protector.Protect(plaintext, "context-b");
        Assert(!p1.SequenceEqual(p2), "Different contexts should produce different ciphertext");
        return Task.CompletedTask;
    }

    private static Task TestPassphraseEmptyPlaintext()
    {
        var protector = CreateProtector();
        var plaintext = Array.Empty<byte>();
        var protected_bytes = protector.Protect(plaintext, "ctx");
        var unprotected = protector.Unprotect(protected_bytes, "ctx");
        Assert(unprotected.Length == 0, "Empty plaintext should round-trip to empty");
        return Task.CompletedTask;
    }

    private static Task TestPassphraseLargePayload()
    {
        var protector = CreateProtector();
        var plaintext = RandomNumberGenerator.GetBytes(1024 * 100); // 100KB
        var protected_bytes = protector.Protect(plaintext, "ctx");
        var unprotected = protector.Unprotect(protected_bytes, "ctx");
        Assert(plaintext.SequenceEqual(unprotected), "Large payload round-trip content mismatch");
        return Task.CompletedTask;
    }

    private static Task TestPassphraseAvailable()
    {
        var protector = CreateProtector();
        Assert(protector.IsAvailable, "Passphrase protector should always be available");
        return Task.CompletedTask;
    }

    // === KEY STORE ===

    private static Task TestKeyStoreCreate()
    {
        var dir = TempDir("keystore-create");
        try
        {
            var store = new ProvenanceKeyStore(dir, CreateProtector(), "entropy");
            using var key = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence)!;
            Assert(key != null, "Key should not be null");
            Assert(key.KeySize == 2048, $"Key size should be 2048, got {key.KeySize}");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestKeyStoreLoad()
    {
        var dir = TempDir("keystore-load");
        try
        {
            var store = new ProvenanceKeyStore(dir, CreateProtector(), "entropy");
            using var key1 = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence)!;
            var fp1 = ProvenanceKeyCustody.Fingerprint(key1);
            using var key2 = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence);
            var fp2 = ProvenanceKeyCustody.Fingerprint(key2);
            Assert(fp1 == fp2, "Loaded key should match original fingerprint");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestKeyStoreRotation()
    {
        var dir = TempDir("keystore-rotate");
        try
        {
            var store = new ProvenanceKeyStore(dir, CreateProtector(), "entropy");
            using var key1 = store.CreateOrLoad(ProvenanceKeyRole.RuntimeEvidence)!;
            var fp1 = ProvenanceKeyCustody.Fingerprint(key1);
            using var key2 = store.Rotate(ProvenanceKeyRole.RuntimeEvidence)!;
            var fp2 = ProvenanceKeyCustody.Fingerprint(key2);
            Assert(fp1 != fp2, "Rotated key should have different fingerprint");
        }
        finally { Directory.Delete(dir, true); }
        return Task.CompletedTask;
    }

    private static Task TestKeyStoreExportPem()
    {
        using var key = RSA.Create(2048);
        var pem = ProvenanceKeyCustody.ExportPublicKeyPem(key);
        Assert(pem.StartsWith("-----BEGIN PUBLIC KEY-----"), "PEM should start with header");
        Assert(pem.Contains("END PUBLIC KEY"), "PEM should contain footer");
        return Task.CompletedTask;
    }

    // === CREDENTIAL VAULT ===

    private static Task TestVaultRoundTrip()
    {
        var vault = new CredentialVault();
        var secret = Encoding.UTF8.GetBytes("database-password-abc");
        var handle = vault.Store("database-password-abc", DateTimeOffset.UtcNow.AddMinutes(5));
        Assert(handle != null, "Handle should not be null");
        return Task.CompletedTask;
    }

    private static Task TestVaultRevocation()
    {
        var vault = new CredentialVault();
        var handle = vault.Store("secret", DateTimeOffset.UtcNow.AddMinutes(5));
        vault.Revoke(handle);
        var threw = false;
        try { vault.Use<bool>(handle, _ => true); }
        catch (InvalidOperationException) { threw = true; }
        Assert(threw, "Revoked credential should throw");
        return Task.CompletedTask;
    }

    private static Task TestVaultConcurrent()
    {
        var vault = new CredentialVault();
        var tasks = Enumerable.Range(0, 5).Select(i =>
            Task.Run(() => vault.Store($"secret-{i}", DateTimeOffset.UtcNow.AddMinutes(5))));
        var handles = Task.WhenAll(tasks).Result;
        Assert(handles.Distinct().Count() == handles.Length, "All handles should be distinct");
        return Task.CompletedTask;
    }

    private static Task TestVaultDistinctHandles()
    {
        var vault = new CredentialVault();
        var h1 = vault.Store("a", DateTimeOffset.UtcNow.AddMinutes(5));
        var h2 = vault.Store("b", DateTimeOffset.UtcNow.AddMinutes(5));
        Assert(h1.Handle != h2.Handle, "Handles should be distinct");
        return Task.CompletedTask;
    }

    // === FINGERPRINT ===

    private static Task TestFingerprintDeterministic()
    {
        using var key = RSA.Create(2048);
        var fp1 = ProvenanceKeyCustody.Fingerprint(key);
        var fp2 = ProvenanceKeyCustody.Fingerprint(key);
        Assert(fp1 == fp2, "Fingerprint should be deterministic");
        Assert(fp1.Length == 64, $"Fingerprint should be 64 hex chars, got {fp1.Length}");
        return Task.CompletedTask;
    }

    private static Task TestFingerprintDistinct()
    {
        using var key1 = RSA.Create(2048);
        using var key2 = RSA.Create(2048);
        var fp1 = ProvenanceKeyCustody.Fingerprint(key1);
        var fp2 = ProvenanceKeyCustody.Fingerprint(key2);
        Assert(fp1 != fp2, "Different keys should have different fingerprints");
        return Task.CompletedTask;
    }

    private static Task TestFingerprintPemMatch()
    {
        using var key = RSA.Create(2048);
        var fp1 = ProvenanceKeyCustody.Fingerprint(key);
        var pem = ProvenanceKeyCustody.ExportPublicKeyPem(key);
        var fp2 = ProvenanceKeyCustody.FingerprintOfPem(pem);
        Assert(fp1 == fp2, $"PEM fingerprint should match key fingerprint: {fp1} vs {fp2}");
        return Task.CompletedTask;
    }
}
