using HermesStack.Domain.Integrations;

namespace HermesStack.Application.Abstractions;

public interface IIntegrationRegistry
{
    IReadOnlyCollection<IntegrationDescriptor> All { get; }
    IntegrationDescriptor GetRequired(string id);
}
