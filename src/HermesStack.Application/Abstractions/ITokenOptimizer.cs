using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Tokens;

namespace HermesStack.Application.Abstractions;

public interface ITokenOptimizer
{
    string Id { get; }
    string DisplayName { get; }
    string Version { get; }
    IReadOnlySet<string> SupportedAgents { get; }
    bool SupportsGainMetrics { get; }

    Task ConfigureAsync(
        WorkspaceDeploymentPlan plan,
        string agentId,
        CancellationToken cancellationToken = default);

    Task DisableAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default);

    Task<TokenOptimizerHealth> InspectAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default);

    Task<TokenGainMetrics> ReadGainAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default);
}
