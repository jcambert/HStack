using System.Text.Json;
using System.Text.RegularExpressions;

var deploymentPath = Environment.GetEnvironmentVariable("HSTACK_ASPIRE_DEPLOYMENT");
if (string.IsNullOrWhiteSpace(deploymentPath))
{
    throw new InvalidOperationException(
        "HSTACK_ASPIRE_DEPLOYMENT must point to a HermesStack-controlled deployment document.");
}

var fullDeploymentPath = Path.GetFullPath(deploymentPath);
if (!File.Exists(fullDeploymentPath))
{
    throw new FileNotFoundException(
        "HermesStack Aspire deployment document was not found.",
        fullDeploymentPath);
}

var document = JsonSerializer.Deserialize<AspireDeploymentDocument>(
    await File.ReadAllTextAsync(fullDeploymentPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("HermesStack Aspire deployment document is invalid.");

Validate(document);

var builder = DistributedApplication.CreateBuilder(args);
var workspace = builder
    .AddContainer("workspace", document.WorkspaceImage)
    .WithContainerName($"hstack-{document.ProjectId}-workspace")
    .WithContainerRuntimeArgs(
        "--init",
        "--read-only",
        "--security-opt", "no-new-privileges",
        "--cap-drop", "ALL",
        "--pids-limit", document.Resources.Pids.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "--cpus", document.Resources.Cpus.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "--memory", document.Resources.Memory,
        "--tmpfs", "/tmp:rw,nosuid,nodev,size=512m",
        "--tmpfs", "/var/tmp:rw,noexec,nosuid,nodev,size=256m",
        "--tmpfs", "/usr/lib/ssl/aspire:rw,noexec,nosuid,nodev,size=16m");

foreach (var mount in document.Mounts)
{
    workspace.WithBindMount(
        mount.Source,
        mount.Target,
        isReadOnly: mount.ReadOnly);
}

foreach (var pair in document.Environment.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
{
    workspace.WithEnvironment(pair.Key, pair.Value);
}

foreach (var port in document.Ports)
{
    workspace.WithContainerRuntimeArgs(
        "--publish",
        $"127.0.0.1:{port.Host}:{port.Container}");
}

builder.Build().Run();

static void Validate(AspireDeploymentDocument document)
{
    if (document.SchemaVersion != 1)
    {
        throw new InvalidDataException(
            $"Unsupported HermesStack Aspire deployment schema {document.SchemaVersion}.");
    }

    if (!Regex.IsMatch(
        document.ProjectId,
        "^[a-z0-9][a-z0-9-]{0,62}$",
        RegexOptions.CultureInvariant))
    {
        throw new InvalidDataException("Invalid HermesStack project id in Aspire deployment.");
    }

    if (!string.Equals(
        document.OrchestratorId,
        "aspire",
        StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException("Aspire AppHost only accepts Aspire deployment plans.");
    }

    if (string.IsNullOrWhiteSpace(document.WorkspaceImage))
    {
        throw new InvalidDataException("Aspire deployment is missing the workspace image.");
    }

    if (document.Security.Privileged ||
        !document.Security.NoNewPrivileges ||
        !document.Security.DropAllCapabilities ||
        document.Security.HostNetwork ||
        document.Security.HostPid ||
        document.Security.HostIpc ||
        !document.Security.ReadOnlyRoot)
    {
        throw new InvalidDataException(
            "HS3007: Aspire deployment would weaken a mandatory workspace security policy.");
    }

    if (document.Mounts.Any(static mount =>
        !Path.IsPathRooted(mount.Source) ||
        mount.Source.Contains("docker.sock", StringComparison.OrdinalIgnoreCase) ||
        mount.Source.Contains("docker_engine", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidDataException(
            "HS3001: Aspire deployment contains an invalid or forbidden host mount.");
    }

    if (document.Ports.Any(static port =>
        !string.Equals(port.Bind, "127.0.0.1", StringComparison.Ordinal) ||
        port.Container is < 1 or > 65535 ||
        port.Host is < 1 or > 65535))
    {
        throw new InvalidDataException(
            "HS3008: Aspire published ports must be valid loopback-only mappings.");
    }
}

internal sealed record AspireDeploymentDocument(
    int SchemaVersion,
    string ProjectId,
    string OrchestratorId,
    string WorkspaceImage,
    IReadOnlyList<AspireMount> Mounts,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<AspirePort> Ports,
    AspireSecurity Security,
    AspireResources Resources);

internal sealed record AspireMount(
    string Source,
    string Target,
    bool ReadOnly,
    string Purpose);

internal sealed record AspirePort(
    int Container,
    int Host,
    string Bind);

internal sealed record AspireSecurity(
    bool Privileged,
    bool NoNewPrivileges,
    bool DropAllCapabilities,
    bool HostNetwork,
    bool HostPid,
    bool HostIpc,
    bool ReadOnlyRoot);

internal sealed record AspireResources(
    double Cpus,
    string Memory,
    int Pids);
