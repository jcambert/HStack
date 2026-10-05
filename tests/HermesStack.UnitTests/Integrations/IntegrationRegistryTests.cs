using HermesStack.Application.Integrations;
using HermesStack.Domain.Integrations;

namespace HermesStack.UnitTests.Integrations;

public sealed class IntegrationRegistryTests
{
    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var descriptor = new IntegrationDescriptor("rtk", "RTK", IntegrationKind.TokenOptimizer, new HashSet<IntegrationCapability>());
        Assert.Throws<InvalidOperationException>(() => new IntegrationRegistry([descriptor, descriptor]));
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        var descriptor = new IntegrationDescriptor("compose", "Docker Compose", IntegrationKind.Orchestrator, new HashSet<IntegrationCapability>());
        var registry = new IntegrationRegistry([descriptor]);
        Assert.Same(descriptor, registry.GetRequired("COMPOSE"));
    }
}
