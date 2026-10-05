namespace HermesStack.Domain.Network;

public sealed record ProxyConfiguration(
    bool Enabled = false,
    string? Http = null,
    string? Https = null,
    IReadOnlyList<string>? NoProxy = null)
{
    private static readonly string[] MandatoryNoProxy =
    [
        "localhost",
        "127.0.0.1",
        "::1",
        "host.docker.internal"
    ];

    public IReadOnlyList<string> EffectiveNoProxy => MandatoryNoProxy
        .Concat(NoProxy ?? [])
        .Select(static value => value.Trim())
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
