namespace HermesStack.Domain.Tokens;

public enum TokenOptimizationProfile
{
    Off,
    Safe,
    Balanced,
    Aggressive,
    Custom
}

public enum TokenOptimizationCompatibility
{
    Compatible,
    Redundant,
    PotentiallyLossy,
    Unsupported
}

public enum TokenMetricEvidence
{
    Measured,
    Estimated,
    Unavailable
}

public sealed record TokenProviderSelection(
    string ProviderId,
    IReadOnlyList<string> Agents);

public sealed record TokenOptimizationConfiguration(
    string ProjectId,
    bool Enabled = true,
    TokenOptimizationProfile Profile = TokenOptimizationProfile.Balanced,
    IReadOnlyList<TokenProviderSelection>? Providers = null)
{
    public IReadOnlyList<TokenProviderSelection> EffectiveProviders => Providers ?? [];
}

public sealed record TokenOptimizerHealth(
    string ProviderId,
    string DisplayName,
    string Version,
    bool Available,
    string Details);

public sealed record TokenGainMetrics(
    string ProviderId,
    TokenMetricEvidence Evidence,
    long? RawTokens,
    long? OptimizedTokens,
    long? SavedTokens,
    double? SavingsPercent,
    string Details);

public sealed record TokenMetricRecord(
    DateTimeOffset Timestamp,
    string ProjectId,
    string Agent,
    string ProviderId,
    TokenMetricEvidence Evidence,
    long? RawTokens,
    long? OptimizedTokens,
    long? SavedTokens,
    double? SavingsPercent);
