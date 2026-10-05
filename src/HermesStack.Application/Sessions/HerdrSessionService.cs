using HermesStack.Application.Abstractions;
using HermesStack.Application.Agents;
using HermesStack.Domain.Agents;
using HermesStack.Domain.Orchestration;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HermesStack.Application.Sessions;

public sealed record HerdrProjectSession(
    string SessionName,
    string WorkspaceId,
    string WorkspaceLabel);

public sealed class HerdrSessionService(
    IWorkspaceOrchestrator orchestrator,
    IAgentHarnessRegistry agents)
{
    private static readonly string[] IntegrationTargets = ["claude", "codex", "hermes", "opencode"];
    private static readonly Regex AgentNamePattern = new(
        "^[a-z][a-z0-9_-]{0,31}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<HerdrProjectSession> EnsureAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        foreach (var harness in agents.All.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            await harness.ConfigureAsync(new AgentConfigureRequest(plan), cancellationToken);
        }

        var workspaceStatus = await orchestrator.GetStatusAsync(plan, cancellationToken);
        if (workspaceStatus.State != WorkspaceState.Running)
        {
            await orchestrator.UpAsync(plan, cancellationToken);
        }

        foreach (var target in IntegrationTargets)
        {
            await RunRequiredAsync(
                plan,
                ["herdr", "integration", "install", target],
                $"install Herdr integration '{target}'",
                cancellationToken);
        }

        var list = await EnsureServerAndListWorkspacesAsync(plan, cancellationToken);
        var label = WorkspaceLabel(plan);
        var existing = FindWorkspaceId(list.StandardOutput, label);
        if (existing is not null)
        {
            return new HerdrProjectSession(SessionName(plan), existing, label);
        }

        var created = await RunRequiredAsync(
            plan,
            ["herdr", "workspace", "create", "--cwd", "/workspace", "--label", label, "--no-focus"],
            "create the project Herdr workspace",
            cancellationToken);

        var workspaceId = ReadRequiredString(
            created.StandardOutput,
            "result",
            "workspace",
            "workspace_id");

        return new HerdrProjectSession(SessionName(plan), workspaceId, label);
    }

    public async Task<int> AttachAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        _ = await EnsureAsync(plan, cancellationToken);
        return await orchestrator.ExecAsync(
            new WorkspaceExecutionRequest(plan, ["herdr"], Interactive: true),
            cancellationToken);
    }

    public async Task<WorkspaceExecutionResult> ListSessionsAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        _ = await EnsureAsync(plan, cancellationToken);
        return await RunRequiredAsync(
            plan,
            ["herdr", "session", "list", "--json"],
            "list Herdr sessions",
            cancellationToken);
    }

    public async Task<WorkspaceExecutionResult> ListAgentsAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        _ = await EnsureAsync(plan, cancellationToken);
        return await RunRequiredAsync(
            plan,
            ["herdr", "agent", "list"],
            "list Herdr agents",
            cancellationToken);
    }

    public async Task<WorkspaceExecutionResult> IntegrationStatusAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        _ = await EnsureAsync(plan, cancellationToken);
        return await RunRequiredAsync(
            plan,
            ["herdr", "integration", "status"],
            "read Herdr integration status",
            cancellationToken);
    }

    public async Task<WorkspaceExecutionResult> RunAgentAsync(
        WorkspaceDeploymentPlan plan,
        string agentId,
        string agentName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        _ = agents.GetRequired(agentId);
        if (!AgentNamePattern.IsMatch(agentName))
        {
            throw new ArgumentException(
                "Herdr agent names must match [a-z][a-z0-9_-]{0,31}.",
                nameof(agentName));
        }

        var session = await EnsureAsync(plan, cancellationToken);
        var tab = await RunRequiredAsync(
            plan,
            [
                "herdr", "tab", "create",
                "--workspace", session.WorkspaceId,
                "--cwd", "/workspace",
                "--label", agentName,
                "--no-focus"
            ],
            $"create Herdr tab for agent '{agentName}'",
            cancellationToken);

        var paneId = ReadRequiredString(tab.StandardOutput, "result", "root_pane", "pane_id");
        var command = new List<string>
        {
            "herdr", "agent", "start", agentName,
            "--kind", agentId,
            "--pane", paneId,
            "--timeout", "30000"
        };
        if (arguments.Count > 0)
        {
            command.Add("--");
            command.AddRange(arguments);
        }

        return await RunRequiredAsync(
            plan,
            command,
            $"start agent '{agentName}' through Herdr",
            cancellationToken);
    }

    public async Task<int> StopAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var status = await orchestrator.GetStatusAsync(plan, cancellationToken);
        if (status.State != WorkspaceState.Running)
        {
            return 0;
        }

        return await orchestrator.ExecAsync(
            new WorkspaceExecutionRequest(
                plan,
                ["herdr", "server", "stop"],
                Interactive: false),
            cancellationToken);
    }

    public static string SessionName(WorkspaceDeploymentPlan plan) => $"hstack-{plan.Project.Id}";
    public static string WorkspaceLabel(WorkspaceDeploymentPlan plan) => $"hstack:{plan.Project.Id}";

    private async Task<WorkspaceExecutionResult> EnsureServerAndListWorkspacesAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken)
    {
        var list = await CaptureAsync(plan, ["herdr", "workspace", "list"], cancellationToken);
        if (list.IsSuccess)
        {
            return list;
        }

        if (!LooksLikeStoppedServer(list))
        {
            throw Failure("list Herdr workspaces", list);
        }

        var startExitCode = await orchestrator.ExecAsync(
            new WorkspaceExecutionRequest(
                plan,
                ["herdr", "server"],
                Interactive: false,
                Detached: true),
            cancellationToken);
        if (startExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to start the Herdr server (exit code {startExitCode}).");
        }

        for (var attempt = 0; attempt < 50; attempt++)
        {
            await Task.Delay(100, cancellationToken);
            list = await CaptureAsync(plan, ["herdr", "workspace", "list"], cancellationToken);
            if (list.IsSuccess)
            {
                return list;
            }
        }

        throw Failure("wait for the Herdr server", list);
    }

    private Task<WorkspaceExecutionResult> CaptureAsync(
        WorkspaceDeploymentPlan plan,
        IReadOnlyList<string> command,
        CancellationToken cancellationToken) =>
        orchestrator.ExecCaptureAsync(
            new WorkspaceExecutionRequest(plan, command, Interactive: false),
            cancellationToken);

    private async Task<WorkspaceExecutionResult> RunRequiredAsync(
        WorkspaceDeploymentPlan plan,
        IReadOnlyList<string> command,
        string operation,
        CancellationToken cancellationToken)
    {
        var result = await CaptureAsync(plan, command, cancellationToken);
        if (!result.IsSuccess)
        {
            throw Failure(operation, result);
        }

        return result;
    }

    private static bool LooksLikeStoppedServer(WorkspaceExecutionResult result)
    {
        var combined = $"{result.StandardOutput}\n{result.StandardError}";
        return combined.Contains("server_not_running", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("server not running", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("no such file", StringComparison.OrdinalIgnoreCase);
    }

    private static InvalidOperationException Failure(
        string operation,
        WorkspaceExecutionResult result)
    {
        var details = string.Join(
            Environment.NewLine,
            new[] { result.StandardError.Trim(), result.StandardOutput.Trim() }
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
        return new InvalidOperationException(
            $"Unable to {operation} (exit code {result.ExitCode}). {details}".Trim());
    }

    private static string? FindWorkspaceId(string json, string label)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("workspaces", out var workspaces))
        {
            return null;
        }

        foreach (var workspace in workspaces.EnumerateArray())
        {
            if (workspace.TryGetProperty("label", out var labelValue) &&
                string.Equals(labelValue.GetString(), label, StringComparison.Ordinal) &&
                workspace.TryGetProperty("workspace_id", out var id))
            {
                return id.GetString();
            }
        }

        return null;
    }

    private static string ReadRequiredString(string json, params string[] path)
    {
        using var document = JsonDocument.Parse(json);
        var current = document.RootElement;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                throw new InvalidDataException(
                    $"Herdr response did not include '{string.Join(".", path)}'.");
            }
        }

        return current.GetString()
            ?? throw new InvalidDataException(
                $"Herdr response field '{string.Join(".", path)}' was null.");
    }
}
