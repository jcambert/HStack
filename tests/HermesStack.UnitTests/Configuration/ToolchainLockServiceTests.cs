using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Configuration;

public sealed class ToolchainLockServiceTests
{
    [Fact]
    public void Exact_agent_versions_are_loaded()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "toolchain.lock.yaml");

        try
        {
            File.WriteAllText(
                path,
                """
                schemaVersion: 1
                workspace:
                  version: "0.2.0"
                tools:
                  claudeCode:
                    version: "2.1.289"
                  codex:
                    version: "0.160.0"
                  hermes:
                    version: "0.21.5"
                    releaseTag: "v2026.9.24"
                    commit: "f97608f178d1ffeca59860195ab7da295f7c8e5f"
                  openCode:
                    version: "1.18.34"
                """);

            var value = new ToolchainLockService().Load(path);

            Assert.Equal("0.2.0", value.WorkspaceVersion);
            Assert.Equal("2.1.289", value.ClaudeCodeVersion);
            Assert.Equal("0.160.0", value.CodexVersion);
            Assert.Equal("0.21.5", value.HermesVersion);
            Assert.Equal("v2026.9.24", value.HermesReleaseTag);
            Assert.Equal("f97608f178d1ffeca59860195ab7da295f7c8e5f", value.HermesCommit);
            Assert.Equal("1.18.34", value.OpenCodeVersion);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Latest_and_deferred_versions_are_rejected()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "toolchain.lock.yaml");

        try
        {
            File.WriteAllText(
                path,
                """
                schemaVersion: 1
                workspace:
                  version: "0.2.0"
                tools:
                  claudeCode:
                    version: "latest"
                  codex:
                    version: "0.160.0"
                  hermes:
                    version: "0.21.5"
                    releaseTag: "v2026.9.24"
                    commit: "f97608f178d1ffeca59860195ab7da295f7c8e5f"
                  openCode:
                    version: "1.18.34"
                """);

            Assert.Throws<InvalidDataException>(
                () => new ToolchainLockService().Load(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
