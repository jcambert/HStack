using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HermesStack.Infrastructure.Configuration;

public sealed record ToolchainVersions(
    string WorkspaceVersion,
    string ClaudeCodeVersion,
    string CodexVersion,
    string HermesVersion,
    string HermesReleaseTag,
    string HermesCommit,
    string OpenCodeVersion);

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

        var document = _deserializer.Deserialize<ToolchainLockDocument>(File.ReadAllText(path))
            ?? throw new InvalidDataException("toolchain.lock.yaml is empty.");

        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported toolchain lock schemaVersion {document.SchemaVersion}.");
        }

        return new ToolchainVersions(
            Require(document.Workspace?.Version, "workspace.version"),
            Require(document.Tools?.ClaudeCode?.Version, "tools.claudeCode.version"),
            Require(document.Tools?.Codex?.Version, "tools.codex.version"),
            Require(document.Tools?.Hermes?.Version, "tools.hermes.version"),
            Require(document.Tools?.Hermes?.ReleaseTag, "tools.hermes.releaseTag"),
            Require(document.Tools?.Hermes?.Commit, "tools.hermes.commit"),
            Require(document.Tools?.OpenCode?.Version, "tools.openCode.version"));
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
        public ToolEntry? Hermes { get; set; }
        public ToolEntry? ClaudeCode { get; set; }
        public ToolEntry? Codex { get; set; }
        public ToolEntry? OpenCode { get; set; }
    }

    public sealed class ToolEntry
    {
        public string? Version { get; set; }
        public string? ReleaseTag { get; set; }
        public string? Commit { get; set; }
    }
}
