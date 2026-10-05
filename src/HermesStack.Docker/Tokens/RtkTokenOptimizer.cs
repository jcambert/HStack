using HermesStack.Application.Abstractions;
using HermesStack.Application.Tokens;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Tokens;

namespace HermesStack.Docker.Tokens;

public sealed class RtkTokenOptimizer(
    IWorkspaceOrchestrator orchestrator,
    string version) : ITokenOptimizer
{
    private static readonly IReadOnlySet<string> Agents =
        new HashSet<string>(
            ["claude", "codex", "hermes", "opencode"],
            StringComparer.OrdinalIgnoreCase);

    public string Id => "rtk";
    public string DisplayName => "RTK";
    public string Version => version;
    public IReadOnlySet<string> SupportedAgents => Agents;
    public bool SupportsGainMetrics => true;

    public async Task ConfigureAsync(
        WorkspaceDeploymentPlan plan,
        string agentId,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedAgents.Contains(agentId))
        {
            throw new NotSupportedException(
                $"RTK does not support agent '{agentId}'.");
        }

        await RunRequiredAsync(
            plan,
            ["rtk", "config", "recall", "disabled"],
            "disable RTK raw-output recall",
            cancellationToken);

        var command = agentId.ToLowerInvariant() switch
        {
            "claude" => new[] { "rtk", "init", "-g", "--auto-patch" },
            "codex" => ["rtk", "init", "-g", "--codex"],
            "hermes" => ["rtk", "init", "--agent", "hermes"],
            "opencode" => ["rtk", "init", "-g", "--opencode"],
            _ => throw new NotSupportedException(
                $"RTK does not support agent '{agentId}'.")
        };

        await RunRequiredAsync(
            plan,
            command,
            $"configure RTK for '{agentId}'",
            cancellationToken);
    }

    public async Task DisableAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var commands = new IReadOnlyList<string>[]
        {
            ["rtk", "init", "-g", "--uninstall"],
            ["rtk", "init", "-g", "--codex", "--uninstall"],
            ["rtk", "init", "-g", "--opencode", "--uninstall"],
            ["rtk", "init", "--agent", "hermes", "--uninstall"]
        };

        foreach (var command in commands)
        {
            var result = await orchestrator.ExecCaptureAsync(
                new WorkspaceExecutionRequest(
                    plan,
                    command,
                    Interactive: false),
                cancellationToken);
            if (!result.IsSuccess &&
                !Combined(result).Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                throw Failure("disable RTK integration", result);
            }
        }
    }

    public async Task<TokenOptimizerHealth> InspectAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var result = await orchestrator.ExecCaptureAsync(
            new WorkspaceExecutionRequest(
                plan,
                ["rtk", "--version"],
                Interactive: false),
            cancellationToken);

        var output = Combined(result).Trim();
        var versionMatches = result.IsSuccess &&
            output.Contains(Version, StringComparison.Ordinal);

        return new TokenOptimizerHealth(
            Id,
            DisplayName,
            Version,
            versionMatches,
            versionMatches
                ? "pinned binary available; telemetry disabled and tracking DB is ephemeral by HermesStack policy"
                : string.IsNullOrWhiteSpace(output)
                    ? "RTK binary unavailable"
                    : output);
    }

    public async Task<TokenGainMetrics> ReadGainAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var result = await orchestrator.ExecCaptureAsync(
            new WorkspaceExecutionRequest(
                plan,
                ["rtk", "gain", "--all", "--format", "json"],
                Interactive: false),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return new TokenGainMetrics(
                Id,
                TokenMetricEvidence.Unavailable,
                null,
                null,
                null,
                null,
                Combined(result).Trim());
        }

        return RtkGainParser.Parse(result.StandardOutput);
    }

    private async Task RunRequiredAsync(
        WorkspaceDeploymentPlan plan,
        IReadOnlyList<string> command,
        string operation,
        CancellationToken cancellationToken)
    {
        var result = await orchestrator.ExecCaptureAsync(
            new WorkspaceExecutionRequest(
                plan,
                command,
                Interactive: false),
            cancellationToken);
        if (!result.IsSuccess)
        {
            throw Failure(operation, result);
        }
    }

    private static string Combined(WorkspaceExecutionResult result) =>
        string.Join(
            Environment.NewLine,
            new[] { result.StandardOutput.Trim(), result.StandardError.Trim() }
                .Where(static value => !string.IsNullOrWhiteSpace(value)));

    private static InvalidOperationException Failure(
        string operation,
        WorkspaceExecutionResult result) =>
        new(
            $"Unable to {operation} (exit code {result.ExitCode}). " +
            Combined(result));
}
