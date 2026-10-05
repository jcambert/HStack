using HermesStack.Application.Abstractions;

namespace HermesStack.Application.Agents;

public sealed class AgentHarnessRegistry : IAgentHarnessRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentHarness> _items;

    public AgentHarnessRegistry(IEnumerable<IAgentHarness> harnesses)
    {
        var items = new Dictionary<string, IAgentHarness>(StringComparer.OrdinalIgnoreCase);
        foreach (var harness in harnesses)
        {
            if (!items.TryAdd(harness.Id, harness))
            {
                throw new InvalidOperationException($"Duplicate agent harness id '{harness.Id}'.");
            }
        }

        _items = items;
    }

    public IReadOnlyCollection<IAgentHarness> All => _items.Values.ToArray();

    public IAgentHarness GetRequired(string id) =>
        _items.TryGetValue(id, out var harness)
            ? harness
            : throw new KeyNotFoundException($"Unknown agent '{id}'.");
}
