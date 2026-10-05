using HermesStack.Domain.Network;

namespace HermesStack.Application.Abstractions;

public interface IProxyConfigurationStore
{
    Task<ProxyConfiguration> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ProxyConfiguration configuration, CancellationToken cancellationToken = default);
}
