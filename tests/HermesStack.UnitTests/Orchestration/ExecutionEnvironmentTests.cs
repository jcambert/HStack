using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;
using HermesStack.Docker.Compose;

namespace HermesStack.UnitTests.Orchestration;

public sealed class ExecutionEnvironmentTests
{
    [Fact]
    public async Task Exec_passes_secret_by_process_environment_without_putting_value_in_arguments()
    {
        var runner = new RecordingProcessRunner();
        var orchestrator = new DockerComposeWorkspaceOrchestrator(
            runner,
            new ComposeOverrideWriter());
        var plan = CreatePlan();

        await orchestrator.ExecCaptureAsync(
            new WorkspaceExecutionRequest(
                plan,
                ["codex", "--version"],
                Interactive: false,
                Environment: new Dictionary<string, string>
                {
                    ["OPENAI_API_KEY"] = "super-secret-value"
                }));

        Assert.NotNull(runner.LastRequest);
        Assert.Equal(
            "super-secret-value",
            runner.LastRequest.Environment?["OPENAI_API_KEY"]);
        Assert.Contains("OPENAI_API_KEY", runner.LastRequest.Arguments);
        Assert.DoesNotContain("super-secret-value", runner.LastRequest.Arguments);
    }

    private static WorkspaceDeploymentPlan CreatePlan()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        return new WorkspaceDeploymentPlan(
            new ProjectDefinition("demo", "Demo", root),
            "compose",
            "hstack/workspace-full:0.4.0",
            "compose.yaml",
            Path.Combine(root, "compose.override.yaml"),
            [],
            new Dictionary<string, string>(),
            [],
            new WorkspaceSecurityPolicy(),
            root);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public ProcessRequest? LastRequest { get; private set; }

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }
}
