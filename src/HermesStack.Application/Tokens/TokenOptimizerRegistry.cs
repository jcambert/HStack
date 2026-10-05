using HermesStack.Application.Abstractions;

namespace HermesStack.Application.Tokens;

public sealed class TokenOptimizerRegistry
{
    private readonly IReadOnlyDictionary<string, ITokenOptimizer> _items;

    public TokenOptimizerRegistry(IEnumerable<ITokenOptimizer> optimizers)
    {
        var items = new Dictionary<string, ITokenOptimizer>(StringComparer.OrdinalIgnoreCase);
        foreach (var optimizer in optimizers)
        {
            if (!items.TryAdd(optimizer.Id, optimizer))
            {
                throw new InvalidOperationException(
                    $"Duplicate token optimizer id '{optimizer.Id}'.");
            }
        }

        _items = items;
    }

    public IReadOnlyCollection<ITokenOptimizer> All => _items.Values.ToArray();

    public ITokenOptimizer GetRequired(string id) =>
        _items.TryGetValue(id, out var optimizer)
            ? optimizer
            : throw new KeyNotFoundException($"Unknown token optimizer '{id}'.");
}
