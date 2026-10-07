using HermesStack.Domain.Updates;

namespace HermesStack.Application.Updates;

public sealed class UpdateCheckService(IUpdateMetadataProvider metadataProvider)
{
    public async Task<UpdateCheckResult> CheckAsync(
        IReadOnlyList<ManagedComponentVersion> current,
        CancellationToken cancellationToken = default)
    {
        var available = await metadataProvider.GetAvailableVersionsAsync(cancellationToken);
        var availableById = available.ToDictionary(
            static item => item.Id,
            StringComparer.OrdinalIgnoreCase);

        var items = current
            .Select(item => BuildItem(item, availableById))
            .ToArray();

        return new UpdateCheckResult(metadataProvider.Source, items);
    }

    private static UpdateCheckItem BuildItem(
        ManagedComponentVersion current,
        IReadOnlyDictionary<string, ManagedComponentVersion> availableById)
    {
        if (!availableById.TryGetValue(current.Id, out var available))
        {
            return new UpdateCheckItem(
                current.Id,
                current.DisplayName,
                current.Version,
                "-",
                UpdateState.Unavailable);
        }

        return new UpdateCheckItem(
            current.Id,
            current.DisplayName,
            current.Version,
            available.Version,
            Compare(current.Version, available.Version));
    }

    private static UpdateState Compare(string current, string available)
    {
        if (string.Equals(current, available, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateState.UpToDate;
        }

        if (TryParseVersion(current, out var currentVersion) &&
            TryParseVersion(available, out var availableVersion))
        {
            var comparison = availableVersion.CompareTo(currentVersion);
            return comparison > 0
                ? UpdateState.UpdateAvailable
                : comparison < 0
                    ? UpdateState.Ahead
                    : UpdateState.Different;
        }

        return UpdateState.Different;
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith('v'))
        {
            normalized = normalized[1..];
        }

        var separator = normalized.IndexOfAny(['-', '+']);
        if (separator >= 0)
        {
            normalized = normalized[..separator];
        }

        if (Version.TryParse(normalized, out var parsed))
        {
            version = parsed;
            return true;
        }

        version = new Version(0, 0);
        return false;
    }
}
