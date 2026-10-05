using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Tokens;

namespace HermesStack.Docker.Tokens;

public sealed class CavemanTokenOptimizer(
    IWorkspaceOrchestrator orchestrator,
    string version,
    string releaseTag) : ITokenOptimizer
{
    private const string Installer = "/opt/caveman/bin/install.js";

    private static readonly IReadOnlySet<string> Agents =
        new HashSet<string>(
            ["claude", "codex", "hermes", "opencode"],
            StringComparer.OrdinalIgnoreCase);

    public string Id => "caveman";
    public string DisplayName => "Caveman";
    public string Version => version;
    public IReadOnlySet<string> SupportedAgents => Agents;
    public bool SupportsGainMetrics => false;

    public async Task ConfigureAsync(
        WorkspaceDeploymentPlan plan,
        string agentId,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedAgents.Contains(agentId))
        {
            throw new NotSupportedException(
                $"Caveman does not support agent '{agentId}'.");
        }

        var result = await orchestrator.ExecCaptureAsync(
            new WorkspaceExecutionRequest(
                plan,
                [
                    "node",
                    Installer,
                    "--only",
                    agentId.ToLowerInvariant(),
                    "--non-interactive"
                ],
                Interactive: false,
                Environment: Environment()),
            cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Unable to configure Caveman for '{agentId}' " +
                $"(exit code {result.ExitCode}). {Combined(result)}");
        }
    }

    public async Task DisableAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var result = await orchestrator.ExecCaptureAsync(
            new WorkspaceExecutionRequest(
                plan,
                ["node", Installer, "--uninstall", "--non-interactive"],
                Interactive: false,
                Environment: Environment()),
            cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Unable to uninstall Caveman (exit code {result.ExitCode}). " +
                Combined(result));
        }
    }

    public async Task<TokenOptimizerHealth> InspectAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var result = await orchestrator.ExecCaptureAsync(
            new WorkspaceExecutionRequest(
                plan,
                ["node", "-p", "require('/opt/caveman/package.json').version"],
                Interactive: false),
            cancellationToken);
        var output = Combined(result).Trim();
        var available = result.IsSuccess &&
            string.Equals(output, Version, StringComparison.Ordinal);

        return new TokenOptimizerHealth(
            Id,
            DisplayName,
            Version,
            available,
            available
                ? $"pinned source available at /opt/caveman ({releaseTag}); opt-in semantic compression"
                : string.IsNullOrWhiteSpace(output)
                    ? "Pinned Caveman source unavailable."
                    : output);
    }

    public Task<TokenGainMetrics> ReadGainAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new TokenGainMetrics(
            Id,
            TokenMetricEvidence.Unavailable,
            null,
            null,
            null,
            null,
            "HermesStack integrates the pinned Caveman agent skill/plugin path, not its proxy accounting runtime; no comparable measured gain is exposed."));

    private IReadOnlyDictionary<string, string> Environment() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CAVEMAN_REF"] = releaseTag
        };

    private static string Combined(WorkspaceExecutionResult result) =>
        string.Join(
            Environment.NewLine,
            new[] { result.StandardOutput.Trim(), result.StandardError.Trim() }
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
}
