using HermesStack.Domain.Agents;
using HermesStack.Domain.Orchestration;

namespace HermesStack.Application.Abstractions;

public interface IAgentHarness
{
    string Id { get; }
    string DisplayName { get; }

    Task<AgentInstallationInfo> InspectAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default);

    Task<int> LaunchAsync(
        AgentLaunchRequest request,
        CancellationToken cancellationToken = default);

    Task ConfigureAsync(
        AgentConfigureRequest request,
        CancellationToken cancellationToken = default);
}
