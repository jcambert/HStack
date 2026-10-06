namespace HermesStack.Domain.Context;

public enum ContextScope
{
    Session,
    Agent,
    Project,
    Shared,
    Global
}

public enum ContextCaptureMode
{
    Off,
    Manual,
    Selective,
    Automatic
}

public sealed record ContextBudgetOptions(
    int MaxTokens = 12000,
    int MaxItems = 20,
    bool PreferSummary = true,
    bool ExpandOnDemand = true);

public sealed record ContextConfiguration(
    string ProjectId,
    bool Enabled = true,
    string ProviderId = "openviking",
    ContextCaptureMode CaptureMode = ContextCaptureMode.Selective,
    ContextScope DefaultScope = ContextScope.Project,
    ContextBudgetOptions? Budget = null)
{
    public ContextBudgetOptions EffectiveBudget => Budget ?? new ContextBudgetOptions();
}

public sealed record ContextProviderAvailability(
    bool IsAvailable,
    string? Version = null,
    string? Details = null);

public sealed record ContextProviderProjectInfo(
    string ProviderId,
    string ProjectId,
    string AccountId,
    string UserId,
    string Endpoint,
    bool Provisioned);

public sealed record ContextSessionRequest(
    string ProjectId,
    string AgentId,
    string? SessionId = null);

public sealed record ContextSession(
    string Id,
    string ProjectId,
    string AgentId);

public sealed record ContextQuery(
    string ProjectId,
    string Query,
    ContextScope Scope = ContextScope.Project,
    string? AgentId = null,
    int? MaxItems = null,
    int? MaxTokens = null);

public sealed record ContextItem(
    string Uri,
    ContextScope Scope,
    string Content,
    int EstimatedTokens,
    double? Score = null,
    bool AlreadySummarized = true,
    bool AlreadyCompressed = false);

public sealed record ContextWriteRequest(
    string ProjectId,
    ContextScope Scope,
    string Name,
    string Content,
    string? AgentId = null,
    IReadOnlyList<string>? Tags = null);

public sealed record ContextShareRequest(
    string ProjectId,
    string Namespace,
    IReadOnlyList<string> ProjectIds,
    bool AllowWrite = false);

public sealed record ContextRetrievalTrace(
    DateTimeOffset Timestamp,
    string ProjectId,
    string ProviderId,
    string Query,
    ContextScope Scope,
    string TargetUri,
    int RequestedItems,
    int ReturnedItems,
    int EstimatedTokens,
    IReadOnlyList<string> IncludedUris,
    IReadOnlyList<string> ExcludedUris);

public sealed record ContextSecretFilterResult(
    string Content,
    bool Changed,
    bool Rejected = false,
    string? Reason = null);
