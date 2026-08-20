using System.Security.Cryptography;

namespace CyberSopHarness.Core;

public delegate T SecretUse<T>(ReadOnlySpan<byte> secret);

public sealed class CredentialVault : IDisposable
{
    private sealed class SecretRecord
    {
        public required byte[] Nonce { get; init; }
        public required byte[] Ciphertext { get; init; }
        public required byte[] Tag { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public bool Revoked { get; set; }
    }

    private readonly byte[] _masterKey = RandomNumberGenerator.GetBytes(32);
    private readonly Dictionary<string, SecretRecord> _records = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public CredentialHandle Store(string secret, DateTimeOffset expiresAt)
    {
        var now = AuthoritativeClock.UtcNow;
        if (string.IsNullOrEmpty(secret)) throw new ArgumentException("secret cannot be empty", nameof(secret));
        if (expiresAt <= now) throw new ArgumentException("credential must expire in the future", nameof(expiresAt));
        var handle = "cred_" + Guid.NewGuid().ToString("N");
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = System.Text.Encoding.UTF8.GetBytes(secret);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(_masterKey, tag.Length);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
        lock (_gate) _records.Add(handle, new SecretRecord { Nonce = nonce, Ciphertext = ciphertext, Tag = tag, ExpiresAt = expiresAt });
        return new CredentialHandle(handle, expiresAt);
    }

    public T Use<T>(CredentialHandle handle, SecretUse<T> action)
    {
        var now = AuthoritativeClock.UtcNow;
        byte[] nonce;
        byte[] ciphertext;
        byte[] tag;
        DateTimeOffset expiresAt;
        lock (_gate)
        {
            if (!_records.TryGetValue(handle.Handle, out var record) || record.Revoked) throw new KeyNotFoundException("credential handle not found or revoked");
            if (record.ExpiresAt <= now) throw new InvalidOperationException("credential handle expired");
            nonce = record.Nonce.ToArray();
            ciphertext = record.Ciphertext.ToArray();
            tag = record.Tag.ToArray();
            expiresAt = record.ExpiresAt;
        }
        if (expiresAt <= now) throw new InvalidOperationException("credential handle expired");
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(_masterKey, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return action(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    public void Revoke(CredentialHandle handle)
    {
        lock (_gate)
        {
            if (!_records.Remove(handle.Handle, out var record)) return;
            record.Revoked = true;
            CryptographicOperations.ZeroMemory(record.Ciphertext);
            CryptographicOperations.ZeroMemory(record.Tag);
            CryptographicOperations.ZeroMemory(record.Nonce);
        }
    }

    public void RevokeAll()
    {
        lock (_gate)
        {
            foreach (var record in _records.Values)
            {
                record.Revoked = true;
                CryptographicOperations.ZeroMemory(record.Ciphertext);
                CryptographicOperations.ZeroMemory(record.Tag);
                CryptographicOperations.ZeroMemory(record.Nonce);
            }
            _records.Clear();
        }
    }

    public int Count
    {
        get { lock (_gate) return _records.Count; }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var record in _records.Values)
            {
                record.Revoked = true;
                CryptographicOperations.ZeroMemory(record.Ciphertext);
                CryptographicOperations.ZeroMemory(record.Tag);
                CryptographicOperations.ZeroMemory(record.Nonce);
            }
            _records.Clear();
        }
        CryptographicOperations.ZeroMemory(_masterKey);
    }
}
