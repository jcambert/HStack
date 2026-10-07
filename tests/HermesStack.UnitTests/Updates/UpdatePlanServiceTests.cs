using HermesStack.Application.Updates;
using HermesStack.Domain.Updates;

namespace HermesStack.UnitTests.Updates;

public sealed class UpdatePlanServiceTests
{
    [Fact]
    public async Task Plan_orders_backup_before_workspace_recreation_and_health_before_commit()
    {
        var provider = new FakeProvider(
        [
            new("workspace", "Workspace image", "0.7.0")
        ]);
        var current = new ManagedComponentVersion[]
        {
            new("workspace", "Workspace image", "0.6.0")
        };

        var plan = await new UpdatePlanService(
            new UpdateCheckService(provider))
            .CreateAsync(current);

        Assert.True(plan.HasChanges);
        Assert.Equal("backup", plan.Steps[3].Id);
        Assert.Equal("recreate", plan.Steps[7].Id);
        Assert.Equal("health", plan.Steps[8].Id);
        Assert.Equal("commit", plan.Steps[9].Id);
        Assert.True(plan.Steps.Single(step => step.Id == "backup").MutatesState);
        Assert.False(plan.Steps.Single(step => step.Id == "health").MutatesState);
    }

    private sealed class FakeProvider(
        IReadOnlyList<ManagedComponentVersion> available) : IUpdateMetadataProvider
    {
        public string Source => "https://updates.example.test/toolchain.lock.yaml";

        public Task<IReadOnlyList<ManagedComponentVersion>> GetAvailableVersionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(available);
    }
}
