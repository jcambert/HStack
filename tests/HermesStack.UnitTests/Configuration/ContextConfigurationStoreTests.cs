using HermesStack.Domain.Context;
using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Configuration;

public sealed class ContextConfigurationStoreTests
{
    [Fact]
    public async Task Defaults_and_project_override_round_trip_in_hstack_yaml()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            await new HStackInitializer(dataRoot).InitializeAsync();
            var store = new HStackConfigStore(dataRoot);

            var defaults = await store.GetAsync("alpha");
            Assert.True(defaults.Enabled);
            Assert.Equal("openviking", defaults.ProviderId);
            Assert.Equal(ContextCaptureMode.Selective, defaults.CaptureMode);
            Assert.Equal(12000, defaults.EffectiveBudget.MaxTokens);

            await store.SaveAsync(defaults with
            {
                Enabled = false,
                CaptureMode = ContextCaptureMode.Manual,
                Budget = new ContextBudgetOptions(4000, 7)
            });

            var loaded = await store.GetAsync("alpha");
            Assert.False(loaded.Enabled);
            Assert.Equal(ContextCaptureMode.Manual, loaded.CaptureMode);
            Assert.Equal(4000, loaded.EffectiveBudget.MaxTokens);
            Assert.Equal(7, loaded.EffectiveBudget.MaxItems);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
