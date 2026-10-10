using HermesStack.Application.Abstractions;
using HermesStack.Application.Orchestration;
using HermesStack.Docker.Execution;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;

namespace HermesStack.UnitTests.Orchestration;

public sealed class NativeContainerExecutionProviderTests
{
    [Theory]
    [InlineData("compose")]
    [InlineData("aspire")]
    public async Task Native_provider_preserves_existing_orchestrator_lifecycle(string orchestratorId)
    {
        var compose = new FakeOrchestrator("compose");
        var aspire = new FakeOrchestrator("aspire");
        var registry = new WorkspaceOrchestratorRegistry([compose, aspire]);
        var runner = new FakeRunner();
        var provider = new NativeContainerExecutionProvider(registry, runner);
        var routed = new RoutedWorkspaceOrchestrator(registry, provider);
        var workspace = Plan(orchestratorId);

        var preview = await routed.PreviewAsync(workspace);
        await routed.UpAsync(workspace);
        var state = await routed.GetStatusAsync(workspace);
        await routed.RestartAsync(workspace);
        await routed.DownAsync(workspace);
        var backend = orchestratorId == "compose" ? compose : aspire;

        Assert.Equal("existing preview", preview.Summary);
        Assert.Equal(WorkspaceState.Running, state.State);
        Assert.Equal(1, backend.UpCount);
        Assert.Equal(1, backend.RestartCount);
        Assert.Equal(1, backend.DownCount);
        Assert.Equal(0, runner.RunCount); // No extra Docker probes or double create on normal path.
    }

    [Fact]
    public async Task Rejects_unsafe_settings_before_any_lifecycle_side_effect()
    {
        var backend = new FakeOrchestrator("compose");
        var provider = new NativeContainerExecutionProvider(
            new WorkspaceOrchestratorRegistry([backend]),
            new FakeRunner());
        var routed = new RoutedWorkspaceOrchestrator(
            new WorkspaceOrchestratorRegistry([backend]), provider);
        var baseline = Plan("compose");

        var cases = new (string Name, WorkspaceDeploymentPlan Plan)[]
        {
            ("non-privileged", baseline with { Security = baseline.Security with { Privileged = true } }),
            ("no-new-privileges", baseline with { Security = baseline.Security with { NoNewPrivileges = false } }),
            ("drop all Linux capabilities", baseline with { Security = baseline.Security with { DropAllCapabilities = false } }),
            ("read-only root", baseline with { Security = baseline.Security with { ReadOnlyRoot = false } }),
            ("no host namespace", baseline with { Security = baseline.Security with { HostNetwork = true } }),
            ("no host namespace", baseline with { Security = baseline.Security with { HostPid = true } }),
            ("no host namespace", baseline with { Security = baseline.Security with { HostIpc = true } }),
            ("loopback-only", baseline with { Ports = [new ProjectPort(3000, 3000, "0.0.0.0")] }),
            ("Docker daemon", baseline with { Mounts = [new WorkspaceMount("/var/run/docker.sock", "/docker.sock", false, "bad")] }),
            ("Docker daemon", baseline with { Mounts = [new WorkspaceMount("/tmp/good", "/var/run/docker.sock", false, "bad")] }),
            ("host filesystem root", baseline with { Mounts = [new WorkspaceMount("/", "/host", false, "bad")] }),
            ("host filesystem root", baseline with { Mounts = [new WorkspaceMount(@"C:\", "/host", false, "bad")] }),
            ("isolated project", baseline with { Project = baseline.Project with { StateScope = "shared" } }),
            ("supported Compose/Aspire", baseline with { OrchestratorId = "unknown-provider" })
        };

        foreach (var item in cases)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => routed.UpAsync(item.Plan));
            Assert.Contains("HS2301", exception.Message, StringComparison.Ordinal);
            Assert.Contains(item.Name, exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(0, backend.UpCount);
    }

    [Fact]
    public async Task Teardown_remains_possible_for_a_now_invalid_policy()
    {
        var backend = new FakeOrchestrator("compose");
        var registry = new WorkspaceOrchestratorRegistry([backend]);
        var routed = new RoutedWorkspaceOrchestrator(
            registry, new NativeContainerExecutionProvider(registry, new FakeRunner()));
        var bad = Plan("compose") with
        {
            Security = new WorkspaceSecurityPolicy(Privileged: true)
        };

        await routed.DownAsync(bad);
        Assert.Equal(1, backend.DownCount);
        Assert.Equal(0, backend.UpCount);
    }

    [Fact]
    public async Task Cannot_create_with_foreign_provider_or_reused_insecure_plan()
    {
        var backend = new FakeOrchestrator("compose");
        var provider = new NativeContainerExecutionProvider(
            new WorkspaceOrchestratorRegistry([backend]), new FakeRunner());
        var safe = await provider.PlanAsync(Plan("compose"));
        var foreign = safe with { ProviderId = "dagger" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.CreateAsync(foreign));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.DestroyAsync(
                new ExecutionEnvironmentHandle("devpod", safe.Workspace)));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.CreateAsync(safe with
            {
                Workspace = safe.Workspace with
                {
                    Security = new WorkspaceSecurityPolicy(HostNetwork: true)
                }
            }));
        Assert.Equal(0, backend.UpCount);
        Assert.Equal(0, backend.DownCount);
    }

    [Fact]
    public async Task Probe_is_explicit_and_fail_closed_when_docker_is_missing()
    {
        var registry = new WorkspaceOrchestratorRegistry([new FakeOrchestrator("compose")]);
        var healthy = new FakeRunner();
        var provider = new NativeContainerExecutionProvider(registry, healthy);
        var availability = await provider.DetectAsync();

        Assert.True(availability.IsAvailable);
        Assert.Equal("27.5.1", availability.Version);
        Assert.Equal(1, healthy.RunCount);
        Assert.Equal("docker", healthy.Last?.Executable);

        var missing = new NativeContainerExecutionProvider(
            registry, new FakeRunner(success: false));
        var unavailable = await missing.DetectAsync();
        Assert.False(unavailable.IsAvailable);
    }

    [Fact]
    public async Task Native_provider_does_not_change_workspace_plan_schema_or_default_state()
    {
        var backend = new FakeOrchestrator("compose");
        var provider = new NativeContainerExecutionProvider(
            new WorkspaceOrchestratorRegistry([backend]), new FakeRunner());
        var plan = Plan("compose");
        var result = await provider.PlanAsync(plan);

        Assert.Same(plan, result.Workspace);
        Assert.Equal("native-container", result.ProviderId);
        Assert.True(result.Capabilities.IsSupported);
        Assert.Contains(result.Capabilities.Notes, x => x.Contains("existing Compose/Aspire", StringComparison.Ordinal));
        Assert.Contains(plan.Mounts, m => m.Target == "/workspace" && !m.ReadOnly);
    }

    private static WorkspaceDeploymentPlan Plan(string orchestratorId)
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-m9", Guid.NewGuid().ToString("N"));
        return new WorkspaceDeploymentPlan(
            new ProjectDefinition("demo", "Demo", root),
            orchestratorId,
            "hstack/workspace-full:0.8.0",
            "compose.yaml",
            Path.Combine(root, "compose.override.yaml"),
            [
                new WorkspaceMount(root, "/workspace", false, "project"),
                new WorkspaceMount(Path.Combine(root, "codex"), "/home/hstack/.codex", false, "agent-state:codex")
            ],
            new Dictionary<string, string> { ["CODEX_HOME"] = "/home/hstack/.codex" },
            [],
            new WorkspaceSecurityPolicy(),
            root);
    }

    private sealed class FakeRunner(bool success = true) : IProcessRunner
    {
        public int RunCount { get; private set; }
        public ProcessRequest? Last { get; private set; }
        public Task<ProcessResult> RunAsync(
            ProcessRequest request, CancellationToken cancellationToken = default)
        {
            RunCount++;
            Last = request;
            return Task.FromResult(success
                ? new ProcessResult(0, "27.5.1\n", "")
                : new ProcessResult(1, "", "Docker daemon unavailable"));
        }
    }

    private sealed class FakeOrchestrator(string id) : IWorkspaceOrchestrator
    {
        public string Id => id;
        public string DisplayName => id;
        public int UpCount { get; private set; }
        public int DownCount { get; private set; }
        public int RestartCount { get; private set; }

        public Task<OrchestratorAvailability> DetectAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new OrchestratorAvailability(true));

        public Task<WorkspaceDeploymentPreview> PreviewAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceDeploymentPreview(Id, "existing preview"));

        public Task UpAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default)
        {
            UpCount++;
            return Task.CompletedTask;
        }

        public Task DownAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default)
        {
            DownCount++;
            return Task.CompletedTask;
        }

        public Task RestartAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default)
        {
            RestartCount++;
            return Task.CompletedTask;
        }

        public Task<WorkspaceStatus> GetStatusAsync(
            WorkspaceDeploymentPlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceStatus(WorkspaceState.Running));

        public Task<int> ExecAsync(
            WorkspaceExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<WorkspaceExecutionResult> ExecCaptureAsync(
            WorkspaceExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceExecutionResult(0, "", ""));

        public Task<int> StreamLogsAsync(
            WorkspaceLogRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
