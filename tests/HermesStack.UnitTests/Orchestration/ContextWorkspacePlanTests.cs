using HermesStack.Application.Orchestration;
using HermesStack.Application.Security;
using HermesStack.Domain.Projects;
using HermesStack.Infrastructure.Certificates;
using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Orchestration;

public sealed class ContextWorkspacePlanTests
{
    [Fact]
    public async Task Enabled_memory_adds_only_project_client_state_and_internal_endpoint()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        var projectPath = OperatingSystem.IsWindows()
            ? $@"C:\Dev\hstack-tests\{Guid.NewGuid():N}\src\demo"
            : $"/tmp/hstack-tests/{Guid.NewGuid():N}/src/demo";

        try
        {
            Directory.CreateDirectory(projectPath);
            var dataRoot = new DefaultDataRootProvider(Path.Combine(root, "home"));
            await new HStackInitializer(dataRoot).InitializeAsync();
            var config = new HStackConfigStore(dataRoot);
            var context = await config.GetAsync("demo");
            await config.SaveAsync(context with { Enabled = true });
            var builder = new WorkspaceDeploymentPlanBuilder(
                dataRoot,
                new HostMountValidator(new HostMountPolicy()),
                new CertificateBundleService(dataRoot),
                Path.Combine(root, "compose.yaml"),
                contextConfigurationStore: config);

            var plan = await builder.BuildAsync(new ProjectDefinition("demo", "Demo", projectPath));

            Assert.True(plan.Context?.Enabled);
            Assert.Contains(
                plan.Mounts,
                mount => mount.Target == "/home/hstack/.openviking" &&
                         mount.Purpose == "context-client-state:openviking");
            Assert.Equal("http://openviking:1933", plan.Environment["OPENVIKING_URL"]);
            Assert.DoesNotContain(
                plan.Environment.Keys,
                key => key.Contains("API_KEY", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
