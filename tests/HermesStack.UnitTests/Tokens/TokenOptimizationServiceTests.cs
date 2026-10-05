using HermesStack.Application.Abstractions;
using HermesStack.Application.Tokens;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;
using HermesStack.Domain.Tokens;

namespace HermesStack.UnitTests.Tokens;

public sealed class TokenOptimizationServiceTests
{
    [Fact]
    public async Task Enable_configures_only_selected_agents_and_persists_after_success()
    {
        var orchestrator = new FakeOrchestrator();
        var rtk = new FakeOptimizer("rtk");
        var store = new MemoryTokenStore();
        var metrics = new MemoryMetricStore();
        var sut = new TokenOptimizationService(
            orchestrator,
            new TokenOptimizerRegistry([rtk]),
            store,
            metrics,
            new TokenOptimizationCompatibilityPolicy());

        var configuration = await sut.EnableAsync(
            Plan(),
            "rtk",
            TokenOptimizationProfile.Balanced,
            ["claude", "codex"]);

        Assert.Equal(["claude", "codex"], rtk.ConfiguredAgents);
        Assert.True(configuration.Enabled);
        Assert.Equal("rtk", Assert.Single(configuration.EffectiveProviders).ProviderId);
        Assert.True(orchestrator.UpCalled);
    }

    [Fact]
    public async Task Caveman_requires_aggressive_or_custom_profile()
    {
        var sut = new TokenOptimizationService(
            new FakeOrchestrator(),
            new TokenOptimizerRegistry([new FakeOptimizer("caveman")]),
            new MemoryTokenStore(),
            new MemoryMetricStore(),
            new TokenOptimizationCompatibilityPolicy());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.EnableAsync(
                Plan(),
                "caveman",
                TokenOptimizationProfile.Balanced,
                ["claude"]));

        Assert.Contains("HS5003", exception.Message, StringComparison.Ordinal);
    }

    private static WorkspaceDeploymentPlan Plan() => new(
        new ProjectDefinition("demo", "Demo", "/tmp/demo"),
        "compose",
        "hstack/workspace-full:0.5.0",
        "compose.yaml",
        "/tmp/override.yaml",
        [],
        new Dictionary<string, string>(),
        [],
        new WorkspaceSecurityPolicy(),
        "/tmp/data");

    private sealed class FakeOptimizer(string id) : ITokenOptimizer
    {
        public string Id { get; } = id;
        public string DisplayName => Id;
        public string Version => "1.0.0";
        public IReadOnlySet<string> SupportedAgents { get; } =
            new HashSet<string>(["claude", "codex", "hermes", "opencode"], StringComparer.OrdinalIgnoreCase);
        public bool SupportsGainMetrics => false;
        public List<string> ConfiguredAgents { get; } = [];

        public Task ConfigureAsync(
            WorkspaceDeploymentPlan plan,
            string agentId,
            CancellationToken cancellationToken = default)
        {
            ConfiguredAgents.Add(agentId);
            return Task.CompletedTask;
        }

        public Task DisableAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TokenOptimizerHealth> InspectAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TokenOptimizerHealth(Id, Id, Version, true, "ok"));

        public Task<TokenGainMetrics> ReadGainAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TokenGainMetrics(
                Id,
                TokenMetricEvidence.Unavailable,
                null, null, null, null, "none"));
    }

    private sealed class MemoryTokenStore : ITokenOptimizationStore
    {
        private readonly Dictionary<string, TokenOptimizationConfiguration> _items =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<TokenOptimizationConfiguration> GetAsync(
            string projectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _items.TryGetValue(projectId, out var value)
                    ? value
                    : new TokenOptimizationConfiguration(
                        projectId,
                        false,
                        TokenOptimizationProfile.Off,
                        []));

        public Task SaveAsync(
            TokenOptimizationConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            _items[configuration.ProjectId] = configuration;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryMetricStore : ITokenMetricStore
    {
        public Task AppendAsync(
            TokenMetricRecord record,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TokenMetricRecord>> ReadAsync(
            string projectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TokenMetricRecord>>([]);
    }

    private sealed class FakeOrchestrator : IWorkspaceOrchestrator
    {
        public string Id => "fake";
        public string DisplayName => "Fake";
        public bool UpCalled { get; private set; }

        public Task<OrchestratorAvailability> DetectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OrchestratorAvailability(true));

        public Task<WorkspaceDeploymentPreview> PreviewAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceDeploymentPreview("fake", "ok"));

        public Task UpAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default)
        {
            UpCalled = true;
            return Task.CompletedTask;
        }

        public Task DownAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RestartAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<WorkspaceStatus> GetStatusAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceStatus(WorkspaceState.Stopped));

        public Task<int> ExecAsync(
            WorkspaceExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<WorkspaceExecutionResult> ExecCaptureAsync(
            WorkspaceExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceExecutionResult(0, string.Empty, string.Empty));

        public Task<int> StreamLogsAsync(
            WorkspaceLogRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
