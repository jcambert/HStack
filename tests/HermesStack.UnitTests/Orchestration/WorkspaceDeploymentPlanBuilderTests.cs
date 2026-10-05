using HermesStack.Application.Orchestration;
using HermesStack.Application.Security;
using HermesStack.Domain.Projects;
using HermesStack.Infrastructure.Certificates;
using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Orchestration;

public sealed class WorkspaceDeploymentPlanBuilderTests
{
    [Fact]
    public async Task Plan_contains_project_scoped_agent_mounts_and_environment()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(root, "src", "demo");

        try
        {
            Directory.CreateDirectory(projectPath);
            var dataRoot = new DefaultDataRootProvider(Path.Combine(root, "hstack-home"));
            Directory.CreateDirectory(dataRoot.CorporateCertificatesDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(dataRoot.CorporateCertificatesDirectory, "corp.crt"),
                "-----BEGIN CERTIFICATE-----\nTEST\n-----END CERTIFICATE-----\n");

            var builder = new WorkspaceDeploymentPlanBuilder(
                dataRoot,
                new HostMountValidator(new HostMountPolicy()),
                new CertificateBundleService(dataRoot),
                Path.Combine(root, "compose.yaml"));

            var plan = await builder.BuildAsync(
                new ProjectDefinition("demo", "Demo", projectPath));

            Assert.Equal(8, plan.Mounts.Count);
            Assert.Contains(
                plan.Mounts,
                mount => mount.Source == projectPath &&
                    mount.Target == "/workspace" &&
                    !mount.ReadOnly);
            Assert.Contains(
                plan.Mounts,
                mount => mount.Target == "/home/hstack/.claude" &&
                    mount.Purpose == "agent-state:claude");
            Assert.Contains(
                plan.Mounts,
                mount => mount.Target == "/home/hstack/.codex" &&
                    mount.Purpose == "agent-state:codex");
            Assert.Contains(
                plan.Mounts,
                mount => mount.Target == "/home/hstack/.hermes" &&
                    mount.Purpose == "agent-state:hermes");
            Assert.Contains(
                plan.Mounts,
                mount => mount.Target == "/home/hstack/.config/opencode");
            Assert.Contains(
                plan.Mounts,
                mount => mount.Target == "/home/hstack/.local/share/opencode");

            Assert.Equal("hstack-demo", plan.Environment["HERDR_SESSION"]);
            Assert.True(Directory.Exists(Path.Combine(plan.ProjectDataRoot, "home", ".config", "herdr")));
            Assert.Equal("/home/hstack/.claude", plan.Environment["CLAUDE_CONFIG_DIR"]);
            Assert.Equal("/home/hstack/.codex", plan.Environment["CODEX_HOME"]);
            Assert.Equal("/home/hstack/.hermes", plan.Environment["HERMES_HOME"]);
            Assert.Equal("local", plan.Environment["TERMINAL_ENV"]);
            Assert.Equal("1", plan.Environment["OPENCODE_DISABLE_AUTOUPDATE"]);
            Assert.True(Directory.Exists(Path.Combine(plan.ProjectDataRoot, "home", ".config")));
            Assert.True(Directory.Exists(Path.Combine(plan.ProjectDataRoot, "home", ".local", "share")));
            Assert.True(Directory.Exists(Path.Combine(plan.ProjectDataRoot, "home", ".local", "state")));
            Assert.True(Directory.Exists(Path.Combine(plan.ProjectDataRoot, "home", ".cache")));
            Assert.Equal(
                "/home/hstack/.hstack/certs/ca-bundle.crt",
                plan.Environment["SSL_CERT_FILE"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Non_local_port_binding_is_rejected()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(root, "src", "demo");

        try
        {
            Directory.CreateDirectory(projectPath);
            var dataRoot = new DefaultDataRootProvider(Path.Combine(root, "hstack-home"));
            var builder = new WorkspaceDeploymentPlanBuilder(
                dataRoot,
                new HostMountValidator(new HostMountPolicy()),
                new CertificateBundleService(dataRoot),
                Path.Combine(root, "compose.yaml"));

            var project = new ProjectDefinition(
                "demo",
                "Demo",
                projectPath,
                Ports: [new ProjectPort(5000, 5000, "0.0.0.0")]);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => builder.BuildAsync(project));
            Assert.Contains("HS3008", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Non_isolated_agent_state_fails_closed_in_m2()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(root, "src", "demo");

        try
        {
            Directory.CreateDirectory(projectPath);
            var dataRoot = new DefaultDataRootProvider(Path.Combine(root, "hstack-home"));
            var builder = new WorkspaceDeploymentPlanBuilder(
                dataRoot,
                new HostMountValidator(new HostMountPolicy()),
                new CertificateBundleService(dataRoot),
                Path.Combine(root, "compose.yaml"));

            var project = new ProjectDefinition(
                "demo",
                "Demo",
                projectPath,
                StateScope: "shared");

            await Assert.ThrowsAsync<NotSupportedException>(
                () => builder.BuildAsync(project));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
