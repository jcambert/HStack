using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Operations;

namespace HermesStack.UnitTests.Operations;

public sealed class BackupArchiveServiceTests
{
    [Fact]
    public async Task Backup_and_restore_preserve_config_certificates_and_project_state()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            Directory.CreateDirectory(dataRoot.ConfigDirectory);
            Directory.CreateDirectory(dataRoot.CorporateCertificatesDirectory);
            Directory.CreateDirectory(
                Path.Combine(dataRoot.GetProjectDataRoot("demo"), "home", ".config", "herdr"));

            await File.WriteAllTextAsync(
                dataRoot.MainConfigFile,
                "schemaVersion: 1\n");
            await File.WriteAllTextAsync(
                Path.Combine(dataRoot.CorporateCertificatesDirectory, "corp.pem"),
                "-----BEGIN CERTIFICATE-----\nTEST\n-----END CERTIFICATE-----\n");
            var stateFile = Path.Combine(
                dataRoot.GetProjectDataRoot("demo"),
                "home",
                ".config",
                "herdr",
                "state.json");
            await File.WriteAllTextAsync(stateFile, "{\"ok\":true}");

            var service = new BackupArchiveService(dataRoot);
            var result = await service.BackupAsync("demo");

            File.Delete(dataRoot.MainConfigFile);
            File.Delete(stateFile);
            await service.RestoreAsync(result.Path);

            Assert.True(File.Exists(dataRoot.MainConfigFile));
            Assert.True(File.Exists(stateFile));
            Assert.True(File.Exists(
                Path.Combine(dataRoot.CorporateCertificatesDirectory, "corp.pem")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Portable_export_excludes_project_state_and_secrets()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            Directory.CreateDirectory(dataRoot.ConfigDirectory);
            Directory.CreateDirectory(Path.Combine(root, "secrets"));
            Directory.CreateDirectory(dataRoot.GetProjectDataRoot("demo"));
            await File.WriteAllTextAsync(dataRoot.MainConfigFile, "schemaVersion: 1\n");
            await File.WriteAllTextAsync(Path.Combine(root, "secrets", "secret.json"), "secret");
            await File.WriteAllTextAsync(
                Path.Combine(dataRoot.GetProjectDataRoot("demo"), "state.txt"),
                "state");

            var output = Path.Combine(root, "portable.hstack");
            await new BackupArchiveService(dataRoot).ExportAsync(output);

            using var archive = System.IO.Compression.ZipFile.OpenRead(output);
            Assert.Contains(archive.Entries, entry => entry.FullName == "config/hstack.yaml");
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("secrets/", StringComparison.Ordinal));
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("data/", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
