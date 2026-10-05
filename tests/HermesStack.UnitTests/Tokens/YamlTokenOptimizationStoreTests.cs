using HermesStack.Domain.Tokens;
using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Tokens;

namespace HermesStack.UnitTests.Tokens;

public sealed class YamlTokenOptimizationStoreTests
{
    [Fact]
    public async Task Policy_is_project_scoped_and_contains_no_metric_content()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            var store = new YamlTokenOptimizationStore(dataRoot);

            await store.SaveAsync(new TokenOptimizationConfiguration(
                "project-a",
                true,
                TokenOptimizationProfile.Balanced,
                [new TokenProviderSelection("rtk", ["claude", "codex"])]));

            var a = await store.GetAsync("project-a");
            var b = await store.GetAsync("project-b");

            Assert.True(a.Enabled);
            Assert.Equal(TokenOptimizationProfile.Balanced, a.Profile);
            Assert.Single(a.EffectiveProviders);
            Assert.False(b.Enabled);
            Assert.Equal(TokenOptimizationProfile.Off, b.Profile);

            var yaml = await File.ReadAllTextAsync(
                Path.Combine(root, "config", "token-optimization.yaml"));
            Assert.Contains("project-a", yaml, StringComparison.Ordinal);
            Assert.DoesNotContain("prompt", yaml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("response", yaml, StringComparison.OrdinalIgnoreCase);
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
