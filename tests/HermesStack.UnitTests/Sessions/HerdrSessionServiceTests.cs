using HermesStack.Application.Abstractions;
using HermesStack.Application.Agents;
using HermesStack.Application.Sessions;
using HermesStack.Domain.Agents;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;

namespace HermesStack.UnitTests.Sessions;

public sealed class HerdrSessionServiceTests
{
    [Fact]
    public async Task Ensure_installs_integrations_starts_server_and_creates_one_project_workspace()
    {
        var orchestrator = new RecordingOrchestrator();
        var service = new HerdrSessionService(
            orchestrator,
            new AgentHarnessRegistry([new StubHarness("claude"), new StubHarness("codex"), new StubHarness("hermes"), new StubHarness("opencode")]));

        var session = await service.EnsureAsync(CreatePlan());

        Assert.Equal("hstack-demo", session.SessionName);
        Assert.Equal("hstack:demo", session.WorkspaceLabel);
        Assert.Equal("w1", session.WorkspaceId);
        Assert.Contains(orchestrator.Executions, request =>
            request.Detached && request.Command.SequenceEqual(["herdr", "server"]));
        foreach (var target in new[] { "claude", "codex", "hermes", "opencode" })
        {
            Assert.Contains(orchestrator.Captures, request =>
                request.Command.SequenceEqual(["herdr", "integration", "install", target]));
        }

        Assert.Contains(orchestrator.Captures, request =>
            request.Command.SequenceEqual([
                "herdr", "workspace", "create", "--cwd", "/workspace",
                "--label", "hstack:demo", "--no-focus"
            ]));
    }

    [Fact]
    public async Task Ensure_reuses_restored_workspace()
    {
        var orchestrator = new RecordingOrchestrator { WorkspaceExists = true, ServerRunning = true };
        var service = new HerdrSessionService(
            orchestrator,
            new AgentHarnessRegistry([new StubHarness("claude")]));

        var session = await service.EnsureAsync(CreatePlan());

        Assert.Equal("restored-w1", session.WorkspaceId);
        Assert.DoesNotContain(orchestrator.Captures, request =>
            request.Command.Contains("create", StringComparer.Ordinal));
    }

    [Fact]
    public async Task Run_agent_creates_tab_then_uses_herdr_agent_start_without_shell_interpolation()
    {
        var orchestrator = new RecordingOrchestrator { WorkspaceExists = true, ServerRunning = true };
        var service = new HerdrSessionService(
            orchestrator,
            new AgentHarnessRegistry([new StubHarness("codex")]));

        var result = await service.RunAgentAsync(
            CreatePlan(),
            "codex",
            "reviewer",
            ["-m", "gpt-5.4", "value with spaces"]);

        Assert.True(result.IsSuccess);
        Assert.Contains(orchestrator.Captures, request =>
            request.Command.SequenceEqual([
                "herdr", "agent", "start", "reviewer",
                "--kind", "codex",
                "--pane", "w1:p2",
                "--timeout", "30000",
                "--", "-m", "gpt-5.4", "value with spaces"
            ]));
    }

    [Fact]
    public async Task Invalid_agent_name_is_rejected_before_topology_changes()
    {
        var orchestrator = new RecordingOrchestrator { WorkspaceExists = true, ServerRunning = true };
        var service = new HerdrSessionService(
            orchestrator,
            new AgentHarnessRegistry([new StubHarness("codex")]));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAgentAsync(CreatePlan(), "codex", "Bad Name", []));
    }

    private static WorkspaceDeploymentPlan CreatePlan()
    {
        var project = new ProjectDefinition("demo", "Demo", "/tmp/demo");
        return new WorkspaceDeploymentPlan(
            project,
            "compose",
            "hstack/workspace-full:0.3.0",
            "/tmp/compose.yaml",
            "/tmp/override.yaml",
            [],
            new Dictionary<string, string> { ["HERDR_SESSION"] = "hstack-demo" },
            [],
            new WorkspaceSecurityPolicy(),
            "/tmp/hstack-data");
    }

    private sealed class StubHarness(string id) : IAgentHarness
    {
        public string Id => id;
        public string DisplayName => id;

        public Task<AgentInstallationInfo> InspectAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentInstallationInfo(Id, DisplayName, true, "test"));

        public Task<int> LaunchAsync(AgentLaunchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task ConfigureAsync(AgentConfigureRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingOrchestrator : IWorkspaceOrchestrator
    {
        public string Id => "recording";
        public string DisplayName => "Recording";
        public bool ServerRunning { get; set; }
        public bool WorkspaceExists { get; set; }
        public List<WorkspaceExecutionRequest> Executions { get; } = [];
        public List<WorkspaceExecutionRequest> Captures { get; } = [];

        public Task<OrchestratorAvailability> DetectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OrchestratorAvailability(true, "test"));
        public Task<WorkspaceDeploymentPreview> PreviewAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceDeploymentPreview("recording", "test"));
        public Task UpAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task DownAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task RestartAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<WorkspaceStatus> GetStatusAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceStatus(WorkspaceState.Running));
        public Task<int> StreamLogsAsync(WorkspaceLogRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> ExecAsync(WorkspaceExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Executions.Add(request);
            if (request.Detached && request.Command.SequenceEqual(["herdr", "server"]))
            {
                ServerRunning = true;
            }
            return Task.FromResult(0);
        }

        public Task<WorkspaceExecutionResult> ExecCaptureAsync(WorkspaceExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Captures.Add(request);
            var command = request.Command;
            if (command.SequenceEqual(["herdr", "workspace", "list"]))
            {
                if (!ServerRunning)
                {
                    return Task.FromResult(new WorkspaceExecutionResult(
                        1, "", """{"error":{"code":"server_not_running"}}"""));
                }

                var json = WorkspaceExists
                    ? """{"result":{"workspaces":[{"workspace_id":"restored-w1","label":"hstack:demo"}]}}"""
                    : """{"result":{"workspaces":[]}}""";
                return Task.FromResult(new WorkspaceExecutionResult(0, json, ""));
            }

            if (command.Contains("workspace", StringComparer.Ordinal) &&
                command.Contains("create", StringComparer.Ordinal))
            {
                WorkspaceExists = true;
                return Task.FromResult(new WorkspaceExecutionResult(
                    0,
                    """{"result":{"workspace":{"workspace_id":"w1","label":"hstack:demo"},"root_pane":{"pane_id":"w1:p1"}}}""",
                    ""));
            }

            if (command.Contains("tab", StringComparer.Ordinal) &&
                command.Contains("create", StringComparer.Ordinal))
            {
                return Task.FromResult(new WorkspaceExecutionResult(
                    0,
                    """{"result":{"tab":{"tab_id":"w1:t2"},"root_pane":{"pane_id":"w1:p2"}}}""",
                    ""));
            }

            return Task.FromResult(new WorkspaceExecutionResult(0, """{"result":{}}""", ""));
        }
    }
}
