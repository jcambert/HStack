namespace HermesStack.Domain.Updates;

public sealed record ManagedComponentVersion(
    string Id,
    string DisplayName,
    string Version);

public enum UpdateState
{
    UpToDate,
    UpdateAvailable,
    Ahead,
    Different,
    Unavailable
}

public sealed record UpdateCheckItem(
    string Id,
    string DisplayName,
    string CurrentVersion,
    string AvailableVersion,
    UpdateState State);

public sealed record UpdateCheckResult(
    string Source,
    IReadOnlyList<UpdateCheckItem> Items)
{
    public bool HasUpdates => Items.Any(static item =>
        item.State is UpdateState.UpdateAvailable or UpdateState.Different);

    public bool HasUnknowns => Items.Any(static item =>
        item.State == UpdateState.Unavailable);
}
