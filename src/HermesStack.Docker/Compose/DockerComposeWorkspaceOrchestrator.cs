using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;

namespace HermesStack.Docker.Compose;

public sealed class DockerComposeWorkspaceOrchestrator(IProcessRunner processRunner, ComposeOverrideWriter overrideWriter) : IWorkspaceOrchestrator
{
    public string Id => "compose";
    public string DisplayName => "Docker Compose";

    public async Task<OrchestratorAvailability> DetectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await processRunner.RunAsync(new ProcessRequest("docker", ["compose", "version", "--short"]), cancellationToken);
            return result.IsSuccess
                ? new OrchestratorAvailability(true, result.StandardOutput.Trim())
                : new OrchestratorAvailability(false, Reason: result.StandardError.Trim());
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new OrchestratorAvailability(false, Reason: exception.Message);
        }
    }

    public async Task<WorkspaceDeploymentPreview> PreviewAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default)
    {
        await overrideWriter.WriteAsync(plan, cancellationToken);
        var result = await processRunner.RunAsync(new ProcessRequest("docker", ComposeArgs(plan, "config")), cancellationToken);
        return new WorkspaceDeploymentPreview(DisplayName, result.IsSuccess ? result.StandardOutput : result.StandardError);
    }

    public async Task UpAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default)
    {
        await overrideWriter.WriteAsync(plan, cancellationToken);
        _ = await processRunner.RunAsync(new ProcessRequest("docker", ComposeArgs(plan, "up", "-d"), ThrowOnError: true), cancellationToken);
    }

    public async Task DownAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(plan.OverrideComposeFile)) return;
        _ = await processRunner.RunAsync(new ProcessRequest("docker", ComposeArgs(plan, "down"), ThrowOnError: true), cancellationToken);
    }

    public async Task RestartAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default)
    {
        await overrideWriter.WriteAsync(plan, cancellationToken);
        _ = await processRunner.RunAsync(new ProcessRequest("docker", ComposeArgs(plan, "restart", "workspace"), ThrowOnError: true), cancellationToken);
    }

    public async Task<WorkspaceStatus> GetStatusAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(plan.OverrideComposeFile)) return new WorkspaceStatus(WorkspaceState.Stopped);

        try
        {
            var result = await processRunner.RunAsync(new ProcessRequest("docker", ComposeArgs(plan, "ps", "--status", "running", "--services")), cancellationToken);
            if (!result.IsSuccess) return new WorkspaceStatus(WorkspaceState.Unknown, result.StandardError.Trim());

            return result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(line => string.Equals(line.Trim(), "workspace", StringComparison.Ordinal))
                ? new WorkspaceStatus(WorkspaceState.Running)
                : new WorkspaceStatus(WorkspaceState.Stopped);
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            return new WorkspaceStatus(WorkspaceState.Unknown, exception.Message);
        }
    }

    public async Task<int> ExecAsync(WorkspaceExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var args = ComposeArgs(request.Plan, "exec", "workspace").Concat(request.Command).ToArray();
        var result = await processRunner.RunAsync(new ProcessRequest("docker", args, CaptureOutput: !request.Interactive), cancellationToken);
        return result.ExitCode;
    }

    public async Task<int> StreamLogsAsync(WorkspaceLogRequest request, CancellationToken cancellationToken = default)
    {
        var args = new List<string>(ComposeArgs(request.Plan, "logs"));
        if (request.Follow) args.Add("--follow");
        if (request.Tail is int tail)
        {
            args.Add("--tail");
            args.Add(tail.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        args.Add("workspace");
        var result = await processRunner.RunAsync(new ProcessRequest("docker", args, CaptureOutput: false), cancellationToken);
        return result.ExitCode;
    }

    private static string[] ComposeArgs(WorkspaceDeploymentPlan plan, params string[] command) =>
    [
        "compose",
        "-p", $"hstack-{plan.Project.Id}",
        "-f", plan.BaseComposeFile,
        "-f", plan.OverrideComposeFile,
        .. command
    ];
}
