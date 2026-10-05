using HermesStack.Domain.Tokens;

namespace HermesStack.Application.Tokens;

public sealed class TokenOptimizationCompatibilityPolicy
{
    public TokenOptimizationCompatibility Classify(
        string leftProvider,
        string rightProvider)
    {
        if (string.Equals(leftProvider, rightProvider, StringComparison.OrdinalIgnoreCase))
        {
            return TokenOptimizationCompatibility.Redundant;
        }

        var ids = new HashSet<string>(
            [leftProvider, rightProvider],
            StringComparer.OrdinalIgnoreCase);

        if (ids.SetEquals(["rtk", "caveman"]))
        {
            return TokenOptimizationCompatibility.PotentiallyLossy;
        }

        return TokenOptimizationCompatibility.Unsupported;
    }

    public void ValidateStack(
        IEnumerable<string> existingProviders,
        string requestedProvider,
        bool allowPotentiallyLossy)
    {
        foreach (var existing in existingProviders)
        {
            var classification = Classify(existing, requestedProvider);
            if (classification == TokenOptimizationCompatibility.Redundant)
            {
                continue;
            }

            if (classification == TokenOptimizationCompatibility.PotentiallyLossy &&
                allowPotentiallyLossy)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"HS5001: Token optimizer stack '{existing} + {requestedProvider}' " +
                $"is {classification}. PotentiallyLossy stacks require explicit " +
                "--allow-lossy-stack.");
        }
    }
}
