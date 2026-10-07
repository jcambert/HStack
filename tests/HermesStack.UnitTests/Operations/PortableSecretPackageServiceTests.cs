using HermesStack.Domain.Projects;
using HermesStack.Domain.Security;
using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Operations;
using HermesStack.Infrastructure.Projects;
using HermesStack.Infrastructure.Security;

namespace HermesStack.UnitTests.Operations;

public sealed class PortableSecretPackageServiceTests
{
    [Fact]
    public async Task Encrypted_secret_package_round_trips_into_native_target_store()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var targetRoot = Path.Combine(root, "target");
        var archivePath = Path.Combine(root, "environment.hstack");

        try
        {
            var sourceData = new DefaultDataRootProvider(sourceRoot);
            var sourceProjects = new YamlProjectStore(sourceData);
            var sourcePolicies = new YamlSecretPolicyStore(sourceData);
            var sourceSecrets = new LocalProtectedSecretStore(sourceData);

            await sourceProjects.SaveAsync(
                new ProjectDefinition("demo", "Demo", "/portable/source"));
            await sourcePolicies.SetAsync(
                new SecretPolicy("demo", "API_KEY", ["claude", "codex"]));
            await sourceSecrets.SetAsync(
                new SecretReference("demo", "API_KEY"),
                new SecretValue("portable-super-secret"));

            await new BackupArchiveService(sourceData)
                .ExportAsync(archivePath);
            var exported = await new PortableSecretPackageService(
                sourceProjects,
                sourcePolicies,
                sourceSecrets)
                .ExportAsync(
                    archivePath,
                    "correct horse battery staple");

            Assert.Equal(1, exported);
            Assert.True(
                PortableSecretPackageService.ContainsEncryptedSecrets(
                    archivePath));

            var targetData = new DefaultDataRootProvider(targetRoot);
            await new BackupArchiveService(targetData)
                .ImportAsync(archivePath);

            var targetProjects = new YamlProjectStore(targetData);
            var targetPolicies = new YamlSecretPolicyStore(targetData);
            var targetSecrets = new LocalProtectedSecretStore(targetData);
            var imported = await new PortableSecretPackageService(
                targetProjects,
                targetPolicies,
                targetSecrets)
                .ImportAsync(
                    archivePath,
                    "correct horse battery staple");

            Assert.Equal(1, imported);
            var value = await targetSecrets.GetAsync(
                new SecretReference("demo", "API_KEY"));
            Assert.Equal("portable-super-secret", value?.Value);

            var policies = await targetPolicies.ListAsync("demo");
            Assert.Single(policies);
            Assert.Contains("claude", policies[0].Agents);
            Assert.Contains("codex", policies[0].Agents);
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
    public async Task Wrong_passphrase_fails_closed()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(root, "environment.hstack");

        try
        {
            var sourceData = new DefaultDataRootProvider(
                Path.Combine(root, "source"));
            var sourceProjects = new YamlProjectStore(sourceData);
            var sourcePolicies = new YamlSecretPolicyStore(sourceData);
            var sourceSecrets = new LocalProtectedSecretStore(sourceData);
            await sourceProjects.SaveAsync(
                new ProjectDefinition("demo", "Demo", "/portable/source"));
            await sourcePolicies.SetAsync(
                new SecretPolicy("demo", "API_KEY", ["claude"]));
            await sourceSecrets.SetAsync(
                new SecretReference("demo", "API_KEY"),
                new SecretValue("portable-super-secret"));

            await new BackupArchiveService(sourceData)
                .ExportAsync(archivePath);
            await new PortableSecretPackageService(
                sourceProjects,
                sourcePolicies,
                sourceSecrets)
                .ExportAsync(
                    archivePath,
                    "correct horse battery staple");

            var targetData = new DefaultDataRootProvider(
                Path.Combine(root, "target"));
            var service = new PortableSecretPackageService(
                new YamlProjectStore(targetData),
                new YamlSecretPolicyStore(targetData),
                new LocalProtectedSecretStore(targetData));

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => service.ImportAsync(
                    archivePath,
                    "incorrect horse battery"));
            Assert.Contains("HS8007", exception.Message, StringComparison.Ordinal);
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
