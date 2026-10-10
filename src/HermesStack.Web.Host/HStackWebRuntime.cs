using HermesStack.Application.Abstractions;
using HermesStack.Application.Agents;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Application.Security;
using HermesStack.Aspire;
using HermesStack.Docker.Compose;
using HermesStack.Docker.Context;
using HermesStack.Docker.Execution;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;
using HermesStack.Infrastructure.Certificates;
using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Operations;
using HermesStack.Infrastructure.Processes;
using HermesStack.Infrastructure.Projects;
using HermesStack.Infrastructure.Security;
using HermesStack.Web.Contracts;

namespace HermesStack.Web.Host;

// A thin UI adapter over the SAME Application services and orchestrators as
// the CLI. No private container manager, no shell command API and no
// credential store ever serialized to the browser.
public sealed class HStackWebRuntime
{
    private readonly ProjectService _projects;
    private readonly WorkspaceDeploymentPlanBuilder _plans;
    private readonly IWorkspaceOrchestrator _orchestrator;
    private readonly IWorkspaceOrchestratorRegistry _registry;
    private readonly AgentHarnessRegistry _agents;
    private readonly HStackConfigStore _settings;
    private readonly IProcessRunner _runner;
    private readonly SecretRedactor _redactor = new();
    private readonly BackupArchiveService _backups;
    private readonly string _expectedAspire;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _locks =
        new(StringComparer.OrdinalIgnoreCase);

    public HStackWebRuntime()
    {
        var root = new DefaultDataRootProvider(
            Environment.GetEnvironmentVariable("HSTACK_HOME"));
        var embeddedAssets = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "assets"));

        string Asset(string packaged, string source)
        {
            var packagedPath = Path.Combine(embeddedAssets, packaged.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(packagedPath))
            {
                return packagedPath;
            }

            // dotnet run may use the project directory as CWD, whereas
            // GitHub Actions invokes it from the solution directory.
            // Search only for a real HStack source checkout (slnx marker).
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
                {
                    if (!File.Exists(Path.Combine(folder.FullName, "HermesStack.slnx")))
                    {
                        continue;
                    }

                    var sourcePath = Path.Combine(
                        folder.FullName, source.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(sourcePath))
                    {
                        return sourcePath;
                    }
                }
            }

            throw new FileNotFoundException(
                "HStack Web requires the matching CLI assets. Run from the repository root " +
                "or install the complete HStack distribution.", packagedPath);
        }

        var toolchainFile = Asset("toolchain.lock.yaml", "toolchain.lock.yaml");
        var toolchain = new ToolchainLockService().LoadEffective(
            toolchainFile,
            Path.Combine(root.ConfigDirectory, "toolchain.lock.yaml"));
        _expectedAspire = toolchain.AspireVersion;
        _settings = new HStackConfigStore(root);
        _runner = new ProcessRunner();
        var certs = new CertificateBundleService(root);
        _projects = new ProjectService(
            new YamlProjectStore(root), new HostMountValidator(new HostMountPolicy()));
        _plans = new WorkspaceDeploymentPlanBuilder(
            root,
            new HostMountValidator(new HostMountPolicy()),
            certs,
            Asset("docker/compose/compose.yaml", "docker/compose/compose.yaml"),
            $"hstack/workspace-full:{toolchain.WorkspaceVersion}",
            _settings, _settings, _settings);

        // Replicate the CLI's dependency wiring (not its business logic).
        var secrets = new LocalProtectedSecretStore(root);
        var memory = new OpenVikingServiceManager(
            root, secrets, _runner, toolchain.OpenVikingImage, toolchain.OpenVikingVersion);
        var compose = new DockerComposeWorkspaceOrchestrator(
            _runner, new ComposeOverrideWriter(), memory);
        var aspire = new AspireWorkspaceOrchestrator(
            _runner,
            new AspireDeploymentPlanWriter(
                root,
                Asset("aspire/apphost/AppHost.cs", "src/HermesStack.AppHost/AppHost.cs"),
                toolchain.AspireVersion),
            new AspireOrchestratorCapabilityEvaluator(),
            toolchain.AspireVersion,
            memory);
        _registry = new WorkspaceOrchestratorRegistry([compose, aspire]);
        _orchestrator = new RoutedWorkspaceOrchestrator(
            _registry, new NativeContainerExecutionProvider(_registry, _runner));
        _agents = new AgentHarnessRegistry([
            new ClaudeCodeHarness(_orchestrator),
            new CodexHarness(_orchestrator),
            new HermesAgentHarness(_orchestrator),
            new OpenCodeHarness(_orchestrator)
        ]);
        _backups = new BackupArchiveService(root, toolchainFile);
    }

    public async Task<IReadOnlyList<ProjectInfo>> ListAsync(CancellationToken ct)
    {
        var list = await _projects.ListAsync(ct);
        var result = new List<ProjectInfo>(list.Count);
        foreach (var project in list)
        {
            result.Add(await DescribeAsync(project, ct));
        }

        return result;
    }

    public async Task<ProjectInfo> GetAsync(string id, CancellationToken ct) =>
        await DescribeAsync(await _projects.GetRequiredAsync(id, ct), ct);

    private async Task<ProjectInfo> DescribeAsync(ProjectDefinition project, CancellationToken ct)
    {
        try
        {
            var plan = await _plans.BuildAsync(project, cancellationToken: ct);
            var status = await _orchestrator.GetStatusAsync(plan, ct);
            return new ProjectInfo(
                project.Id, project.Name, project.HostPath, plan.OrchestratorId,
                status.State.ToString(),
                project.EffectivePorts.Select(p => new PortInfo(p.Container, p.EffectiveHost)).ToArray(),
                status.Details is null ? null : _redactor.Redact(status.Details));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new ProjectInfo(
                project.Id, project.Name, project.HostPath,
                project.Orchestrator ?? "compose", "Unknown",
                project.EffectivePorts.Select(p => new PortInfo(p.Container, p.EffectiveHost)).ToArray(),
                _redactor.Redact(e.Message));
        }
    }

    public async Task<ProjectInfo> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var project = await _projects.AddAsync(request.Id, request.HostPath, request.Name, ct);
        return await DescribeAsync(project, ct);
    }

    public async Task<ProjectInfo> EditAsync(string id, EditProjectRequest edit, CancellationToken ct)
    {
        await RequireStoppedAsync(id, ct);
        _ = await _projects.EditAsync(id, edit.HostPath, edit.Name, ct);
        if (edit.Orchestrator is not null)
        {
            // Availability checked rather than silently changing the runtime.
            var selected = _registry.GetRequired(edit.Orchestrator);
            var availability = await selected.DetectAsync(ct);
            if (!availability.IsAvailable)
            {
                throw new InvalidOperationException(
                    "Orchestrator is unavailable: " + availability.Reason);
            }

            _ = await _projects.SetOrchestratorAsync(id, edit.Orchestrator, ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await RequireStoppedAsync(id, ct);
        await _projects.RemoveAsync(id, ct); // Never deletes the project source.
    }

    public async Task<ProjectInfo> LifecycleAsync(
        string id, string action, CancellationToken ct)
    {
        if (action is not ("up" or "down" or "restart"))
        {
            throw new ArgumentException("Unsupported workspace operation.");
        }

        var gate = _locks.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, ct))
        {
            throw new InvalidOperationException("An operation is already running for this project.");
        }

        try
        {
            var project = await _projects.GetRequiredAsync(id, ct);
            var plan = await _plans.BuildAsync(project, cancellationToken: ct);
            switch (action)
            {
                case "up": await _orchestrator.UpAsync(plan, ct); break;
                case "down": await _orchestrator.DownAsync(plan, ct); break;
                default: await _orchestrator.RestartAsync(plan, ct); break;
            }

            return await DescribeAsync(project, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ProjectInfo> AddPortAsync(string id, PortRequest port, CancellationToken ct)
    {
        await RequireStoppedAsync(id, ct);
        _ = await _projects.AddPortAsync(id, port.Container, port.Host, ct);
        return await GetAsync(id, ct);
    }

    public async Task<ProjectInfo> RemovePortAsync(string id, int port, CancellationToken ct)
    {
        await RequireStoppedAsync(id, ct);
        _ = await _projects.RemovePortAsync(id, port, ct);
        return await GetAsync(id, ct);
    }

    public async Task<DiagnosticsInfo> DiagnosticsAsync(CancellationToken ct)
    {
        var statuses = new List<BackendInfo>();
        foreach (var item in _registry.All)
        {
            var state = await item.DetectAsync(ct);
            statuses.Add(new BackendInfo(
                item.Id, state.IsAvailable, state.Version,
                state.Reason is null ? null : _redactor.Redact(state.Reason)));
        }

        return new DiagnosticsInfo(_expectedAspire, statuses);
    }

    public async Task<IReadOnlyList<AgentInfo>> AgentsAsync(string id, CancellationToken ct)
    {
        var project = await _projects.GetRequiredAsync(id, ct);
        var plan = await _plans.BuildAsync(project, cancellationToken: ct);
        var status = await _orchestrator.GetStatusAsync(plan, ct);
        if (status.State != WorkspaceState.Running)
        {
            return _agents.All.Select(a => new AgentInfo(a.Id, a.DisplayName, false,
                "Démarrer le workspace pour vérifier l'installation.")).ToArray();
        }

        var found = new List<AgentInfo>();
        foreach (var agent in _agents.All)
        {
            var info = await agent.InspectAsync(plan, ct);
            found.Add(new AgentInfo(
                agent.Id, agent.DisplayName, info.Installed,
                _redactor.Redact(info.Version ?? info.Details ?? "")));
        }

        return found;
    }

    public async Task<LogsInfo> LogsAsync(string id, int tail, CancellationToken ct)
    {
        _ = await _projects.GetRequiredAsync(id, ct); // Don't inspect arbitrary container names.
        if (tail is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(tail), "Tail must be between 1 and 200.");
        }

        var result = await _runner.RunAsync(new ProcessRequest(
            "docker", ["logs", "--tail", tail.ToString(System.Globalization.CultureInfo.InvariantCulture),
                       $"hstack-{id}-workspace"]), ct);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Docker logs unavailable; check workspace status.");
        }

        var text = _redactor.Redact(result.StandardOutput + result.StandardError);
        return new LogsInfo(text.Length > 65536 ? text[^65536..] : text, tail);
    }

    public async Task<MemoryInfo> MemoryAsync(string id, CancellationToken ct)
    {
        _ = await _projects.GetRequiredAsync(id, ct);
        var config = await _settings.GetAsync(id, ct);
        return new MemoryInfo(config.Enabled, config.ProviderId);
    }

    public async Task<string> BackupAsync(string id, CancellationToken ct)
    {
        _ = await _projects.GetRequiredAsync(id, ct);
        var info = await _backups.BackupAsync(id, cancellationToken: ct);
        return info.Path; // Path only, never archive bytes or credentials.
    }

    private async Task RequireStoppedAsync(string id, CancellationToken ct)
    {
        var state = await GetAsync(id, ct);
        if (state.State != nameof(WorkspaceState.Stopped))
        {
            throw new InvalidOperationException(
                "The workspace must be stopped and healthy before modifying its configuration.");
        }
    }
}
