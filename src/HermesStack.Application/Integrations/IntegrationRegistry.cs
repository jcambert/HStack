using HermesStack.Application.Abstractions;
using HermesStack.Domain.Integrations;

namespace HermesStack.Application.Integrations;

public sealed class IntegrationRegistry : IIntegrationRegistry
{
    private readonly IReadOnlyDictionary<string, IntegrationDescriptor> _items;

    public IntegrationRegistry(IEnumerable<IntegrationDescriptor> descriptors)
    {
        var items = new Dictionary<string, IntegrationDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in descriptors)
        {
            if (!items.TryAdd(descriptor.Id, descriptor))
            {
                throw new InvalidOperationException($"Duplicate integration id '{descriptor.Id}'.");
            }
        }

        _items = items;
    }

    public IReadOnlyCollection<IntegrationDescriptor> All => _items.Values.ToArray();

    public IntegrationDescriptor GetRequired(string id) =>
        _items.TryGetValue(id, out var value) ? value : throw new KeyNotFoundException($"Unknown integration '{id}'.");
}
