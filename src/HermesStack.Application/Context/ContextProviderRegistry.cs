using HermesStack.Application.Abstractions;

namespace HermesStack.Application.Context;

public sealed class ContextProviderRegistry : IContextProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IContextProvider> _providers;

    public ContextProviderRegistry(IEnumerable<IContextProvider> providers)
    {
        var items = new Dictionary<string, IContextProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (!items.TryAdd(provider.Id, provider))
            {
                throw new InvalidOperationException($"Duplicate context provider id '{provider.Id}'.");
            }
        }

        _providers = items;
    }

    public IReadOnlyCollection<IContextProvider> All => _providers.Values.ToArray();

    public IContextProvider GetRequired(string id) =>
        _providers.TryGetValue(id, out var provider)
            ? provider
            : throw new KeyNotFoundException($"Unknown context provider '{id}'.");
}
