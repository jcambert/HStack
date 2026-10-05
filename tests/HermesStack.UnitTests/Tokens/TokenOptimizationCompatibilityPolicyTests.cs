using HermesStack.Application.Tokens;
using HermesStack.Domain.Tokens;

namespace HermesStack.UnitTests.Tokens;

public sealed class TokenOptimizationCompatibilityPolicyTests
{
    [Fact]
    public void Rtk_and_caveman_are_potentially_lossy()
    {
        var sut = new TokenOptimizationCompatibilityPolicy();

        Assert.Equal(
            TokenOptimizationCompatibility.PotentiallyLossy,
            sut.Classify("rtk", "caveman"));
    }

    [Fact]
    public void Potentially_lossy_stack_requires_explicit_override()
    {
        var sut = new TokenOptimizationCompatibilityPolicy();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            sut.ValidateStack(["rtk"], "caveman", allowPotentiallyLossy: false));

        Assert.Contains("HS5001", exception.Message, StringComparison.Ordinal);
        sut.ValidateStack(["rtk"], "caveman", allowPotentiallyLossy: true);
    }
}
