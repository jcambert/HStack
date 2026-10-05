using HermesStack.Domain.Projects;

namespace HermesStack.Application.Abstractions;

public interface IProjectStore
{
    Task<IReadOnlyList<ProjectDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task<ProjectDefinition?> FindAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(ProjectDefinition project, CancellationToken cancellationToken = default);
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);
}
