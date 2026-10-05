using HermesStack.Application.Abstractions;
using HermesStack.Application.Agents;
using HermesStack.Application.Integrations;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Application.Security;
using HermesStack.Application.Sessions;
using HermesStack.Domain.Integrations;
using HermesStack.Domain.Orchestration;
using HermesStack.Docker.Compose;
using HermesStack.Infrastructure.Certificates;
using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Processes;
using HermesStack.Infrastructure.Projects;
using System.Security.Cryptography;
using Spectre.Console;

return await HStackCli.RunAsync(args);

internal static class HStackCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var dataRoot = new DefaultDataRootProvider(Environment.GetEnvironmentVariable("HSTACK_HOME"));
            var initializer = new HStackInitializer(dataRoot);
            var mountValidator = new HostMountValidator(new HostMountPolicy());
            var projectStore = new YamlProjectStore(dataRoot);
            var projectService = new ProjectService(projectStore, mountValidator);
            var processRunner = new ProcessRunner();
            var certificateService = new CertificateBundleService(dataRoot);
            var baseCompose = Path.Combine(AppContext.BaseDirectory, "assets", "docker", "compose", "compose.yaml");
            var toolchainPath = Path.Combine(AppContext.BaseDirectory, "assets", "toolchain.lock.yaml");
            var toolchain = new ToolchainLockService().Load(toolchainPath);
            var planBuilder = new WorkspaceDeploymentPlanBuilder(
                dataRoot,
                mountValidator,
                certificateService,
                baseCompose,
                $"hstack/workspace-full:{toolchain.WorkspaceVersion}");
            var orchestrator = new DockerComposeWorkspaceOrchestrator(processRunner, new ComposeOverrideWriter());
            var integrations = CreateIntegrationRegistry();
            var agents = CreateAgentHarnessRegistry(orchestrator);
            var agentCli = new HermesStack.Cli.AgentCliService(projectService, planBuilder, orchestrator, agents);
            var herdrSessions = new HerdrSessionService(orchestrator, agents);
            var sessionCli = new HermesStack.Cli.SessionCliService(
                projectService,
                planBuilder,
                orchestrator,
                herdrSessions);

            if (args.Length == 0)
            {
                await ShowDashboardAsync(projectService, planBuilder, orchestrator, integrations, agents);
                return 0;
            }

            return args[0] switch
            {
                "init" => await InitAsync(args[1..], initializer, processRunner, toolchain, toolchainPath),
                "project" => await ProjectAsync(args[1..], projectService),
                "up" => await WorkspaceActionAsync(args[1..], projectService, planBuilder, orchestrator, static (o, p, ct) => o.UpAsync(p, ct)),
                "down" => await WorkspaceActionAsync(args[1..], projectService, planBuilder, orchestrator, static (o, p, ct) => o.DownAsync(p, ct)),
                "shell" => await ShellAsync(args[1..], projectService, planBuilder, orchestrator),
                "status" => await StatusAsync(args[1..], projectService, planBuilder, orchestrator, agents),
                "agent" => await agentCli.AgentAsync(args[1..]),
                "auth" => await agentCli.AuthAsync(args[1..]),
                "session" => await sessionCli.SessionAsync(args[1..]),
                "herdr" => await sessionCli.HerdrAsync(args[1..]),
                "tmux" => await sessionCli.TmuxAsync(args[1..]),
                "claude" => await agentCli.AliasAsync("claude", args[1..]),
                "codex" => await agentCli.AliasAsync("codex", args[1..]),
                "hermes" => await agentCli.AliasAsync("hermes", args[1..]),
                "opencode" => await agentCli.AliasAsync("opencode", args[1..]),
                "cert" => await CertAsync(args[1..], dataRoot, certificateService),
                "integrations" => Integrations(args[1..], integrations),
                "version" or "--version" or "-v" => ShowVersion(toolchain.WorkspaceVersion),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => UnknownCommand(args[0])
            };
        }
        catch (Exception exception)
        {
            AnsiConsole.MarkupLine($"[red]ERROR[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
    }

    private static async Task<int> InitAsync(
        string[] args,
        HStackInitializer initializer,
        ProcessRunner processRunner,
        ToolchainVersions toolchain,
        string toolchainPath)
    {
        var orchestrator = GetOption(args, "--orchestrator") ?? "compose";
        if (!string.Equals(orchestrator, "compose", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "The current milestone implements Docker Compose only; Aspire remains an explicit later backend.");
        }

        await initializer.InitializeAsync(orchestrator);
        AnsiConsole.MarkupLine("[green]✓[/] HermesStack data root initialized");

        ProcessResult docker;
        ProcessResult compose;
        try
        {
            docker = await processRunner.RunAsync(new("docker", ["version", "--format", "{{.Client.Version}}"]));
            compose = await processRunner.RunAsync(new("docker", ["compose", "version", "--short"]));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            AnsiConsole.MarkupLine(
                "[yellow]![/] Docker executable was not found. Configuration is initialized; image build was skipped.");
            return 0;
        }

        if (!docker.IsSuccess || !compose.IsSuccess)
        {
            AnsiConsole.MarkupLine(
                "[yellow]![/] Docker/Compose is not available. Configuration is initialized; image build was skipped.");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Docker {Markup.Escape(docker.StandardOutput.Trim())}");
        AnsiConsole.MarkupLine($"[green]✓[/] Compose {Markup.Escape(compose.StandardOutput.Trim())}");

        var workspaceDir = Path.Combine(AppContext.BaseDirectory, "assets", "docker", "workspace");
        var toolchainHash = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(toolchainPath))).ToLowerInvariant();
        var revision = Environment.GetEnvironmentVariable("HSTACK_REVISION")
            ?? Environment.GetEnvironmentVariable("GITHUB_SHA")
            ?? "local";

        var commonBuildArgs = new List<string>
        {
            "--build-arg", $"HSTACK_VERSION={toolchain.WorkspaceVersion}",
            "--build-arg", $"HSTACK_CREATED={DateTimeOffset.UtcNow:O}",
            "--build-arg", $"HSTACK_REVISION={revision}",
            "--build-arg", $"HSTACK_TOOLCHAIN_SHA256={toolchainHash}"
        };

        if (OperatingSystem.IsLinux())
        {
            var uid = await processRunner.RunAsync(new("id", ["-u"]));
            var gid = await processRunner.RunAsync(new("id", ["-g"]));
            if (!uid.IsSuccess ||
                !gid.IsSuccess ||
                !int.TryParse(uid.StandardOutput.Trim(), out var uidValue) ||
                !int.TryParse(gid.StandardOutput.Trim(), out var gidValue) ||
                uidValue <= 0 ||
                gidValue <= 0)
            {
                throw new InvalidOperationException(
                    "Unable to determine the non-root Linux UID/GID for the workspace image.");
            }

            commonBuildArgs.AddRange(
            [
                "--build-arg", $"HSTACK_UID={uidValue}",
                "--build-arg", $"HSTACK_GID={gidValue}"
            ]);
        }

        var baseTag = $"hstack/workspace-base:{toolchain.WorkspaceVersion}";
        var fullTag = $"hstack/workspace-full:{toolchain.WorkspaceVersion}";

        await BuildImageAsync(
            processRunner,
            workspaceDir,
            "Dockerfile.base",
            baseTag,
            commonBuildArgs);

        await BuildImageAsync(
            processRunner,
            workspaceDir,
            "Dockerfile.full",
            fullTag,
            [
                .. commonBuildArgs,
                "--build-arg", $"HSTACK_WORKSPACE_VERSION={toolchain.WorkspaceVersion}",
                "--build-arg", $"HERDR_VERSION={toolchain.HerdrVersion}",
                "--build-arg", $"HERDR_SHA256_X64={toolchain.HerdrSha256X64}",
                "--build-arg", $"HERDR_SHA256_ARM64={toolchain.HerdrSha256Arm64}",
                "--build-arg", $"CLAUDE_CODE_VERSION={toolchain.ClaudeCodeVersion}",
                "--build-arg", $"CODEX_VERSION={toolchain.CodexVersion}",
                "--build-arg", $"HERMES_VERSION={toolchain.HermesVersion}",
                "--build-arg", $"HERMES_RELEASE_TAG={toolchain.HermesReleaseTag}",
                "--build-arg", $"HERMES_COMMIT={toolchain.HermesCommit}",
                "--build-arg", $"OPENCODE_VERSION={toolchain.OpenCodeVersion}"
            ]);

        AnsiConsole.MarkupLine(
            $"[green]✓[/] Workspace images built with Herdr {Markup.Escape(toolchain.HerdrVersion)}, " +
            $"Claude {Markup.Escape(toolchain.ClaudeCodeVersion)}, Codex {Markup.Escape(toolchain.CodexVersion)}, " +
            $"Hermes {Markup.Escape(toolchain.HermesVersion)}, OpenCode {Markup.Escape(toolchain.OpenCodeVersion)}");
        return 0;
    }

    private static async Task BuildImageAsync(
        ProcessRunner runner,
        string context,
        string dockerfile,
        string tag,
        IReadOnlyList<string>? extra = null)
    {
        var args = new List<string>
        {
            "build",
            "-f", Path.Combine(context, dockerfile),
            "-t", tag
        };
        if (extra is not null)
        {
            args.AddRange(extra);
        }

        args.Add(context);
        await runner.RunAsync(
            new("docker", args, CaptureOutput: false, ThrowOnError: true));
    }

    private static async Task<int> ProjectAsync(string[] args, ProjectService projects)
    {
        if (args.Length == 0 || args[0] == "list")
        {
            var list = await projects.ListAsync();
            var table = new Table()
                .AddColumn("Id")
                .AddColumn("Name")
                .AddColumn("Host path")
                .AddColumn("State");
            foreach (var project in list)
            {
                table.AddRow(project.Id, project.Name, project.HostPath, project.StateScope);
            }

            AnsiConsole.Write(table);
            return 0;
        }

        if (args[0] == "add")
        {
            if (args.Length < 3)
            {
                throw new ArgumentException("Usage: hstack project add <id> <hostPath>");
            }

            var validation = projects.ValidateHostPath(args[2]);
            if (validation.Classification == HermesStack.Domain.Security.MountClassification.Suspicious)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]! {validation.Code}[/] {Markup.Escape(validation.Message)}");
            }

            var project = await projects.AddAsync(args[1], args[2]);
            AnsiConsole.MarkupLine(
                $"[green]✓[/] Project [bold]{Markup.Escape(project.Id)}[/] added: {Markup.Escape(project.HostPath)}");
            return 0;
        }

        throw new ArgumentException($"Unknown project command '{args[0]}'.");
    }

    private static async Task<int> WorkspaceActionAsync(
        string[] args,
        ProjectService projects,
        WorkspaceDeploymentPlanBuilder plans,
        DockerComposeWorkspaceOrchestrator orchestrator,
        Func<DockerComposeWorkspaceOrchestrator, WorkspaceDeploymentPlan, CancellationToken, Task> action)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Project id is required.");
        }

        var project = await projects.GetRequiredAsync(args[0]);
        var plan = await plans.BuildAsync(project, GetOption(args, "--orchestrator"));
        await action(orchestrator, plan, CancellationToken.None);
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(project.Id)}");
        return 0;
    }

    private static async Task<int> ShellAsync(
        string[] args,
        ProjectService projects,
        WorkspaceDeploymentPlanBuilder plans,
        DockerComposeWorkspaceOrchestrator orchestrator)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Usage: hstack shell <project>");
        }

        var project = await projects.GetRequiredAsync(args[0]);
        var plan = await plans.BuildAsync(project, GetOption(args, "--orchestrator"));
        var status = await orchestrator.GetStatusAsync(plan);
        if (status.State != WorkspaceState.Running)
        {
            await orchestrator.UpAsync(plan);
        }

        return await orchestrator.ExecAsync(
            new WorkspaceExecutionRequest(plan, ["/bin/bash"]));
    }

    private static async Task<int> StatusAsync(
        string[] args,
        ProjectService projects,
        WorkspaceDeploymentPlanBuilder plans,
        DockerComposeWorkspaceOrchestrator orchestrator,
        IAgentHarnessRegistry agents)
    {
        if (args.Length == 0)
        {
            await ShowDashboardAsync(
                projects,
                plans,
                orchestrator,
                CreateIntegrationRegistry(),
                agents);
            return 0;
        }

        var project = await projects.GetRequiredAsync(args[0]);
        var plan = await plans.BuildAsync(project, GetOption(args, "--orchestrator"));
        var status = await orchestrator.GetStatusAsync(plan);
        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Project", project.Name);
        table.AddRow("Workspace", status.State.ToString());
        table.AddRow("Image", plan.WorkspaceImage);
        table.AddRow("User", "hstack");
        table.AddRow("Mount", $"{project.HostPath} -> /workspace");
        table.AddRow("Agent state", $"{project.StateScope} ({agents.All.Count} harnesses)");
        table.AddRow("Security", "Policy A");
        table.AddRow("Orchestrator", orchestrator.DisplayName);
        AnsiConsole.Write(table);
        return status.State == WorkspaceState.Unknown ? 2 : 0;
    }

    private static async Task<int> CertAsync(
        string[] args,
        DefaultDataRootProvider dataRoot,
        CertificateBundleService certificateService)
    {
        if (args.Length < 2 || args[0] != "add")
        {
            throw new ArgumentException("Usage: hstack cert add <certificate.pem>");
        }

        var source = Path.GetFullPath(args[1]);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Certificate file not found.", source);
        }

        var content = await File.ReadAllTextAsync(source);
        if (!content.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal))
        {
            throw new InvalidDataException("HermesStack accepts PEM encoded CA certificates only.");
        }

        Directory.CreateDirectory(dataRoot.CorporateCertificatesDirectory);
        var target = Path.Combine(
            dataRoot.CorporateCertificatesDirectory,
            Path.GetFileName(source));
        if (!string.Equals(
            source,
            Path.GetFullPath(target),
            StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(source, target, true);
        }

        _ = await certificateService.BuildCorporateBundleAsync();
        AnsiConsole.MarkupLine(
            $"[green]✓[/] Corporate CA added: {Markup.Escape(Path.GetFileName(target))}");
        return 0;
    }

    private static int Integrations(string[] args, IntegrationRegistry registry)
    {
        if (args.Length > 0 && args[0] is not ("list" or "status"))
        {
            throw new ArgumentException("Usage: hstack integrations [list|status]");
        }

        var table = new Table()
            .AddColumn("Integration")
            .AddColumn("Type")
            .AddColumn("Strategy")
            .AddColumn("MVP");
        foreach (var item in registry.All
            .OrderBy(static i => i.Kind)
            .ThenBy(static i => i.Id))
        {
            table.AddRow(
                item.DisplayName,
                item.Kind.ToString(),
                item.Strategy,
                item.RequiredForMvp ? "yes" : "no");
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static async Task ShowDashboardAsync(
        ProjectService projects,
        WorkspaceDeploymentPlanBuilder plans,
        DockerComposeWorkspaceOrchestrator orchestrator,
        IntegrationRegistry registry,
        IAgentHarnessRegistry agents)
    {
        AnsiConsole.Write(
            new Rule("[bold]HermesStack[/] — Secure Local Agent Workspaces"));
        var dockerAvailability = await orchestrator.DetectAsync();
        var dockerReady = dockerAvailability.IsAvailable;
        AnsiConsole.MarkupLine(
            $"Docker Compose  {(dockerReady ? "[green]Ready[/]" : "[yellow]Unavailable[/]")}");
        AnsiConsole.MarkupLine(
            $"Agents          {agents.All.Count} registered harnesses");
        AnsiConsole.MarkupLine(
            $"Integrations    {registry.All.Count} registered descriptors");

        var projectList = await projects.ListAsync();
        if (projectList.Count == 0)
        {
            AnsiConsole.MarkupLine("Projects        [grey]none[/]");
            return;
        }

        var table = new Table()
            .AddColumn("Project")
            .AddColumn("State")
            .AddColumn("Path");
        foreach (var project in projectList)
        {
            var plan = await plans.BuildAsync(project);
            var status = dockerReady
                ? await orchestrator.GetStatusAsync(plan)
                : new WorkspaceStatus(WorkspaceState.Unknown);
            table.AddRow(project.Name, status.State.ToString(), project.HostPath);
        }

        AnsiConsole.Write(table);
    }

    private static AgentHarnessRegistry CreateAgentHarnessRegistry(
        IWorkspaceOrchestrator orchestrator) => new(
    [
        new ClaudeCodeHarness(orchestrator),
        new CodexHarness(orchestrator),
        new HermesAgentHarness(orchestrator),
        new OpenCodeHarness(orchestrator)
    ]);

    private static IntegrationRegistry CreateIntegrationRegistry() => new(
    [
        new(
            "compose",
            "Docker Compose",
            IntegrationKind.Orchestrator,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.BindMounts,
                IntegrationCapability.NamedVolumes,
                IntegrationCapability.NoNewPrivileges,
                IntegrationCapability.DropCapabilities,
                IntegrationCapability.LocalhostPortBinding,
                IntegrationCapability.InteractiveTty,
                IntegrationCapability.PersistentHome
            },
            true),
        new(
            "native-container",
            "Native Docker Container",
            IntegrationKind.ExecutionProvider,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.BindMounts,
                IntegrationCapability.NoNewPrivileges,
                IntegrationCapability.DropCapabilities,
                IntegrationCapability.LocalhostPortBinding
            },
            true),
        new(
            "claude",
            "Claude Code",
            IntegrationKind.Agent,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.InteractiveTty,
                IntegrationCapability.Authentication,
                IntegrationCapability.ProjectScopedState
            },
            true),
        new(
            "codex",
            "Codex",
            IntegrationKind.Agent,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.InteractiveTty,
                IntegrationCapability.Authentication,
                IntegrationCapability.ProjectScopedState
            },
            true),
        new(
            "hermes",
            "Hermes Agent",
            IntegrationKind.Agent,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.InteractiveTty,
                IntegrationCapability.Authentication,
                IntegrationCapability.ProjectScopedState
            },
            true),
        new(
            "opencode",
            "OpenCode",
            IntegrationKind.Agent,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.InteractiveTty,
                IntegrationCapability.Authentication,
                IntegrationCapability.ProjectScopedState
            },
            true),
        new(
            "herdr",
            "Herdr",
            IntegrationKind.SessionManager,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.InteractiveTty,
                IntegrationCapability.PersistentHome,
                IntegrationCapability.ProjectScopedState,
                IntegrationCapability.AgentHooks
            },
            true),
        new(
            "rtk",
            "RTK",
            IntegrationKind.TokenOptimizer,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.AgentHooks,
                IntegrationCapability.TokenOptimization,
                IntegrationCapability.Metrics
            },
            false),
        new(
            "openviking",
            "OpenViking",
            IntegrationKind.ContextProvider,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.SharedMemory,
                IntegrationCapability.Mcp
            },
            false),
        new(
            "aspire",
            "Aspire",
            IntegrationKind.Orchestrator,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.StructuredLogs,
                IntegrationCapability.Traces,
                IntegrationCapability.Metrics
            },
            false)
    ]);

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }

    private static int ShowVersion(string workspaceVersion)
    {
        AnsiConsole.MarkupLine(
            $"HermesStack {Markup.Escape(workspaceVersion)}-dev");
        return 0;
    }

    private static int ShowHelp()
    {
        AnsiConsole.MarkupLine("""
[bold]hstack[/]
  hstack init [--orchestrator compose]
  hstack project add <id> <hostPath>
  hstack project list
  hstack up <project>
  hstack down <project>
  hstack shell <project>
  hstack status [project]

  hstack agent list [--project <project>]
  hstack agent run <agent> --project <project> [-- <args>]
  hstack auth <agent> --project <project>

  hstack session init|status|list|agents|stop <project>
  hstack session run <agent> <project> --name <name> [-- <args>]
  hstack herdr <project>
  hstack tmux <project> [session-name]

  hstack claude <project> [-- <args>]
  hstack codex <project> [-- <args>]
  hstack hermes <project> [-- <args>]
  hstack opencode <project> [-- <args>]

  hstack cert add <certificate.pem>
  hstack integrations list
  hstack version
""");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        AnsiConsole.MarkupLine(
            $"[red]Unknown command:[/] {Markup.Escape(command)}");
        return 2;
    }
}
