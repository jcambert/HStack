using HermesStack.Domain.Tokens;

namespace HermesStack.Application.Abstractions;

public interface ITokenOptimizationStore
{
    Task<TokenOptimizationConfiguration> GetAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        TokenOptimizationConfiguration configuration,
        CancellationToken cancellationToken = default);
}
