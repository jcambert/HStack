using HermesStack.Domain.Orchestration;

namespace HermesStack.Application.Abstractions;

public interface IWorkspaceOrchestrator
{
    string Id { get; }
    string DisplayName { get; }

    Task<OrchestratorAvailability> DetectAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceDeploymentPreview> PreviewAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default);
    Task UpAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default);
    Task DownAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default);
    Task RestartAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default);
    Task<WorkspaceStatus> GetStatusAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default);
    Task<int> ExecAsync(WorkspaceExecutionRequest request, CancellationToken cancellationToken = default);
    Task<WorkspaceExecutionResult> ExecCaptureAsync(WorkspaceExecutionRequest request, CancellationToken cancellationToken = default);
    Task<int> StreamLogsAsync(WorkspaceLogRequest request, CancellationToken cancellationToken = default);
}
