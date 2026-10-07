using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;

namespace HermesStack.Application.Orchestration;

public sealed class WorkspaceOrchestratorRegistry(
    IEnumerable<IWorkspaceOrchestrator> orchestrators) : IWorkspaceOrchestratorRegistry
{
    private readonly IReadOnlyDictionary<string, IWorkspaceOrchestrator> _byId =
        orchestrators.ToDictionary(
            static item => item.Id,
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IWorkspaceOrchestrator> All =>
        _byId.Values.OrderBy(static item => item.Id, StringComparer.Ordinal).ToArray();

    public IWorkspaceOrchestrator GetRequired(string id) =>
        _byId.TryGetValue(id, out var orchestrator)
            ? orchestrator
            : throw new KeyNotFoundException(
                $"Unknown workspace orchestrator '{id}'. Available: {string.Join(", ", _byId.Keys.OrderBy(static value => value, StringComparer.Ordinal))}.");
}

public sealed class RoutedWorkspaceOrchestrator(
    IWorkspaceOrchestratorRegistry registry) : IWorkspaceOrchestrator
{
    public string Id => "routed";
    public string DisplayName => "HermesStack orchestrator router";

    public async Task<OrchestratorAvailability> DetectAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        var any = false;
        foreach (var item in registry.All)
        {
            var availability = await item.DetectAsync(cancellationToken);
            any |= availability.IsAvailable;
            results.Add(
                $"{item.Id}={(availability.IsAvailable ? availability.Version ?? "available" : availability.Reason ?? "unavailable")}");
        }

        return new OrchestratorAvailability(
            any,
            Reason: string.Join("; ", results));
    }

    public Task<WorkspaceDeploymentPreview> PreviewAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default) =>
        Resolve(plan).PreviewAsync(plan, cancellationToken);

    public Task UpAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default) =>
        Resolve(plan).UpAsync(plan, cancellationToken);

    public Task DownAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default) =>
        Resolve(plan).DownAsync(plan, cancellationToken);

    public Task RestartAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default) =>
        Resolve(plan).RestartAsync(plan, cancellationToken);

    public Task<WorkspaceStatus> GetStatusAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default) =>
        Resolve(plan).GetStatusAsync(plan, cancellationToken);

    public Task<int> ExecAsync(
        WorkspaceExecutionRequest request,
        CancellationToken cancellationToken = default) =>
        Resolve(request.Plan).ExecAsync(request, cancellationToken);

    public Task<WorkspaceExecutionResult> ExecCaptureAsync(
        WorkspaceExecutionRequest request,
        CancellationToken cancellationToken = default) =>
        Resolve(request.Plan).ExecCaptureAsync(request, cancellationToken);

    public Task<int> StreamLogsAsync(
        WorkspaceLogRequest request,
        CancellationToken cancellationToken = default) =>
        Resolve(request.Plan).StreamLogsAsync(request, cancellationToken);

    private IWorkspaceOrchestrator Resolve(WorkspaceDeploymentPlan plan) =>
        registry.GetRequired(plan.OrchestratorId);
}
