using HermesStack.Domain.Orchestration;

namespace HermesStack.Application.Abstractions;

public interface IOrchestrationConfigurationStore
{
    Task<OrchestrationConfiguration> GetOrchestrationAsync(
        CancellationToken cancellationToken = default);

    Task SaveOrchestrationAsync(
        OrchestrationConfiguration configuration,
        CancellationToken cancellationToken = default);
}
