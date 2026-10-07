using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Updates;

namespace HermesStack.UnitTests.Updates;

public sealed class ToolchainUpdateMetadataProviderTests
{
    [Fact]
    public void Rejects_non_https_update_metadata_sources()
    {
        using var client = new HttpClient();

        var exception = Assert.Throws<InvalidDataException>(() =>
            new ToolchainUpdateMetadataProvider(
                client,
                new ToolchainLockService(),
                new Uri("http://updates.example.test/toolchain.lock.yaml")));

        Assert.Contains("HS7002", exception.Message, StringComparison.Ordinal);
    }
}
