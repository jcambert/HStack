using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HermesStack.Infrastructure.Configuration;

public sealed record ToolchainVersions(
    string WorkspaceVersion,
    string AspireVersion,
    string HerdrVersion,
    string HerdrReleaseTag,
    string HerdrSha256X64,
    string HerdrSha256Arm64,
    string ClaudeCodeVersion,
    string CodexVersion,
    string HermesVersion,
    string HermesReleaseTag,
    string HermesCommit,
    string OpenCodeVersion,
    string RtkVersion,
    string RtkReleaseTag,
    string RtkSha256X64,
    string RtkSha256Arm64,
    string CavemanVersion,
    string CavemanReleaseTag,
    string CavemanCommit,
    string OpenVikingVersion,
    string OpenVikingReleaseTag,
    string OpenVikingImageDigest)
{
    public string OpenVikingImage =>
        $"ghcr.io/volcengine/openviking:v{OpenVikingVersion}@sha256:{OpenVikingImageDigest}";
}

public sealed class ToolchainLockService
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public ToolchainVersions Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("HermesStack toolchain lock file was not found.", path);
        }

        return Parse(File.ReadAllText(path));
    }

    public ToolchainVersions LoadEffective(string embeddedPath, string overridePath)
    {
        var effectivePath = File.Exists(overridePath) ? overridePath : embeddedPath;
        return Load(effectivePath);
    }

    public ToolchainVersions Parse(string yaml)
    {
        var document = ParseDocument(yaml);
        return BuildVersions(document, requireAspire: true);
    }

    internal (ToolchainVersions Versions, bool HasAspire) ParseForUpdateCheck(string yaml)
    {
        var document = ParseDocument(yaml);
        var hasAspire = !string.IsNullOrWhiteSpace(document.Tools?.Aspire?.Version);
        return (BuildVersions(document, requireAspire: false), hasAspire);
    }

    private ToolchainLockDocument ParseDocument(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new InvalidDataException("toolchain.lock.yaml is empty.");
        }

        var document = _deserializer.Deserialize<ToolchainLockDocument>(yaml)
            ?? throw new InvalidDataException("toolchain.lock.yaml is empty.");

        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported toolchain lock schemaVersion {document.SchemaVersion}.");
        }

        return document;
    }

    private static ToolchainVersions BuildVersions(
        ToolchainLockDocument document,
        bool requireAspire)
    {
        var aspireVersion = document.Tools?.Aspire?.Version;
        return new ToolchainVersions(
            Require(document.Workspace?.Version, "workspace.version"),
            requireAspire
                ? Require(aspireVersion, "tools.aspire.version")
                : string.IsNullOrWhiteSpace(aspireVersion)
                    ? "0.0.0"
                    : Require(aspireVersion, "tools.aspire.version"),
            Require(document.Tools?.Herdr?.Version, "tools.herdr.version"),
            Require(document.Tools?.Herdr?.ReleaseTag, "tools.herdr.releaseTag"),
            RequireDigest(document.Tools?.Herdr?.Sha256X64, "tools.herdr.sha256X64"),
            RequireDigest(document.Tools?.Herdr?.Sha256Arm64, "tools.herdr.sha256Arm64"),
            Require(document.Tools?.ClaudeCode?.Version, "tools.claudeCode.version"),
            Require(document.Tools?.Codex?.Version, "tools.codex.version"),
            Require(document.Tools?.Hermes?.Version, "tools.hermes.version"),
            Require(document.Tools?.Hermes?.ReleaseTag, "tools.hermes.releaseTag"),
            Require(document.Tools?.Hermes?.Commit, "tools.hermes.commit"),
            Require(document.Tools?.OpenCode?.Version, "tools.openCode.version"),
            Require(document.Tools?.Rtk?.Version, "tools.rtk.version"),
            Require(document.Tools?.Rtk?.ReleaseTag, "tools.rtk.releaseTag"),
            RequireDigest(document.Tools?.Rtk?.Sha256X64, "tools.rtk.sha256X64"),
            RequireDigest(document.Tools?.Rtk?.Sha256Arm64, "tools.rtk.sha256Arm64"),
            Require(document.Tools?.Caveman?.Version, "tools.caveman.version"),
            Require(document.Tools?.Caveman?.ReleaseTag, "tools.caveman.releaseTag"),
            Require(document.Tools?.Caveman?.Commit, "tools.caveman.commit"),
            Require(document.Tools?.OpenViking?.Version, "tools.openViking.version"),
            Require(document.Tools?.OpenViking?.ReleaseTag, "tools.openViking.releaseTag"),
            RequireDigest(document.Tools?.OpenViking?.ImageDigest, "tools.openViking.imageDigest"));
    }

    private static string Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains("deferred", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "latest", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Pinned value required for '{key}'.");
        }

        return value.Trim();
    }

    private static string RequireDigest(string? value, string key)
    {
        var digest = Require(value, key).ToLowerInvariant();
        if (digest.Length != 64 || digest.Any(static character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"A 64-character SHA-256 digest is required for '{key}'.");
        }

        return digest;
    }

    public sealed class ToolchainLockDocument
    {
        public int SchemaVersion { get; set; }
        public WorkspaceEntry? Workspace { get; set; }
        public ToolsEntry? Tools { get; set; }
    }

    public sealed class WorkspaceEntry
    {
        public string? Version { get; set; }
    }

    public sealed class ToolsEntry
    {
        public ToolEntry? Aspire { get; set; }
        public ToolEntry? Hermes { get; set; }
        public ToolEntry? Herdr { get; set; }
        public ToolEntry? ClaudeCode { get; set; }
        public ToolEntry? Codex { get; set; }
        public ToolEntry? OpenCode { get; set; }
        public ToolEntry? Rtk { get; set; }
        public ToolEntry? Caveman { get; set; }
        public ToolEntry? OpenViking { get; set; }
    }

    public sealed class ToolEntry
    {
        public string? Version { get; set; }
        public string? ReleaseTag { get; set; }
        public string? Commit { get; set; }
        public string? Sha256X64 { get; set; }
        public string? Sha256Arm64 { get; set; }
        public string? ImageDigest { get; set; }
    }
}
