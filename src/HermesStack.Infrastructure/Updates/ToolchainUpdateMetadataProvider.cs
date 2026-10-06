using HermesStack.Application.Updates;
using HermesStack.Domain.Updates;
using HermesStack.Infrastructure.Configuration;

namespace HermesStack.Infrastructure.Updates;

public sealed class ToolchainUpdateMetadataProvider(
    HttpClient httpClient,
    ToolchainLockService lockService,
    Uri? manifestUri = null) : IUpdateMetadataProvider
{
    private const int MaximumManifestBytes = 256 * 1024;

    public static Uri DefaultManifestUri { get; } = new(
        "https://raw.githubusercontent.com/jcambert/HStack/main/toolchain.lock.yaml");

    private readonly Uri _manifestUri = ValidateManifestUri(manifestUri ?? DefaultManifestUri);

    public string Source => _manifestUri.ToString();

    public async Task<IReadOnlyList<ManagedComponentVersion>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            _manifestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumManifestBytes)
        {
            throw new InvalidDataException(
                "HS7001: Update manifest exceeds the allowed size.");
        }

        var yaml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (yaml.Length > MaximumManifestBytes)
        {
            throw new InvalidDataException(
                "HS7001: Update manifest exceeds the allowed size.");
        }

        return ToManagedComponents(lockService.Parse(yaml));
    }

    public static IReadOnlyList<ManagedComponentVersion> ToManagedComponents(
        ToolchainVersions toolchain) =>
    [
        new("hstack", "HermesStack", toolchain.WorkspaceVersion),
        new("workspace", "Workspace image", toolchain.WorkspaceVersion),
        new("hermes", "Hermes", toolchain.HermesVersion),
        new("herdr", "Herdr", toolchain.HerdrVersion),
        new("claude", "Claude Code", toolchain.ClaudeCodeVersion),
        new("codex", "Codex", toolchain.CodexVersion),
        new("opencode", "OpenCode", toolchain.OpenCodeVersion),
        new("rtk", "RTK", toolchain.RtkVersion),
        new("caveman", "Caveman", toolchain.CavemanVersion),
        new("openviking", "OpenViking", toolchain.OpenVikingVersion)
    ];

    private static Uri ValidateManifestUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException(
                "HS7002: Update metadata must be loaded over HTTPS.");
        }

        return uri;
    }
}
