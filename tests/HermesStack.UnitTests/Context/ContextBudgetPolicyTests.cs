using HermesStack.Application.Context;
using HermesStack.Domain.Context;

namespace HermesStack.UnitTests.Context;

public sealed class ContextBudgetPolicyTests
{
    [Fact]
    public void Budget_limits_items_and_tokens_without_modifying_content()
    {
        var policy = new ContextBudgetPolicy();
        ContextItem[] items =
        [
            new("viking://a", ContextScope.Project, "alpha", 3, 0.9),
            new("viking://b", ContextScope.Project, "beta", 4, 0.8),
            new("viking://c", ContextScope.Project, "gamma", 2, 0.7)
        ];

        var accepted = policy.Apply(
            items,
            new ContextBudgetOptions(MaxTokens: 5, MaxItems: 3),
            out var excluded);

        Assert.Equal(["viking://a", "viking://c"], accepted.Select(static x => x.Uri));
        Assert.Equal(["viking://b"], excluded);
        Assert.Equal("alpha", accepted[0].Content);
    }
}
