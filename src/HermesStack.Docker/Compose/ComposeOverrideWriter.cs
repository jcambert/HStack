using HermesStack.Domain.Orchestration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HermesStack.Docker.Compose;

public sealed class ComposeOverrideWriter
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public async Task WriteAsync(WorkspaceDeploymentPlan plan, CancellationToken cancellationToken = default)
    {
        ValidateSecurity(plan);
        Directory.CreateDirectory(Path.GetDirectoryName(plan.OverrideComposeFile)!);

        var service = new Dictionary<string, object?>
        {
            ["container_name"] = $"hstack-{plan.Project.Id}-workspace",
            ["image"] = plan.WorkspaceImage,
            ["environment"] = plan.Environment,
            ["volumes"] = plan.Mounts.Select(ToVolume).ToArray(),
            ["labels"] = new Dictionary<string, string>
            {
                ["io.hstack.managed"] = "true",
                ["io.hstack.project"] = plan.Project.Id,
                ["io.hstack.kind"] = "workspace",
                ["io.hstack.version"] = "0.5.0"
            },
            ["cpus"] = plan.Project.EffectiveResources.Cpus.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["mem_limit"] = plan.Project.EffectiveResources.Memory,
            ["pids_limit"] = plan.Project.EffectiveResources.Pids
        };

        if (plan.Ports.Count > 0)
        {
            service["ports"] = plan.Ports.Select(port => $"127.0.0.1:{port.EffectiveHost}:{port.Container}").ToArray();
        }

        var document = new Dictionary<string, object>
        {
            ["services"] = new Dictionary<string, object?> { ["workspace"] = service }
        };

        await File.WriteAllTextAsync(plan.OverrideComposeFile, _serializer.Serialize(document), cancellationToken);
    }

    private static Dictionary<string, object> ToVolume(WorkspaceMount mount) => new()
    {
        ["type"] = "bind",
        ["source"] = mount.Source,
        ["target"] = mount.Target,
        ["read_only"] = mount.ReadOnly
    };

    private static void ValidateSecurity(WorkspaceDeploymentPlan plan)
    {
        if (plan.Security.Privileged || plan.Security.HostNetwork || plan.Security.HostPid || plan.Security.HostIpc ||
            !plan.Security.NoNewPrivileges || !plan.Security.DropAllCapabilities || !plan.Security.ReadOnlyRoot)
        {
            throw new InvalidOperationException("HS3007: Mandatory workspace security policy would be violated.");
        }

        if (plan.Mounts.Any(m => m.Source.Contains("docker.sock", StringComparison.OrdinalIgnoreCase) || m.Source.Contains("docker_engine", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("HS3001: Docker daemon access is forbidden.");
        }
    }
}
