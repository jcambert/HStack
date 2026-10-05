using System.Text.Json;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Security;

namespace HermesStack.Docker.Security;

public sealed class DockerWorkspaceSecurityInspector(IProcessRunner processRunner)
{
    public async Task<WorkspaceSecuritySnapshot> InspectAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var fallback = FromPlan(plan, live: false);
        try
        {
            var result = await processRunner.RunAsync(
                new ProcessRequest(
                    "docker",
                    ["inspect", $"hstack-{plan.Project.Id}-workspace"]),
                cancellationToken);
            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return fallback;
            }

            using var document = JsonDocument.Parse(result.StandardOutput);
            if (document.RootElement.ValueKind != JsonValueKind.Array ||
                document.RootElement.GetArrayLength() == 0)
            {
                return fallback;
            }

            return FromDockerJson(document.RootElement[0], plan);
        }
        catch (Exception exception) when (
            exception is JsonException or
            System.ComponentModel.Win32Exception or
            FileNotFoundException)
        {
            return fallback;
        }
    }

    private static WorkspaceSecuritySnapshot FromDockerJson(
        JsonElement root,
        WorkspaceDeploymentPlan plan)
    {
        var hostConfig = root.GetProperty("HostConfig");
        var config = root.GetProperty("Config");
        var networkSettings = root.GetProperty("NetworkSettings");

        var mounts = root.TryGetProperty("Mounts", out var mountArray)
            ? mountArray.EnumerateArray().ToArray()
            : [];
        var mountRows = mounts
            .Select(static mount =>
            {
                var source = GetString(mount, "Source");
                var target = GetString(mount, "Destination");
                var rw = mount.TryGetProperty("RW", out var rwElement) && rwElement.GetBoolean();
                return $"{source} -> {target} ({(rw ? "rw" : "ro")})";
            })
            .ToArray();
        var writable = mounts
            .Where(static mount =>
                mount.TryGetProperty("RW", out var rwElement) && rwElement.GetBoolean())
            .Select(static mount =>
                $"{GetString(mount, "Source")} -> {GetString(mount, "Destination")}")
            .ToArray();

        var environment = config.TryGetProperty("Env", out var envArray) &&
                          envArray.ValueKind == JsonValueKind.Array
            ? envArray.EnumerateArray()
                .Select(static item => item.GetString() ?? string.Empty)
                .Select(static item =>
                {
                    var separator = item.IndexOf('=');
                    return separator >= 0 ? item[..separator] : item;
                })
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .OrderBy(static item => item, StringComparer.Ordinal)
                .ToArray()
            : [];

        var networks = new List<string>();
        var networkMode = GetString(hostConfig, "NetworkMode");
        if (!string.IsNullOrWhiteSpace(networkMode))
        {
            networks.Add(networkMode);
        }

        if (networkSettings.TryGetProperty("Networks", out var networkObject) &&
            networkObject.ValueKind == JsonValueKind.Object)
        {
            networks.AddRange(networkObject.EnumerateObject().Select(static property => property.Name));
        }

        var publishedPorts = new List<string>();
        if (networkSettings.TryGetProperty("Ports", out var ports) &&
            ports.ValueKind == JsonValueKind.Object)
        {
            foreach (var port in ports.EnumerateObject())
            {
                if (port.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var binding in port.Value.EnumerateArray())
                {
                    publishedPorts.Add(
                        $"{GetString(binding, "HostIp")}:{GetString(binding, "HostPort")} -> {port.Name}");
                }
            }
        }

        var sources = mounts.Select(static mount => GetString(mount, "Source")).ToArray();
        var destinations = mounts.Select(static mount => GetString(mount, "Destination")).ToArray();

        return new WorkspaceSecuritySnapshot(
            mountRows,
            writable,
            publishedPorts,
            environment,
            [],
            string.IsNullOrWhiteSpace(GetString(config, "User"))
                ? "root"
                : GetString(config, "User"),
            ReadStringArray(hostConfig, "CapAdd"),
            ReadStringArray(hostConfig, "CapDrop"),
            ReadStringArray(hostConfig, "SecurityOpt"),
            networks.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            hostConfig.TryGetProperty("Privileged", out var privileged) && privileged.GetBoolean(),
            sources.Concat(destinations).Any(IsDockerEndpoint),
            ReadDeviceArray(hostConfig),
            GetString(hostConfig, "PidMode"),
            GetString(hostConfig, "IpcMode"),
            sources.Any(IsHostRoot),
            hostConfig.TryGetProperty("ReadonlyRootfs", out var readOnlyRoot) &&
                readOnlyRoot.GetBoolean(),
            true);
    }

    private static WorkspaceSecuritySnapshot FromPlan(
        WorkspaceDeploymentPlan plan,
        bool live)
    {
        return new WorkspaceSecuritySnapshot(
            plan.Mounts.Select(static mount =>
                $"{mount.Source} -> {mount.Target} ({(mount.ReadOnly ? "ro" : "rw")})").ToArray(),
            plan.Mounts.Where(static mount => !mount.ReadOnly)
                .Select(static mount => $"{mount.Source} -> {mount.Target}").ToArray(),
            plan.Ports.Select(static port =>
                $"127.0.0.1:{port.EffectiveHost} -> {port.Container}/tcp").ToArray(),
            plan.Environment.Keys.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            [],
            "hstack",
            [],
            plan.Security.DropAllCapabilities ? ["ALL"] : [],
            plan.Security.NoNewPrivileges ? ["no-new-privileges:true"] : [],
            [plan.Security.HostNetwork ? "host" : "workspace"],
            plan.Security.Privileged,
            plan.Mounts.Any(static mount =>
                IsDockerEndpoint(mount.Source) || IsDockerEndpoint(mount.Target)),
            [],
            plan.Security.HostPid ? "host" : string.Empty,
            plan.Security.HostIpc ? "host" : string.Empty,
            plan.Mounts.Any(static mount => IsHostRoot(mount.Source)),
            plan.Security.ReadOnlyRoot,
            live);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        return element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray()
                .Select(static item => item.GetString() ?? string.Empty)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
            : [];
    }

    private static IReadOnlyList<string> ReadDeviceArray(JsonElement hostConfig)
    {
        if (!hostConfig.TryGetProperty("Devices", out var devices) ||
            devices.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return devices.EnumerateArray()
            .Select(static device =>
                $"{GetString(device, "PathOnHost")} -> {GetString(device, "PathInContainer")}")
            .ToArray();
    }

    private static string GetString(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool IsDockerEndpoint(string value)
    {
        var normalized = value.Replace('\\', '/');
        return normalized.Contains("docker.sock", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("docker_engine", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHostRoot(string value)
    {
        if (value == "/")
        {
            return true;
        }

        var normalized = value.Replace('/', '\\');
        return normalized.Length == 3 &&
               char.IsLetter(normalized[0]) &&
               normalized[1] == ':' &&
               normalized[2] == '\\';
    }
}
