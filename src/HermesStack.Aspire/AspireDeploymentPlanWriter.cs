using System.Security;
using System.Text.Json;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;

namespace HermesStack.Aspire;

public sealed record AspireRuntimeFiles(
    string RuntimeDirectory,
    string DeploymentFile,
    string AppHostProjectFile,
    string StartResultFile);

public sealed class AspireDeploymentPlanWriter(
    IDataRootProvider dataRoot,
    string appHostSourcePath,
    string aspireVersion)
{
    public async Task<AspireRuntimeFiles> WriteAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        Validate(plan);

        if (!File.Exists(appHostSourcePath))
        {
            throw new FileNotFoundException(
                "HermesStack generic Aspire AppHost source was not found.",
                appHostSourcePath);
        }

        var runtimeDirectory = Path.Combine(
            dataRoot.RuntimeDirectory,
            "aspire",
            plan.Project.Id);
        var appHostDirectory = Path.Combine(runtimeDirectory, "apphost");
        Directory.CreateDirectory(appHostDirectory);

        var deploymentFile = Path.Combine(runtimeDirectory, "deployment.json");
        var appHostProjectFile = Path.Combine(
            appHostDirectory,
            "HermesStack.Runtime.AppHost.csproj");
        var startResultFile = Path.Combine(runtimeDirectory, "start.json");

        var document = new AspireDeploymentDocument(
            1,
            plan.Project.Id,
            plan.OrchestratorId,
            plan.WorkspaceImage,
            plan.Mounts
                .Select(static mount => new AspireMount(
                    mount.Source,
                    mount.Target,
                    mount.ReadOnly,
                    mount.Purpose))
                .ToArray(),
            plan.Environment,
            plan.Ports
                .Select(static port => new AspirePort(
                    port.Container,
                    port.EffectiveHost,
                    port.Bind))
                .ToArray(),
            plan.Security,
            new AspireResources(
                plan.Project.EffectiveResources.Cpus,
                plan.Project.EffectiveResources.Memory,
                plan.Project.EffectiveResources.Pids));

        var json = JsonSerializer.Serialize(
            document,
            new JsonSerializerOptions { WriteIndented = true });
        await AtomicWriteAsync(
            deploymentFile,
            json + Environment.NewLine,
            cancellationToken);

        var escapedSource = SecurityElement.Escape(Path.GetFullPath(appHostSourcePath))
            ?? throw new InvalidOperationException("Could not encode the AppHost source path.");
        var escapedVersion = SecurityElement.Escape(aspireVersion)
            ?? throw new InvalidOperationException("Could not encode the Aspire version.");

        var project = $"""
            <Project Sdk="Aspire.AppHost.Sdk/{escapedVersion}">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <AspireUseCliBundle>true</AspireUseCliBundle>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Aspire.Hosting.AppHost" Version="{escapedVersion}" />
                <Compile Include="{escapedSource}" Link="AppHost.cs" />
              </ItemGroup>
            </Project>
            """;
        await AtomicWriteAsync(
            appHostProjectFile,
            project + Environment.NewLine,
            cancellationToken);

        return new AspireRuntimeFiles(
            runtimeDirectory,
            deploymentFile,
            appHostProjectFile,
            startResultFile);
    }

    public AspireRuntimeFiles GetFiles(string projectId)
    {
        var runtimeDirectory = Path.Combine(
            dataRoot.RuntimeDirectory,
            "aspire",
            projectId);
        return new AspireRuntimeFiles(
            runtimeDirectory,
            Path.Combine(runtimeDirectory, "deployment.json"),
            Path.Combine(runtimeDirectory, "apphost", "HermesStack.Runtime.AppHost.csproj"),
            Path.Combine(runtimeDirectory, "start.json"));
    }

    private static void Validate(WorkspaceDeploymentPlan plan)
    {
        if (!string.Equals(plan.OrchestratorId, "aspire", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Aspire plan writer cannot render orchestrator '{plan.OrchestratorId}'.");
        }

        if (plan.Security.Privileged ||
            !plan.Security.NoNewPrivileges ||
            !plan.Security.DropAllCapabilities ||
            plan.Security.HostNetwork ||
            plan.Security.HostPid ||
            plan.Security.HostIpc ||
            !plan.Security.ReadOnlyRoot)
        {
            throw new InvalidOperationException(
                "HS3007: Mandatory workspace security policy would be violated by the Aspire deployment.");
        }

        if (plan.Mounts.Any(static mount =>
            mount.Source.Contains("docker.sock", StringComparison.OrdinalIgnoreCase) ||
            mount.Source.Contains("docker_engine", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "HS3001: Docker daemon access is forbidden.");
        }

        if (plan.Ports.Any(static port =>
            !string.Equals(port.Bind, "127.0.0.1", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "HS3008: Aspire ports must bind to 127.0.0.1.");
        }
    }

    private static async Task AtomicWriteAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content, cancellationToken);
        File.Move(temp, path, true);
    }

    private sealed record AspireDeploymentDocument(
        int SchemaVersion,
        string ProjectId,
        string OrchestratorId,
        string WorkspaceImage,
        IReadOnlyList<AspireMount> Mounts,
        IReadOnlyDictionary<string, string> Environment,
        IReadOnlyList<AspirePort> Ports,
        WorkspaceSecurityPolicy Security,
        AspireResources Resources);

    private sealed record AspireMount(
        string Source,
        string Target,
        bool ReadOnly,
        string Purpose);

    private sealed record AspirePort(
        int Container,
        int Host,
        string Bind);

    private sealed record AspireResources(
        double Cpus,
        string Memory,
        int Pids);
}
