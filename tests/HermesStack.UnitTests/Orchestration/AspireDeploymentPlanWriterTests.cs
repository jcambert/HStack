using System.Xml.Linq;
using HermesStack.Aspire;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;
using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Orchestration;

public sealed class AspireDeploymentPlanWriterTests
{
    [Fact]
    public async Task Generated_AppHost_scopes_experimental_certificate_diagnostic_without_disabling_other_warnings()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-aspire-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var appHostSource = Path.Combine(root, "AppHost.cs");
            await File.WriteAllTextAsync(appHostSource, "// Placeholder AppHost for rendering tests.");
            var writer = new AspireDeploymentPlanWriter(
                new DefaultDataRootProvider(Path.Combine(root, "home")),
                appHostSource,
                "13.6.0");

            var output = await writer.WriteAsync(CreatePlan(root));
            var project = XDocument.Load(output.AppHostProjectFile);
            var properties = project.Root?.Element("PropertyGroup");

            Assert.NotNull(properties);
            Assert.Equal("Aspire.AppHost.Sdk/13.6.0", project.Root?.Attribute("Sdk")?.Value);
            Assert.Equal("true", properties.Element("TreatWarningsAsErrors")?.Value);
            Assert.Equal(
                "$(NoWarn);ASPIRECERTIFICATES001",
                properties.Element("NoWarn")?.Value);
            Assert.Equal("false", properties.Element("EnableDefaultCompileItems")?.Value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Invalid_read_only_root_policy_is_rejected_before_writing_AppHost()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-aspire-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var appHostSource = Path.Combine(root, "AppHost.cs");
            await File.WriteAllTextAsync(appHostSource, "// Placeholder AppHost for rendering tests.");
            var writer = new AspireDeploymentPlanWriter(
                new DefaultDataRootProvider(Path.Combine(root, "home")),
                appHostSource,
                "13.6.0");

            var plan = CreatePlan(root) with
            {
                Security = new WorkspaceSecurityPolicy(ReadOnlyRoot: false)
            };

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => writer.WriteAsync(plan));
            Assert.Contains("HS3007", error.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(writer.GetFiles("demo").AppHostProjectFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static WorkspaceDeploymentPlan CreatePlan(string root) =>
        new(
            new ProjectDefinition("demo", "Demo", Path.Combine(root, "project")),
            "aspire",
            "hstack/workspace-full:0.8.0",
            Path.Combine(root, "compose.yaml"),
            Path.Combine(root, "compose.override.yaml"),
            [],
            new Dictionary<string, string>(StringComparer.Ordinal),
            [],
            new WorkspaceSecurityPolicy(),
            Path.Combine(root, "project-data"));
}
