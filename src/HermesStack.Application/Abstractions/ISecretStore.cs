using HermesStack.Domain.Security;

namespace HermesStack.Application.Abstractions;

public interface ISecretStore
{
    Task SetAsync(
        SecretReference reference,
        SecretValue value,
        CancellationToken cancellationToken = default);

    Task<SecretValue?> GetAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default);
}
