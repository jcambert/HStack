using HermesStack.Domain.Context;

namespace HermesStack.Application.Context;

public sealed class ContextBudgetPolicy
{
    public IReadOnlyList<ContextItem> Apply(
        IReadOnlyList<ContextItem> items,
        ContextBudgetOptions budget,
        out IReadOnlyList<string> excludedUris)
    {
        var accepted = new List<ContextItem>();
        var excluded = new List<string>();
        var remaining = Math.Max(0, budget.MaxTokens);

        foreach (var item in items.Take(Math.Max(0, budget.MaxItems)))
        {
            if (item.EstimatedTokens <= remaining)
            {
                accepted.Add(item);
                remaining -= item.EstimatedTokens;
            }
            else
            {
                excluded.Add(item.Uri);
            }
        }

        if (items.Count > budget.MaxItems)
        {
            excluded.AddRange(items.Skip(budget.MaxItems).Select(static item => item.Uri));
        }

        excludedUris = excluded;
        return accepted;
    }

    public static int EstimateTokens(string value) =>
        string.IsNullOrEmpty(value) ? 0 : Math.Max(1, (int)Math.Ceiling(value.Length / 4d));
}
