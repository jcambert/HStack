using HermesStack.Domain.Context;

namespace HermesStack.Application.Abstractions;

public interface IContextProvider
{
    string Id { get; }
    string DisplayName { get; }

    Task<ContextProviderAvailability> DetectAsync(CancellationToken cancellationToken = default);

    Task<ContextProviderProjectInfo> EnsureProjectAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task<ContextSession> OpenSessionAsync(
        ContextSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContextItem>> RetrieveAsync(
        ContextQuery query,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        ContextWriteRequest request,
        CancellationToken cancellationToken = default);

    Task ShareAsync(
        ContextShareRequest request,
        CancellationToken cancellationToken = default);

    Task ExportAsync(
        string projectId,
        string outputPath,
        CancellationToken cancellationToken = default);

    Task ImportAsync(
        string projectId,
        string inputPath,
        CancellationToken cancellationToken = default);
}

public interface IContextProviderRegistry
{
    IReadOnlyCollection<IContextProvider> All { get; }
    IContextProvider GetRequired(string id);
}

public interface IContextConfigurationStore
{
    Task<ContextConfiguration> GetAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ContextConfiguration configuration,
        CancellationToken cancellationToken = default);
}

public interface IContextSecretFilter
{
    ContextSecretFilterResult Filter(string content);
}

public interface IContextTraceStore
{
    Task SaveAsync(
        ContextRetrievalTrace trace,
        CancellationToken cancellationToken = default);

    Task<ContextRetrievalTrace?> GetLatestAsync(
        string projectId,
        CancellationToken cancellationToken = default);
}

public interface IContextScopePolicy
{
    void ValidateQuery(ContextQuery query);
    void ValidateWrite(ContextWriteRequest request);
    void ValidateShare(ContextShareRequest request);
}

public interface IContextScopeMapper
{
    string GetSearchRoot(ContextQuery query);
    string GetWriteUri(ContextWriteRequest request);
    string GetSharedNamespaceUri(string @namespace);
}
