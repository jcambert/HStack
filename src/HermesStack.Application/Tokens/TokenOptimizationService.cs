using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Tokens;

namespace HermesStack.Application.Tokens;

public sealed class TokenOptimizationService(
    IWorkspaceOrchestrator orchestrator,
    TokenOptimizerRegistry registry,
    ITokenOptimizationStore store,
    ITokenMetricStore metrics,
    TokenOptimizationCompatibilityPolicy compatibility)
{
    public async Task<TokenOptimizationConfiguration> EnableAsync(
        WorkspaceDeploymentPlan plan,
        string providerId,
        TokenOptimizationProfile profile,
        IReadOnlyList<string> agents,
        bool allowPotentiallyLossy = false,
        CancellationToken cancellationToken = default)
    {
        if (profile == TokenOptimizationProfile.Off)
        {
            throw new ArgumentException(
                "Use 'hstack token disable' for the off profile.",
                nameof(profile));
        }

        if (string.Equals(providerId, "caveman", StringComparison.OrdinalIgnoreCase) &&
            profile is not (TokenOptimizationProfile.Aggressive or TokenOptimizationProfile.Custom))
        {
            throw new InvalidOperationException(
                "HS5003: Caveman performs semantic response compression and is restricted to explicit aggressive/custom profiles.");
        }

        var provider = registry.GetRequired(providerId);
        var normalizedAgents = agents
            .Select(static value => value.Trim().ToLowerInvariant())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        if (normalizedAgents.Length == 0)
        {
            throw new ArgumentException("At least one agent must be selected.", nameof(agents));
        }

        foreach (var agent in normalizedAgents)
        {
            if (!provider.SupportedAgents.Contains(agent))
            {
                throw new NotSupportedException(
                    $"Token optimizer '{provider.Id}' does not support agent '{agent}'.");
            }
        }

        var current = await store.GetAsync(plan.Project.Id, cancellationToken);
        compatibility.ValidateStack(
            current.EffectiveProviders.Select(static value => value.ProviderId)
                .Where(value => !string.Equals(value, provider.Id, StringComparison.OrdinalIgnoreCase)),
            provider.Id,
            allowPotentiallyLossy);

        var status = await orchestrator.GetStatusAsync(plan, cancellationToken);
        if (status.State != WorkspaceState.Running)
        {
            await orchestrator.UpAsync(plan, cancellationToken);
        }

        foreach (var agent in normalizedAgents)
        {
            await provider.ConfigureAsync(plan, agent, cancellationToken);
        }

        var selections = current.EffectiveProviders
            .Where(value => !string.Equals(value.ProviderId, provider.Id, StringComparison.OrdinalIgnoreCase))
            .Append(new TokenProviderSelection(provider.Id, normalizedAgents))
            .OrderBy(static value => value.ProviderId, StringComparer.Ordinal)
            .ToArray();

        var updated = new TokenOptimizationConfiguration(
            plan.Project.Id,
            true,
            profile,
            selections);
        await store.SaveAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<TokenOptimizationConfiguration> DisableAsync(
        WorkspaceDeploymentPlan plan,
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var current = await store.GetAsync(plan.Project.Id, cancellationToken);
        var target = current.EffectiveProviders.FirstOrDefault(value =>
            string.Equals(value.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return current;
        }

        var optimizer = registry.GetRequired(providerId);
        var status = await orchestrator.GetStatusAsync(plan, cancellationToken);
        if (status.State == WorkspaceState.Running)
        {
            await optimizer.DisableAsync(plan, cancellationToken);
        }

        var remaining = current.EffectiveProviders
            .Where(value => !string.Equals(value.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var updated = current with
        {
            Enabled = remaining.Length > 0,
            Profile = remaining.Length > 0 ? current.Profile : TokenOptimizationProfile.Off,
            Providers = remaining
        };
        await store.SaveAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<TokenOptimizationConfiguration> ConfigureProfileAsync(
        string projectId,
        TokenOptimizationProfile profile,
        CancellationToken cancellationToken = default)
    {
        var current = await store.GetAsync(projectId, cancellationToken);
        if (profile == TokenOptimizationProfile.Off && current.EffectiveProviders.Count > 0)
        {
            throw new InvalidOperationException(
                "HS5002: Disable configured providers before switching profile to off.");
        }

        var updated = current with { Profile = profile };
        await store.SaveAsync(updated, cancellationToken);
        return updated;
    }

    public Task<TokenOptimizationConfiguration> GetAsync(
        string projectId,
        CancellationToken cancellationToken = default) =>
        store.GetAsync(projectId, cancellationToken);

    public async Task<IReadOnlyList<TokenOptimizerHealth>> DoctorAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var configuration = await store.GetAsync(plan.Project.Id, cancellationToken);
        var results = new List<TokenOptimizerHealth>();
        foreach (var optimizer in registry.All.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            var health = await optimizer.InspectAsync(plan, cancellationToken);
            var enabled = configuration.EffectiveProviders.Any(value =>
                string.Equals(value.ProviderId, optimizer.Id, StringComparison.OrdinalIgnoreCase));
            results.Add(health with
            {
                Details = enabled
                    ? $"enabled; {health.Details}"
                    : $"disabled; {health.Details}"
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<TokenGainMetrics>> GainAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var configuration = await store.GetAsync(plan.Project.Id, cancellationToken);
        var gains = new List<TokenGainMetrics>();

        foreach (var selection in configuration.EffectiveProviders)
        {
            var optimizer = registry.GetRequired(selection.ProviderId);
            var gain = optimizer.SupportsGainMetrics
                ? await optimizer.ReadGainAsync(plan, cancellationToken)
                : new TokenGainMetrics(
                    optimizer.Id,
                    TokenMetricEvidence.Unavailable,
                    null,
                    null,
                    null,
                    null,
                    "Provider does not expose a HermesStack-safe aggregate metric surface.");
            gains.Add(gain);

            if (gain.Evidence != TokenMetricEvidence.Unavailable)
            {
                await metrics.AppendAsync(
                    new TokenMetricRecord(
                        DateTimeOffset.UtcNow,
                        plan.Project.Id,
                        "all",
                        gain.ProviderId,
                        gain.Evidence,
                        gain.RawTokens,
                        gain.OptimizedTokens,
                        gain.SavedTokens,
                        gain.SavingsPercent),
                    cancellationToken);
            }
        }

        return gains;
    }

    public Task<IReadOnlyList<TokenMetricRecord>> HistoryAsync(
        string projectId,
        CancellationToken cancellationToken = default) =>
        metrics.ReadAsync(projectId, cancellationToken);
}
