using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Security;

namespace HermesStack.Infrastructure.Operations;

public sealed class PortableSecretPackageService(
    IProjectStore projects,
    ISecretPolicyStore policies,
    ISecretStore secrets)
{
    private const int Iterations = 310_000;
    private static readonly byte[] AssociatedData =
        Encoding.UTF8.GetBytes("HermesStack:portable-secrets:v1");

    public async Task<int> ExportAsync(
        string archivePath,
        string passphrase,
        CancellationToken cancellationToken = default)
    {
        ValidatePassphrase(passphrase);
        var entries = new List<PortableSecretEntry>();

        foreach (var project in await projects.ListAsync(cancellationToken))
        {
            foreach (var policy in await policies.ListAsync(
                project.Id,
                cancellationToken))
            {
                var value = await secrets.GetAsync(
                    new SecretReference(project.Id, policy.Name),
                    cancellationToken);
                if (value is null)
                {
                    continue;
                }

                entries.Add(new PortableSecretEntry(
                    project.Id,
                    policy.Name,
                    value.Value,
                    policy.Agents.ToArray()));
            }
        }

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(
            new PortableSecretDocument(1, entries));
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = DeriveKey(passphrase, salt);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        try
        {
            using (var aes = new AesGcm(key, tag.Length))
            {
                aes.Encrypt(
                    nonce,
                    plaintext,
                    ciphertext,
                    tag,
                    AssociatedData);
            }

            var envelope = new PortableSecretEnvelope(
                1,
                Iterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(ciphertext),
                Convert.ToBase64String(tag));

            using var archive = ZipFile.Open(
                Path.GetFullPath(archivePath),
                ZipArchiveMode.Update);
            archive.GetEntry("secrets.enc")?.Delete();
            var entry = archive.CreateEntry(
                "secrets.enc",
                CompressionLevel.NoCompression);
            await using var stream = entry.Open();
            await JsonSerializer.SerializeAsync(
                stream,
                envelope,
                cancellationToken: cancellationToken);
            return entries.Count;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    public async Task<int> ImportAsync(
        string archivePath,
        string passphrase,
        CancellationToken cancellationToken = default)
    {
        ValidatePassphrase(passphrase);
        using var archive = ZipFile.OpenRead(Path.GetFullPath(archivePath));
        var entry = archive.GetEntry("secrets.enc");
        if (entry is null)
        {
            return 0;
        }

        PortableSecretEnvelope envelope;
        await using (var stream = entry.Open())
        {
            envelope = await JsonSerializer.DeserializeAsync<PortableSecretEnvelope>(
                stream,
                cancellationToken: cancellationToken)
                ?? throw new InvalidDataException(
                    "HS8007: Encrypted secret package is invalid.");
        }

        if (envelope.SchemaVersion != 1 ||
            envelope.Iterations < 200_000)
        {
            throw new InvalidDataException(
                "HS8007: Unsupported encrypted secret package.");
        }

        var salt = Convert.FromBase64String(envelope.Salt);
        var nonce = Convert.FromBase64String(envelope.Nonce);
        var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        var tag = Convert.FromBase64String(envelope.Tag);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            passphrase,
            salt,
            envelope.Iterations,
            HashAlgorithmName.SHA256,
            32);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            try
            {
                using var aes = new AesGcm(key, tag.Length);
                aes.Decrypt(
                    nonce,
                    ciphertext,
                    tag,
                    plaintext,
                    AssociatedData);
            }
            catch (CryptographicException exception)
            {
                throw new InvalidDataException(
                    "HS8007: Secret package passphrase is incorrect or the package was modified.",
                    exception);
            }

            var document = JsonSerializer.Deserialize<PortableSecretDocument>(
                plaintext)
                ?? throw new InvalidDataException(
                    "HS8007: Decrypted secret package is invalid.");
            if (document.SchemaVersion != 1)
            {
                throw new InvalidDataException(
                    "HS8007: Unsupported decrypted secret package.");
            }

            foreach (var item in document.Secrets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await secrets.SetAsync(
                    new SecretReference(item.ProjectId, item.Name),
                    new SecretValue(item.Value),
                    cancellationToken);
                await policies.SetAsync(
                    new SecretPolicy(
                        item.ProjectId,
                        item.Name,
                        item.Agents),
                    cancellationToken);
            }

            return document.Secrets.Count;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    public static bool ContainsEncryptedSecrets(string archivePath)
    {
        using var archive = ZipFile.OpenRead(Path.GetFullPath(archivePath));
        return archive.GetEntry("secrets.enc") is not null;
    }

    private static byte[] DeriveKey(string passphrase, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            passphrase,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            32);

    private static void ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase) ||
            passphrase.Length < 12)
        {
            throw new InvalidOperationException(
                "HS8007: Portable secret passphrase must contain at least 12 characters.");
        }
    }

    private sealed record PortableSecretEnvelope(
        int SchemaVersion,
        int Iterations,
        string Salt,
        string Nonce,
        string Ciphertext,
        string Tag);

    private sealed record PortableSecretDocument(
        int SchemaVersion,
        IReadOnlyList<PortableSecretEntry> Secrets);

    private sealed record PortableSecretEntry(
        string ProjectId,
        string Name,
        string Value,
        IReadOnlyList<string> Agents);
}
