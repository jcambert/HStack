using HermesStack.Domain.Orchestration;

namespace HermesStack.Application.Abstractions;

// Provider implementations must never silently substitute a weaker sandbox.
// Create/Destroy delegate orchestration to the existing Compose/Aspire owner:
// there is only one owner of a running workspace.
public interface IExecutionEnvironmentProvider
{
    string Id { get; }

    Task<ExecutionProviderAvailability> DetectAsync(
        CancellationToken cancellationToken = default);

    Task<ExecutionEnvironmentPlan> PlanAsync(
        WorkspaceDeploymentPlan workspace,
        CancellationToken cancellationToken = default);

    Task<ExecutionEnvironmentHandle> CreateAsync(
        ExecutionEnvironmentPlan plan,
        CancellationToken cancellationToken = default);

    Task DestroyAsync(
        ExecutionEnvironmentHandle environment,
        CancellationToken cancellationToken = default);
}
