using HermesStack.Application.Updates;
using HermesStack.Domain.Updates;

namespace HermesStack.UnitTests.Updates;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task Reports_current_update_ahead_and_missing_components()
    {
        var current = new ManagedComponentVersion[]
        {
            new("workspace", "Workspace image", "0.6.0"),
            new("claude", "Claude Code", "2.1.289"),
            new("codex", "Codex", "0.161.0"),
            new("missing", "Missing", "1.0.0")
        };
        var provider = new FakeProvider(
        [
            new("workspace", "Workspace image", "0.7.0"),
            new("claude", "Claude Code", "2.1.289"),
            new("codex", "Codex", "0.160.0")
        ]);

        var result = await new UpdateCheckService(provider).CheckAsync(current);

        Assert.True(result.HasUpdates);
        Assert.True(result.HasUnknowns);
        Assert.Equal(UpdateState.UpdateAvailable, result.Items[0].State);
        Assert.Equal(UpdateState.UpToDate, result.Items[1].State);
        Assert.Equal(UpdateState.Ahead, result.Items[2].State);
        Assert.Equal(UpdateState.Unavailable, result.Items[3].State);
    }

    [Fact]
    public async Task Non_semantic_version_changes_are_reported_without_guessing_order()
    {
        var current = new ManagedComponentVersion[]
        {
            new("tool", "Tool", "release-a")
        };
        var provider = new FakeProvider(
        [
            new("tool", "Tool", "release-b")
        ]);

        var result = await new UpdateCheckService(provider).CheckAsync(current);

        Assert.Equal(UpdateState.Different, result.Items.Single().State);
        Assert.True(result.HasUpdates);
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
