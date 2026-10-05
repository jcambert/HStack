using HermesStack.Application.Abstractions;
using HermesStack.Application.Agents;
using HermesStack.Domain.Agents;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;

namespace HermesStack.UnitTests.Agents;

public sealed class AgentHarnessTests
{
    [Fact]
    public async Task Authentication_uses_the_upstream_agent_flow()
    {
        var orchestrator = new RecordingOrchestrator();
        var cases = new (IAgentHarness Harness, string[] Expected)[]
        {
            (new ClaudeCodeHarness(orchestrator), ["claude", "auth", "login"]),
            (new CodexHarness(orchestrator), ["codex", "login"]),
            (new HermesAgentHarness(orchestrator), ["hermes", "setup"]),
            (new OpenCodeHarness(orchestrator), ["opencode", "auth", "login"])
        };

        foreach (var (harness, expected) in cases)
        {
            var exitCode = await harness.LaunchAsync(
                new AgentLaunchRequest(CreatePlan(), AgentLaunchMode.Authenticate));

            Assert.Equal(0, exitCode);
            Assert.NotNull(orchestrator.LastExecution);
            Assert.Equal(expected, orchestrator.LastExecution.Command.ToArray());
        }
    }

    [Fact]
    public async Task Run_passes_arguments_without_shell_interpolation()
    {
        var orchestrator = new RecordingOrchestrator();
        var harness = new CodexHarness(orchestrator);
        var plan = CreatePlan();

        await harness.LaunchAsync(
            new AgentLaunchRequest(
                plan,
                AgentLaunchMode.Run,
                ["--version", "value with spaces"]));

        Assert.NotNull(orchestrator.LastExecution);
        Assert.Equal(
            ["codex", "--version", "value with spaces"],
            orchestrator.LastExecution.Command.ToArray());
    }

    [Fact]
    public async Task Configure_creates_only_project_scoped_agent_state()
    {
        var plan = CreatePlan();
        try
        {
            var orchestrator = new RecordingOrchestrator();
            IAgentHarness[] harnesses =
            [
                new ClaudeCodeHarness(orchestrator),
                new CodexHarness(orchestrator),
                new HermesAgentHarness(orchestrator),
                new OpenCodeHarness(orchestrator)
            ];

            foreach (var harness in harnesses)
            {
                await harness.ConfigureAsync(new AgentConfigureRequest(plan));
            }

            Assert.True(Directory.Exists(Path.Combine(plan.ProjectDataRoot, "claude")));
            Assert.Contains(
                "cli_auth_credentials_store = \"file\"",
                await File.ReadAllTextAsync(Path.Combine(plan.ProjectDataRoot, "codex", "config.toml")),
                StringComparison.Ordinal);
            Assert.Contains(
                "backend: local",
                await File.ReadAllTextAsync(Path.Combine(plan.ProjectDataRoot, "hermes", "config.yaml")),
                StringComparison.Ordinal);
            Assert.Contains(
                "\"autoupdate\": false",
                await File.ReadAllTextAsync(Path.Combine(plan.ProjectDataRoot, "opencode", "config", "opencode.json")),
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(plan.ProjectDataRoot))
            {
                Directory.Delete(plan.ProjectDataRoot, recursive: true);
            }
        }
    }

    private static WorkspaceDeploymentPlan CreatePlan()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "project");
        Directory.CreateDirectory(projectPath);

        return new WorkspaceDeploymentPlan(
            new ProjectDefinition("demo", "Demo", projectPath),
            "compose",
            "hstack/workspace-full:0.2.0",
            "compose.yaml",
            Path.Combine(root, "compose.override.yaml"),
            [],
            new Dictionary<string, string>(),
            [],
            new WorkspaceSecurityPolicy(),
            root);
    }
}

internal sealed class RecordingOrchestrator : IWorkspaceOrchestrator
{
    public string Id => "recording";
    public string DisplayName => "Recording";
    public WorkspaceExecutionRequest? LastExecution { get; private set; }

    public Task<OrchestratorAvailability> DetectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new OrchestratorAvailability(true, "test"));

    public Task<WorkspaceDeploymentPreview> PreviewAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkspaceDeploymentPreview(DisplayName, "test"));

    public Task UpAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DownAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RestartAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<WorkspaceStatus> GetStatusAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkspaceStatus(WorkspaceState.Running));

    public Task<int> ExecAsync(
        WorkspaceExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        LastExecution = request;
        return Task.FromResult(0);
    }

    public Task<WorkspaceExecutionResult> ExecCaptureAsync(
        WorkspaceExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        LastExecution = request;
        return Task.FromResult(new WorkspaceExecutionResult(0, "1.0.0", string.Empty));
    }

    public Task<int> StreamLogsAsync(
        WorkspaceLogRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
