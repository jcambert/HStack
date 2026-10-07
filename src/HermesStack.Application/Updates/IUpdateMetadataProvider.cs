using HermesStack.Domain.Updates;

namespace HermesStack.Application.Updates;

public interface IUpdateMetadataProvider
{
    string Source { get; }

    Task<IReadOnlyList<ManagedComponentVersion>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default);
}
