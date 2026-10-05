using HermesStack.Domain.Security;

namespace HermesStack.Application.Abstractions;

public interface ISecretPolicyStore
{
    Task SetAsync(SecretPolicy policy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecretPolicy>> ListAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecretPolicy>> FindForAgentAsync(
        string projectId,
        string agentId,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        string projectId,
        string name,
        CancellationToken cancellationToken = default);
}
