using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;
using HermesStack.Docker.Compose;

namespace HermesStack.UnitTests.Orchestration;

public sealed class ComposeOverrideWriterTests
{
    [Fact]
    public async Task Generated_override_keeps_security_sensitive_values_out()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var output = Path.Combine(root, "compose.override.yaml");
        var project = new ProjectDefinition("demo", "Demo", Path.Combine(root, "project"));
        Directory.CreateDirectory(project.HostPath);
        var plan = new WorkspaceDeploymentPlan(
            project,
            "compose",
            "hstack/workspace-full:0.1.0",
            "compose.yaml",
            output,
            [new(project.HostPath, "/workspace", false, "project")],
            new Dictionary<string, string> { ["HOME"] = "/home/hstack" },
            [new ProjectPort(5000)],
            new WorkspaceSecurityPolicy(),
            root);

        await new ComposeOverrideWriter().WriteAsync(plan);
        var yaml = await File.ReadAllTextAsync(output);

        Assert.Contains("127.0.0.1:5000:5000", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("docker.sock", yaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("privileged: true", yaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("io.hstack.managed", yaml, StringComparison.Ordinal);
    }
}
