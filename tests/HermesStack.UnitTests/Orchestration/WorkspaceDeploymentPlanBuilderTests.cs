using HermesStack.Application.Orchestration;
using HermesStack.Application.Security;
using HermesStack.Domain.Projects;
using HermesStack.Infrastructure.Certificates;
using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Orchestration;

public sealed class WorkspaceDeploymentPlanBuilderTests
{
    [Fact]
    public async Task Plan_contains_only_explicit_project_home_and_certificate_mounts()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
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

            var plan = await builder.BuildAsync(new ProjectDefinition("demo", "Demo", projectPath));

            Assert.Equal(3, plan.Mounts.Count);
            Assert.Contains(plan.Mounts, mount => mount.Source == projectPath && mount.Target == "/workspace" && !mount.ReadOnly);
            Assert.Contains(plan.Mounts, mount => mount.Target == "/home/hstack" && !mount.ReadOnly);
            Assert.Contains(plan.Mounts, mount => mount.Target == "/etc/hstack/certs/corporate-ca.crt" && mount.ReadOnly);
            Assert.Equal("/home/hstack/.hstack/certs/ca-bundle.crt", plan.Environment["SSL_CERT_FILE"]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Non_local_port_binding_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
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

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync(project));
            Assert.Contains("HS3008", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
