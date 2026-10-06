using HermesStack.Application.Abstractions;
using HermesStack.Domain.Context;

namespace HermesStack.Application.Context;

public sealed class ContextService(
    IContextProviderRegistry providers,
    IContextConfigurationStore configurations,
    IContextSecretFilter secretFilter,
    IContextTraceStore traces,
    ContextBudgetPolicy budgetPolicy)
{
    public Task<ContextConfiguration> GetConfigurationAsync(
        string projectId,
        CancellationToken cancellationToken = default) =>
        configurations.GetAsync(projectId, cancellationToken);

    public async Task<ContextConfiguration> SetEnabledAsync(
        string projectId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var current = await configurations.GetAsync(projectId, cancellationToken);
        var updated = current with { Enabled = enabled };
        await configurations.SaveAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<ContextProviderProjectInfo> EnsureProjectAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await RequireEnabledAsync(projectId, cancellationToken);
        return await providers.GetRequired(configuration.ProviderId)
            .EnsureProjectAsync(projectId, cancellationToken);
    }

    public async Task<IReadOnlyList<ContextItem>> SearchAsync(
        ContextQuery query,
        CancellationToken cancellationToken = default)
    {
        var configuration = await RequireEnabledAsync(query.ProjectId, cancellationToken);
        var provider = providers.GetRequired(configuration.ProviderId);
        _ = await provider.EnsureProjectAsync(query.ProjectId, cancellationToken);

        var effectiveBudget = configuration.EffectiveBudget with
        {
            MaxItems = query.MaxItems ?? configuration.EffectiveBudget.MaxItems,
            MaxTokens = query.MaxTokens ?? configuration.EffectiveBudget.MaxTokens
        };

        var raw = await provider.RetrieveAsync(
            query with
            {
                MaxItems = effectiveBudget.MaxItems,
                MaxTokens = effectiveBudget.MaxTokens
            },
            cancellationToken);

        var accepted = budgetPolicy.Apply(raw, effectiveBudget, out var excluded);
        var target = provider is IContextScopeMapper mapper
            ? mapper.GetSearchRoot(query)
            : query.Scope.ToString();

        await traces.SaveAsync(
            new ContextRetrievalTrace(
                DateTimeOffset.UtcNow,
                query.ProjectId,
                provider.Id,
                query.Query,
                query.Scope,
                target,
                effectiveBudget.MaxItems,
                accepted.Count,
                accepted.Sum(static item => item.EstimatedTokens),
                accepted.Select(static item => item.Uri).ToArray(),
                excluded),
            cancellationToken);

        return accepted;
    }

    public async Task StoreAsync(
        ContextWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuration = await RequireEnabledAsync(request.ProjectId, cancellationToken);
        if (configuration.CaptureMode == ContextCaptureMode.Off)
        {
            throw new InvalidOperationException("Context capture is disabled for this project.");
        }

        var filtered = secretFilter.Filter(request.Content);
        if (filtered.Rejected)
        {
            throw new InvalidOperationException(
                $"HS3016: Context write rejected: {filtered.Reason}");
        }

        var provider = providers.GetRequired(configuration.ProviderId);
        _ = await provider.EnsureProjectAsync(request.ProjectId, cancellationToken);
        await provider.StoreAsync(request with { Content = filtered.Content }, cancellationToken);
    }

    public async Task ShareAsync(
        ContextShareRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuration = await RequireEnabledAsync(request.ProjectId, cancellationToken);
        await providers.GetRequired(configuration.ProviderId)
            .ShareAsync(request, cancellationToken);
    }

    public async Task ExportAsync(
        string projectId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var configuration = await RequireEnabledAsync(projectId, cancellationToken);
        await providers.GetRequired(configuration.ProviderId)
            .ExportAsync(projectId, outputPath, cancellationToken);
    }

    public async Task ImportAsync(
        string projectId,
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var configuration = await RequireEnabledAsync(projectId, cancellationToken);
        await providers.GetRequired(configuration.ProviderId)
            .ImportAsync(projectId, inputPath, cancellationToken);
    }

    public Task<ContextRetrievalTrace?> GetLatestTraceAsync(
        string projectId,
        CancellationToken cancellationToken = default) =>
        traces.GetLatestAsync(projectId, cancellationToken);

    public async Task<IReadOnlyList<ContextProviderAvailability>> DoctorAsync(
        string? projectId = null,
        CancellationToken cancellationToken = default)
    {
        if (projectId is not null)
        {
            var configuration = await configurations.GetAsync(projectId, cancellationToken);
            if (!configuration.Enabled)
            {
                return [new ContextProviderAvailability(true, Details: "Memory disabled for project.")];
            }

            var provider = providers.GetRequired(configuration.ProviderId);
            var availability = await provider.DetectAsync(cancellationToken);
            if (availability.IsAvailable)
            {
                _ = await provider.EnsureProjectAsync(projectId, cancellationToken);
            }

            return [availability];
        }

        var results = new List<ContextProviderAvailability>();
        foreach (var provider in providers.All)
        {
            results.Add(await provider.DetectAsync(cancellationToken));
        }

        return results;
    }

    private async Task<ContextConfiguration> RequireEnabledAsync(
        string projectId,
        CancellationToken cancellationToken)
    {
        var configuration = await configurations.GetAsync(projectId, cancellationToken);
        if (!configuration.Enabled)
        {
            throw new InvalidOperationException(
                $"Shared context is disabled for project '{projectId}'.");
        }

        return configuration;
    }
}
