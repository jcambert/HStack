using HermesStack.Domain.Orchestration;

namespace HermesStack.Application.Abstractions;

public interface IWorkspaceOrchestratorRegistry
{
    IReadOnlyList<IWorkspaceOrchestrator> All { get; }

    IWorkspaceOrchestrator GetRequired(string id);
}

public interface IOrchestratorCapabilityEvaluator
{
    string OrchestratorId { get; }

    Task<OrchestratorCapabilityReport> EvaluateAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default);
}
