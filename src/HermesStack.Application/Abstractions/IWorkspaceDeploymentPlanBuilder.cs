using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;

namespace HermesStack.Application.Abstractions;

public interface IWorkspaceDeploymentPlanBuilder
{
    Task<WorkspaceDeploymentPlan> BuildAsync(ProjectDefinition project, string? orchestratorOverride = null, CancellationToken cancellationToken = default);
}
