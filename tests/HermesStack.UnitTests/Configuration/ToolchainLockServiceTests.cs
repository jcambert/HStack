using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Configuration;

public sealed class ToolchainLockServiceTests
{
    [Fact]
    public void Exact_m5_versions_are_loaded()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "toolchain.lock.yaml");

        try
        {
            File.WriteAllText(path, """
                schemaVersion: 1
                workspace:
                  version: "0.5.0"
                tools:
                  herdr:
                    version: "0.9.3"
                    releaseTag: "v0.9.3"
                    sha256X64: "18a8dc65f1c2fa485884344356dea1cfd911c6f06cf46fa78e193f4087f4dba7"
                    sha256Arm64: "4de7aa3e25678812e92960de64f7c2aaa1bca1f0f80a3c5e559837e231e1f5c0"
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
                  rtk:
                    version: "0.51.0"
                    releaseTag: "v0.51.0"
                    sha256X64: "5028d3b19a8f0990d30fec9fbb07e32782bc5698e618fb1861aad8a9ccba4eb5"
                    sha256Arm64: "8d6d1aad9e69b42481eda7039507d1f7ee93698f87713cecd873d287c1931632"
                  caveman:
                    version: "2.7.0"
                    releaseTag: "v2.7.0"
                    commit: "8b0c1d3699b8d83e87fe4605b378da20c41555e0"
                """);

            var value = new ToolchainLockService().Load(path);

            Assert.Equal("0.5.0", value.WorkspaceVersion);
            Assert.Equal("0.9.3", value.HerdrVersion);
            Assert.Equal("v0.9.3", value.HerdrReleaseTag);
            Assert.Equal("18a8dc65f1c2fa485884344356dea1cfd911c6f06cf46fa78e193f4087f4dba7", value.HerdrSha256X64);
            Assert.Equal("4de7aa3e25678812e92960de64f7c2aaa1bca1f0f80a3c5e559837e231e1f5c0", value.HerdrSha256Arm64);
            Assert.Equal("2.1.289", value.ClaudeCodeVersion);
            Assert.Equal("0.160.0", value.CodexVersion);
            Assert.Equal("0.21.5", value.HermesVersion);
            Assert.Equal("v2026.9.24", value.HermesReleaseTag);
            Assert.Equal("f97608f178d1ffeca59860195ab7da295f7c8e5f", value.HermesCommit);
            Assert.Equal("1.18.34", value.OpenCodeVersion);
            Assert.Equal("0.51.0", value.RtkVersion);
            Assert.Equal("v0.51.0", value.RtkReleaseTag);
            Assert.Equal("5028d3b19a8f0990d30fec9fbb07e32782bc5698e618fb1861aad8a9ccba4eb5", value.RtkSha256X64);
            Assert.Equal("8d6d1aad9e69b42481eda7039507d1f7ee93698f87713cecd873d287c1931632", value.RtkSha256Arm64);
            Assert.Equal("2.7.0", value.CavemanVersion);
            Assert.Equal("v2.7.0", value.CavemanReleaseTag);
            Assert.Equal("8b0c1d3699b8d83e87fe4605b378da20c41555e0", value.CavemanCommit);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Latest_deferred_and_invalid_digests_are_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "toolchain.lock.yaml");

        try
        {
            File.WriteAllText(path, """
                schemaVersion: 1
                workspace:
                  version: "0.5.0"
                tools:
                  herdr:
                    version: "deferred-to-M3"
                    releaseTag: "v0.9.3"
                    sha256X64: "invalid"
                    sha256Arm64: "4de7aa3e25678812e92960de64f7c2aaa1bca1f0f80a3c5e559837e231e1f5c0"
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
                  rtk:
                    version: "0.51.0"
                    releaseTag: "v0.51.0"
                    sha256X64: "5028d3b19a8f0990d30fec9fbb07e32782bc5698e618fb1861aad8a9ccba4eb5"
                    sha256Arm64: "8d6d1aad9e69b42481eda7039507d1f7ee93698f87713cecd873d287c1931632"
                  caveman:
                    version: "2.7.0"
                    releaseTag: "v2.7.0"
                    commit: "8b0c1d3699b8d83e87fe4605b378da20c41555e0"
                """);

            Assert.Throws<InvalidDataException>(() => new ToolchainLockService().Load(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
