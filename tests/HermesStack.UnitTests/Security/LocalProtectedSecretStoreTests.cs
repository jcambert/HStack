using HermesStack.Domain.Security;
using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Security;

namespace HermesStack.UnitTests.Security;

public sealed class LocalProtectedSecretStoreTests
{
    [Fact]
    public async Task Round_trip_is_project_scoped_and_ciphertext_does_not_contain_plaintext()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            var store = new LocalProtectedSecretStore(dataRoot);
            var reference = new SecretReference("project-a", "OPENAI_API_KEY");
            const string secret = "m4-super-secret-123456789";

            await store.SetAsync(reference, new SecretValue(secret));

            var restored = await store.GetAsync(reference);
            var otherProject = await store.GetAsync(
                new SecretReference("project-b", "OPENAI_API_KEY"));

            Assert.Equal(secret, restored?.Value);
            Assert.Null(otherProject);

            var files = Directory.GetFiles(
                Path.Combine(root, "secrets"),
                "*.json",
                SearchOption.TopDirectoryOnly);
            Assert.Single(files);
            var envelope = await File.ReadAllTextAsync(files[0]);
            Assert.DoesNotContain(secret, envelope, StringComparison.Ordinal);
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
