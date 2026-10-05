using HermesStack.Domain.Tokens;

namespace HermesStack.Application.Abstractions;

public interface ITokenMetricStore
{
    Task AppendAsync(
        TokenMetricRecord record,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TokenMetricRecord>> ReadAsync(
        string projectId,
        CancellationToken cancellationToken = default);
}
