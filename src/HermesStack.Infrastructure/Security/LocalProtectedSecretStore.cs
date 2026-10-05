using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Security;

namespace HermesStack.Infrastructure.Security;

public sealed partial class LocalProtectedSecretStore(IDataRootProvider dataRoot) : ISecretStore
{
    private readonly string _root = Path.Combine(dataRoot.Root, "secrets");

    public async Task SetAsync(
        SecretReference reference,
        SecretValue value,
        CancellationToken cancellationToken = default)
    {
        Validate(reference);
        if (string.IsNullOrEmpty(value.Value))
        {
            throw new ArgumentException("Secret values cannot be empty.", nameof(value));
        }

        Directory.CreateDirectory(_root);
        var plaintext = Encoding.UTF8.GetBytes(value.Value);
        try
        {
            SecretEnvelope envelope;
            if (OperatingSystem.IsWindows())
            {
                var protectedBytes = ProtectedData.Protect(
                    plaintext,
                    Entropy(reference),
                    DataProtectionScope.CurrentUser);
                envelope = new SecretEnvelope
                {
                    Algorithm = "dpapi-current-user",
                    Ciphertext = Convert.ToBase64String(protectedBytes)
                };
            }
            else
            {
                var key = await GetOrCreateMasterKeyAsync(cancellationToken);
                var nonce = RandomNumberGenerator.GetBytes(12);
                var ciphertext = new byte[plaintext.Length];
                var tag = new byte[16];
                using (var aes = new AesGcm(key, tag.Length))
                {
                    aes.Encrypt(nonce, plaintext, ciphertext, tag, Entropy(reference));
                }

                envelope = new SecretEnvelope
                {
                    Algorithm = "aes-256-gcm",
                    Nonce = Convert.ToBase64String(nonce),
                    Ciphertext = Convert.ToBase64String(ciphertext),
                    Tag = Convert.ToBase64String(tag)
                };
                CryptographicOperations.ZeroMemory(key);
            }

            var path = SecretPath(reference);
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(
                temp,
                JsonSerializer.Serialize(envelope),
                cancellationToken);
            File.Move(temp, path, true);
            ProtectFilePermissions(path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task<SecretValue?> GetAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default)
    {
        Validate(reference);
        var path = SecretPath(reference);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var envelope = JsonSerializer.Deserialize<SecretEnvelope>(json)
            ?? throw new InvalidDataException("Secret envelope is invalid.");

        byte[] plaintext;
        if (string.Equals(envelope.Algorithm, "dpapi-current-user", StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException(
                    "This secret was protected with Windows DPAPI and cannot be read on this platform.");
            }

            plaintext = ProtectedData.Unprotect(
                Convert.FromBase64String(envelope.Ciphertext),
                Entropy(reference),
                DataProtectionScope.CurrentUser);
        }
        else if (string.Equals(envelope.Algorithm, "aes-256-gcm", StringComparison.Ordinal))
        {
            var key = await GetOrCreateMasterKeyAsync(cancellationToken);
            var nonce = Convert.FromBase64String(envelope.Nonce
                ?? throw new InvalidDataException("Secret nonce is missing."));
            var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
            var tag = Convert.FromBase64String(envelope.Tag
                ?? throw new InvalidDataException("Secret authentication tag is missing."));
            plaintext = new byte[ciphertext.Length];
            using (var aes = new AesGcm(key, tag.Length))
            {
                aes.Decrypt(nonce, ciphertext, tag, plaintext, Entropy(reference));
            }

            CryptographicOperations.ZeroMemory(key);
        }
        else
        {
            throw new InvalidDataException(
                $"Unsupported secret protection algorithm '{envelope.Algorithm}'.");
        }

        try
        {
            return new SecretValue(Encoding.UTF8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public Task RemoveAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(reference);
        var path = SecretPath(reference);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private async Task<byte[]> GetOrCreateMasterKeyAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, ".master-key");
        if (File.Exists(path))
        {
            var encoded = await File.ReadAllTextAsync(path, cancellationToken);
            var existing = Convert.FromBase64String(encoded.Trim());
            if (existing.Length != 32)
            {
                throw new InvalidDataException("HermesStack secret master key is invalid.");
            }

            return existing;
        }

        var key = RandomNumberGenerator.GetBytes(32);
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, Convert.ToBase64String(key), cancellationToken);
        File.Move(temp, path, false);
        ProtectFilePermissions(path);
        return key;
    }

    private string SecretPath(SecretReference reference)
    {
        var identity = Encoding.UTF8.GetBytes(
            $"{reference.ProjectId}\n{reference.Name}");
        var hash = Convert.ToHexString(SHA256.HashData(identity)).ToLowerInvariant();
        return Path.Combine(_root, hash + ".json");
    }

    private static byte[] Entropy(SecretReference reference) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(
            $"HermesStack:v1:{reference.ProjectId}:{reference.Name}"));

    private static void Validate(SecretReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.ProjectId))
        {
            throw new ArgumentException("Secret project id is required.", nameof(reference));
        }

        if (!SecretNameRegex().IsMatch(reference.Name))
        {
            throw new ArgumentException(
                "Secret names must match ^[A-Z_][A-Z0-9_]{0,127}$.",
                nameof(reference));
        }
    }

    private static void ProtectFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private sealed class SecretEnvelope
    {
        public int Version { get; set; } = 1;
        public string Algorithm { get; set; } = string.Empty;
        public string? Nonce { get; set; }
        public string Ciphertext { get; set; } = string.Empty;
        public string? Tag { get; set; }
    }

    [GeneratedRegex("^[A-Z_][A-Z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretNameRegex();
}
