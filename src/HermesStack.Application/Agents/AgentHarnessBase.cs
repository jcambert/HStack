using HermesStack.Application.Abstractions;
using HermesStack.Domain.Agents;
using HermesStack.Domain.Orchestration;

namespace HermesStack.Application.Agents;

public abstract class AgentHarnessBase(IWorkspaceOrchestrator orchestrator) : IAgentHarness
{
    protected IWorkspaceOrchestrator Orchestrator { get; } = orchestrator;

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    protected abstract string Executable { get; }
    protected virtual IReadOnlyList<string> VersionArguments => ["--version"];
    protected abstract IReadOnlyList<string> AuthenticationArguments { get; }

    public virtual async Task<AgentInstallationInfo> InspectAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var command = new[] { Executable }.Concat(VersionArguments).ToArray();
        var result = await Orchestrator.ExecCaptureAsync(
            new WorkspaceExecutionRequest(plan, command, Interactive: false),
            cancellationToken);

        var output = string.Join(
            Environment.NewLine,
            new[] { result.StandardOutput.Trim(), result.StandardError.Trim() }
                .Where(static value => !string.IsNullOrWhiteSpace(value)))
            .Trim();

        return result.ExitCode == 0
            ? new AgentInstallationInfo(Id, DisplayName, true, output)
            : new AgentInstallationInfo(Id, DisplayName, false, Details: output);
    }

    public virtual Task<int> LaunchAsync(
        AgentLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        var prefix = request.Mode == AgentLaunchMode.Authenticate
            ? AuthenticationArguments
            : Array.Empty<string>();

        var command = new[] { Executable }
            .Concat(prefix)
            .Concat(request.EffectiveArguments)
            .ToArray();

        return Orchestrator.ExecAsync(
            new WorkspaceExecutionRequest(request.Plan, command, Interactive: true),
            cancellationToken);
    }

    public abstract Task ConfigureAsync(
        AgentConfigureRequest request,
        CancellationToken cancellationToken = default);

    protected static string StateDirectory(WorkspaceDeploymentPlan plan, params string[] segments)
    {
        var parts = new[] { plan.ProjectDataRoot }.Concat(segments).ToArray();
        return Path.Combine(parts);
    }

    protected static Task EnsureDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    protected static async Task WriteManagedFileAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            return;
        }

        await File.WriteAllTextAsync(path, content, cancellationToken);
    }
}
