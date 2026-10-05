using HermesStack.Application.Tokens;
using HermesStack.Domain.Tokens;

namespace HermesStack.UnitTests.Tokens;

public sealed class RtkGainParserTests
{
    [Fact]
    public void Gain_is_marked_estimated_and_never_as_cost_savings()
    {
        var metrics = RtkGainParser.Parse("""
            {
              "summary": {
                "total_commands": 12,
                "total_input": 45230,
                "total_output": 4890,
                "total_saved": 40340,
                "avg_savings_pct": 89.2
              },
              "daily": [],
              "weekly": [],
              "monthly": []
            }
            """);

        Assert.Equal(TokenMetricEvidence.Estimated, metrics.Evidence);
        Assert.Equal(45_230, metrics.RawTokens);
        Assert.Equal(4_890, metrics.OptimizedTokens);
        Assert.Equal(40_340, metrics.SavedTokens);
        Assert.Equal(89.2, metrics.SavingsPercent);
        Assert.Contains("not LLM billing", metrics.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_gain_format_is_unavailable_not_fabricated()
    {
        var metrics = RtkGainParser.Parse("no machine-readable metrics here");

        Assert.Equal(TokenMetricEvidence.Unavailable, metrics.Evidence);
        Assert.Null(metrics.SavedTokens);
    }
}
