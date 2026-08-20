using System.Security.Cryptography;
using System.Text;

namespace CyberSopHarness.Core;

public interface ISecretProtector
{
    bool IsAvailable { get; }
    byte[] Protect(byte[] plaintext, string context);
    byte[] Unprotect(byte[] protectedBytes, string context);
}

public sealed class WindowsDpapiSecretProtector : ISecretProtector
{
    private readonly byte[] _applicationEntropy;

    public WindowsDpapiSecretProtector(string applicationEntropy)
    {
        if (string.IsNullOrWhiteSpace(applicationEntropy)) throw new ArgumentException("application entropy is required", nameof(applicationEntropy));
        _applicationEntropy = SHA256.HashData(Encoding.UTF8.GetBytes(applicationEntropy));
    }

    public bool IsAvailable => OperatingSystem.IsWindows();

    public byte[] Protect(byte[] plaintext, string context)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI secret protection requires Windows");
        return ProtectedData.Protect(plaintext, EntropyFor(context), DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] protectedBytes, string context)
    {
        ArgumentNullException.ThrowIfNull(protectedBytes);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI secret protection requires Windows");
        return ProtectedData.Unprotect(protectedBytes, EntropyFor(context), DataProtectionScope.CurrentUser);
    }

    private byte[] EntropyFor(string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        var combined = new byte[_applicationEntropy.Length + Encoding.UTF8.GetByteCount(context) + 1];
        _applicationEntropy.CopyTo(combined, 0);
        combined[_applicationEntropy.Length] = 0x1f;
        Encoding.UTF8.GetBytes(context, combined.AsSpan(_applicationEntropy.Length + 1));
        return combined;
    }
}

public sealed class TestSecretProtector : ISecretProtector
{
    private readonly byte[] _masterKey = RandomNumberGenerator.GetBytes(32);

    public bool IsAvailable => true;

    public byte[] Protect(byte[] plaintext, string context)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_masterKey, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return Combine(nonce, tag, ciphertext);
    }

    public byte[] Unprotect(byte[] protectedBytes, string context)
    {
        ArgumentNullException.ThrowIfNull(protectedBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        if (protectedBytes.Length < 12 + 16) throw new CryptographicException("protected blob is too short");
        var nonce = protectedBytes.AsSpan(0, 12).ToArray();
        var tag = protectedBytes.AsSpan(12, 16).ToArray();
        var ciphertext = protectedBytes.AsSpan(28).ToArray();
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(_masterKey, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    private static byte[] Combine(byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, nonce.Length);
        ciphertext.CopyTo(result, nonce.Length + tag.Length);
        return result;
    }
}

public sealed class PersistentSecretStore
{
    private readonly string _directory;
    private readonly ISecretProtector _protector;
    private readonly string _applicationEntropy;

    public PersistentSecretStore(string directory, ISecretProtector protector, string applicationEntropy)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) throw new ArgumentException("secret directory must be an absolute path", nameof(directory));
        if (!protector.IsAvailable) throw new PlatformNotSupportedException("no usable secret protector is available on this platform");
        _directory = directory;
        _protector = protector;
        _applicationEntropy = applicationEntropy;
        Directory.CreateDirectory(directory);
    }

    public void Store(string providerId, string secret)
    {
        var id = ValidateProviderId(providerId);
        if (string.IsNullOrEmpty(secret)) throw new ArgumentException("secret cannot be empty", nameof(secret));
        var plaintext = Encoding.UTF8.GetBytes(secret);
        try
        {
            var protectedBytes = _protector.Protect(plaintext, Context(id));
            var path = GetPath(id);
            var temporaryPath = path + ".tmp";
            File.WriteAllBytes(temporaryPath, protectedBytes);
            using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Flush(true);
            }
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public string Load(string providerId)
    {
        var id = ValidateProviderId(providerId);
        var path = GetPath(id);
        if (!File.Exists(path)) throw new FileNotFoundException("no stored secret for provider", path);
        var protectedBytes = File.ReadAllBytes(path);
        try
        {
            var plaintext = _protector.Unprotect(protectedBytes, Context(id));
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public bool Exists(string providerId)
    {
        var id = ValidateProviderId(providerId);
        return File.Exists(GetPath(id));
    }

    public void Delete(string providerId)
    {
        var id = ValidateProviderId(providerId);
        var path = GetPath(id);
        if (File.Exists(path)) File.Delete(path);
    }

    private string Context(string providerId) => _applicationEntropy + "|" + providerId;

    private string GetPath(string providerId) => Path.Combine(_directory, providerId + ".secret");

    private static string ValidateProviderId(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId) || providerId.Length > 128) throw new ArgumentException("provider id is invalid", nameof(providerId));
        if (!providerId.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_')) throw new ArgumentException("provider id contains unsafe characters", nameof(providerId));
        return providerId;
    }
}