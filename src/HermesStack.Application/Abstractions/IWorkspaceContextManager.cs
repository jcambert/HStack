namespace HermesStack.Application.Abstractions;

public interface IWorkspaceContextManager
{
    Task EnsureWorkspaceReadyAsync(
        string projectId,
        CancellationToken cancellationToken = default);
}
