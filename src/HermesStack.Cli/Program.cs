using HermesStack.Application.Abstractions;
using HermesStack.Application.Agents;
using HermesStack.Application.Context;
using HermesStack.Application.Integrations;
using HermesStack.Application.Network;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Application.Security;
using HermesStack.Application.Sessions;
using HermesStack.Application.Tokens;
using HermesStack.Application.Updates;
using HermesStack.Aspire;
using HermesStack.Docker.Compose;
using HermesStack.Docker.Context;
using HermesStack.Docker.Execution;
using HermesStack.Docker.Security;
using HermesStack.Docker.Tokens;
using HermesStack.Domain.Integrations;
using HermesStack.Domain.Orchestration;
using HermesStack.Infrastructure.Certificates;
using HermesStack.Infrastructure.Context;
using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Processes;
using HermesStack.Infrastructure.Projects;
using HermesStack.Infrastructure.Security;
using HermesStack.Infrastructure.Operations;
using HermesStack.Infrastructure.Tokens;
using HermesStack.Infrastructure.Updates;
using Spectre.Console;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

return await HStackCli.RunAsync(args);

internal static class HStackCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var redactor = new SecretRedactor();
        ApplicationLogService? appLog = null;
        try
        {
            var dataRoot = new DefaultDataRootProvider(Environment.GetEnvironmentVariable("HSTACK_HOME"));
            appLog = new ApplicationLogService(dataRoot, redactor);
            await appLog.WriteAsync(
                "info",
                "command",
                args.Length == 0 ? "dashboard" : args[0]);
            var initializer = new HStackInitializer(dataRoot);
            var mountValidator = new HostMountValidator(new HostMountPolicy());
            var projectStore = new YamlProjectStore(dataRoot);
            var projectService = new ProjectService(projectStore, mountValidator);
            var processRunner = new ProcessRunner();
            var certificateService = new CertificateBundleService(dataRoot);
            var configStore = new HStackConfigStore(dataRoot);
            var secretStore = new LocalProtectedSecretStore(dataRoot);
            var secretPolicies = new YamlSecretPolicyStore(dataRoot);
            var secretInjection = new SecretInjectionService(secretStore, secretPolicies);
            var tokenStore = new YamlTokenOptimizationStore(dataRoot);
            var tokenMetricStore = new JsonlTokenMetricStore(dataRoot);
            var baseCompose = Path.Combine(AppContext.BaseDirectory, "assets", "docker", "compose", "compose.yaml");
            var embeddedToolchainPath = Path.Combine(AppContext.BaseDirectory, "assets", "toolchain.lock.yaml");
            var toolchainOverridePath = Path.Combine(dataRoot.ConfigDirectory, "toolchain.lock.yaml");
            var lockService = new ToolchainLockService();
            var toolchain = lockService.LoadEffective(embeddedToolchainPath, toolchainOverridePath);
            var effectiveToolchainPath = File.Exists(toolchainOverridePath)
                ? toolchainOverridePath
                : embeddedToolchainPath;
            var openVikingManager = new OpenVikingServiceManager(
                dataRoot,
                secretStore,
                processRunner,
                toolchain.OpenVikingImage,
                toolchain.OpenVikingVersion);
            var planBuilder = new WorkspaceDeploymentPlanBuilder(
                dataRoot,
                mountValidator,
                certificateService,
                baseCompose,
                $"hstack/workspace-full:{toolchain.WorkspaceVersion}",
                configStore,
                configStore,
                configStore);
            var composeOrchestrator = new DockerComposeWorkspaceOrchestrator(
                processRunner,
                new ComposeOverrideWriter(),
                openVikingManager);
            var appHostSource = Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "aspire",
                "apphost",
                "AppHost.cs");
            var aspireVersion = toolchain.AspireVersion;
            var aspireCapabilities =
                new AspireOrchestratorCapabilityEvaluator();
            var aspireOrchestrator = new AspireWorkspaceOrchestrator(
                processRunner,
                new AspireDeploymentPlanWriter(
                    dataRoot,
                    appHostSource,
                    aspireVersion),
                aspireCapabilities,
                aspireVersion,
                openVikingManager);
            var orchestratorRegistry = new WorkspaceOrchestratorRegistry(
            [
                composeOrchestrator,
                aspireOrchestrator
            ]);
            // Native Docker remains the only shipped execution provider.
            // Compose/Aspire continue to own their concrete lifecycle.
            var nativeExecutionProvider = new NativeContainerExecutionProvider(
                orchestratorRegistry,
                processRunner);
            var orchestrator = new RoutedWorkspaceOrchestrator(
                orchestratorRegistry,
                nativeExecutionProvider);
            var integrations = CreateIntegrationRegistry(toolchain);
            var agents = CreateAgentHarnessRegistry(orchestrator);
            var contextRegistry = new ContextProviderRegistry(
            [
                new OpenVikingContextProvider(
                    openVikingManager,
                    new OpenVikingContextScopeMapper())
            ]);
            var contextService = new ContextService(
                contextRegistry,
                configStore,
                new ContextSecretFilter(redactor),
                new JsonContextTraceStore(dataRoot),
                new ContextBudgetPolicy());
            var memoryCli = new HermesStack.Cli.MemoryCliService(
                projectService,
                planBuilder,
                orchestrator,
                contextService,
                contextRegistry,
                openVikingManager);
            var contextCli = new HermesStack.Cli.ContextCliService(
                projectService,
                contextService);
            var tokenRegistry = new TokenOptimizerRegistry(
            [
                new RtkTokenOptimizer(orchestrator, toolchain.RtkVersion),
                new CavemanTokenOptimizer(
                    orchestrator,
                    toolchain.CavemanVersion,
                    toolchain.CavemanReleaseTag)
            ]);
            var tokenService = new TokenOptimizationService(
                orchestrator,
                tokenRegistry,
                tokenStore,
                tokenMetricStore,
                new TokenOptimizationCompatibilityPolicy());
            var tokenCli = new HermesStack.Cli.TokenCliService(
                projectService,
                planBuilder,
                tokenService,
                tokenRegistry);
            var agentCli = new HermesStack.Cli.AgentCliService(
                projectService,
                planBuilder,
                orchestrator,
                agents,
                secretInjection);
            var herdrSessions = new HerdrSessionService(orchestrator, agents);
            var sessionCli = new HermesStack.Cli.SessionCliService(
                projectService,
                planBuilder,
                orchestrator,
                herdrSessions);
            var securityEvaluator = new SecurityInspectionService();
            var dockerSecurityInspector = new DockerWorkspaceSecurityInspector(processRunner);
            var proxyCli = new HermesStack.Cli.ProxyCliService(configStore);
            var secretCli = new HermesStack.Cli.SecretCliService(
                projectService,
                agents,
                secretStore,
                secretPolicies);
            var securityCli = new HermesStack.Cli.SecurityCliService(
                projectService,
                planBuilder,
                dockerSecurityInspector,
                securityEvaluator,
                secretPolicies,
                tokenStore,
                configStore,
                configStore);
            var doctorCli = new HermesStack.Cli.DoctorCliService(
                projectService,
                planBuilder,
                orchestrator,
                agents,
                configStore,
                certificateService,
                dockerSecurityInspector,
                securityEvaluator,
                secretPolicies,
                tokenService,
                contextService,
                redactor,
                orchestratorRegistry);
            var archives = new BackupArchiveService(
                dataRoot,
                embeddedToolchainPath);
            var portableSecrets = new PortableSecretPackageService(
                projectStore,
                secretPolicies,
                secretStore);
            var operationsCli = new HermesStack.Cli.OperationsCliService(
                projectService,
                planBuilder,
                orchestrator,
                archives,
                portableSecrets);
            var configCli = new HermesStack.Cli.ConfigCliService(
                configStore,
                projectService);
            var orchestratorCli = new HermesStack.Cli.OrchestratorCliService(
                projectService,
                planBuilder,
                orchestratorRegistry,
                orchestrator,
                configStore);
            var aspireCli = new HermesStack.Cli.AspireCliService(
                projectService,
                planBuilder,
                aspireOrchestrator);

            if (args.Length == 0)
            {
                await ShowDashboardAsync(
                    projectService,
                    planBuilder,
                    orchestratorRegistry,
                    orchestrator,
                    integrations,
                    agents);
                if (Console.IsInputRedirected || Console.IsOutputRedirected)
                {
                    return 0;
                }

                while (true)
                {
                    var action = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[bold]Action[/]")
                            .AddChoices(
                                "Open project",
                                "Projects",
                                "Agents",
                                "Herdr",
                                "Updates",
                                "Doctor",
                                "Security",
                                "Certificates",
                                "Settings",
                                "Exit"));

                    if (action == "Exit")
                    {
                        return 0;
                    }

                    if (action == "Projects")
                    {
                        _ = await ProjectAsync(["list"], projectService);
                        continue;
                    }

                    if (action == "Agents")
                    {
                        _ = await agentCli.AgentAsync(["list"]);
                        continue;
                    }

                    if (action == "Updates")
                    {
                        _ = await UpdateAsync(
                            ["check"],
                            toolchain,
                            configStore,
                            dataRoot,
                            archives,
                            processRunner,
                            certificateService,
                            projectService,
                            planBuilder,
                            composeOrchestrator,
                            mountValidator,
                            baseCompose,
                            secretStore);
                        continue;
                    }

                    if (action == "Doctor")
                    {
                        _ = await doctorCli.RunAsync([]);
                        continue;
                    }

                    if (action == "Certificates")
                    {
                        AnsiConsole.MarkupLine(
                            $"Corporate CA directory: {Markup.Escape(dataRoot.CorporateCertificatesDirectory)}");
                        AnsiConsole.MarkupLine(
                            "Add a CA with [bold]hstack cert add <certificate.pem>[/].");
                        continue;
                    }

                    if (action == "Settings")
                    {
                        _ = await proxyCli.RunAsync(["show"]);
                        continue;
                    }

                    var selectedProject = await SelectProjectAsync(
                        projectService,
                        action);
                    if (selectedProject is null)
                    {
                        continue;
                    }

                    if (action == "Open project")
                    {
                        _ = await ShellAsync(
                            [selectedProject],
                            projectService,
                            planBuilder,
                            orchestrator);
                    }
                    else if (action == "Herdr")
                    {
                        _ = await sessionCli.HerdrAsync([selectedProject]);
                    }
                    else if (action == "Security")
                    {
                        _ = await securityCli.RunAsync(
                            ["inspect", selectedProject]);
                    }
                }
            }

            return args[0] switch
            {
                "init" => await InitAsync(
                    args[1..],
                    initializer,
                    processRunner,
                    toolchain,
                    effectiveToolchainPath,
                    certificateService,
                    configStore,
                    orchestratorRegistry),
                "project" => await ProjectAsync(args[1..], projectService),
                "up" => await WorkspaceActionAsync(args[1..], projectService, planBuilder, orchestrator, static (o, p, ct) => o.UpAsync(p, ct)),
                "down" => await WorkspaceActionAsync(args[1..], projectService, planBuilder, orchestrator, static (o, p, ct) => o.DownAsync(p, ct)),
                "restart" => await WorkspaceActionAsync(args[1..], projectService, planBuilder, orchestrator, static (o, p, ct) => o.RestartAsync(p, ct)),
                "shell" => await ShellAsync(args[1..], projectService, planBuilder, orchestrator),
                "status" => await StatusAsync(args[1..], projectService, planBuilder, orchestrator, agents, integrations, orchestratorRegistry),
                "ps" => await StatusAsync(args[1..], projectService, planBuilder, orchestrator, agents, integrations, orchestratorRegistry),
                "logs" => await operationsCli.LogsAsync(args[1..]),
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
                "proxy" => await proxyCli.RunAsync(args[1..]),
                "port" => await PortAsync(args[1..], projectService),
                "secret" => await secretCli.RunAsync(args[1..]),
                "security" => await securityCli.RunAsync(args[1..]),
                "token" => await tokenCli.RunAsync(args[1..]),
                "memory" => await memoryCli.RunAsync(args[1..]),
                "context" => await contextCli.RunAsync(args[1..]),
                "doctor" => args[1..].Contains("--aspire", StringComparer.Ordinal)
                    ? await aspireCli.DoctorAsync()
                    : await doctorCli.RunAsync(args[1..]),
                "config" => await configCli.RunAsync(args[1..]),
                "orchestrator" => await orchestratorCli.RunAsync(args[1..]),
                "plan" => await orchestratorCli.PlanAsync(args[1..]),
                "aspire" => await aspireCli.RunAsync(args[1..]),
                "update" => await UpdateAsync(
                    args[1..],
                    toolchain,
                    configStore,
                    dataRoot,
                    archives,
                    processRunner,
                    certificateService,
                    projectService,
                    planBuilder,
                    composeOrchestrator,
                    mountValidator,
                    baseCompose,
                    secretStore),
                "backup" => await operationsCli.BackupAsync(args[1..]),
                "restore" => await operationsCli.RestoreAsync(args[1..]),
                "export" => await operationsCli.ExportAsync(args[1..]),
                "import" => await operationsCli.ImportAsync(args[1..]),
                "compose" => await ComposeAsync(args[1..], projectService, planBuilder, processRunner),
                "clean" => await CleanAsync(args[1..], processRunner),
                "integrations" => Integrations(args[1..], integrations),
                "version" or "--version" or "-v" => ShowVersion(toolchain.WorkspaceVersion),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => UnknownCommand(args[0])
            };
        }
        catch (Exception exception)
        {
            var safeMessage = redactor.Redact(exception.Message);
            if (appLog is not null)
            {
                await appLog.WriteAsync("error", "command-failed", safeMessage);
            }

            AnsiConsole.MarkupLine(
                $"[red]ERROR[/] {Markup.Escape(safeMessage)}");
            return 1;
        }
    }

    private static async Task<int> InitAsync(
        string[] args,
        HStackInitializer initializer,
        ProcessRunner processRunner,
        ToolchainVersions toolchain,
        string toolchainPath,
        CertificateBundleService certificateService,
        HStackConfigStore configStore,
        IWorkspaceOrchestratorRegistry orchestrators)
    {
        var orchestrator = (GetOption(args, "--orchestrator") ?? "compose")
            .Trim()
            .ToLowerInvariant();
        var selectedOrchestrator = orchestrators.GetRequired(orchestrator);
        var availability = await selectedOrchestrator.DetectAsync();
        if (!availability.IsAvailable)
        {
            throw new InvalidOperationException(
                $"HS2110: Orchestrator '{orchestrator}' is unavailable: {availability.Reason}");
        }

        await initializer.InitializeAsync(orchestrator);
        var orchestration = await configStore.GetOrchestrationAsync();
        await configStore.SaveOrchestrationAsync(
            orchestration with
            {
                DefaultOrchestrator = orchestrator,
                ComposeEnabled = orchestration.ComposeEnabled || orchestrator == "compose",
                AspireEnabled = orchestration.AspireEnabled || orchestrator == "aspire"
            });
        AnsiConsole.MarkupLine("[green]✓[/] HermesStack data root initialized");

        ProcessResult docker;
        ProcessResult? compose = null;
        try
        {
            docker = await processRunner.RunAsync(new("docker", ["version", "--format", "{{.Client.Version}}"]));
            if (orchestrator == "compose")
            {
                compose = await processRunner.RunAsync(new("docker", ["compose", "version", "--short"]));
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            AnsiConsole.MarkupLine(
                "[yellow]![/] Docker executable was not found. Configuration is initialized; image build was skipped.");
            return 0;
        }

        if (!docker.IsSuccess || (compose is not null && !compose.IsSuccess))
        {
            AnsiConsole.MarkupLine(
                "[yellow]![/] Required container runtime/orchestrator tooling is not available. Configuration is initialized; image build was skipped.");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Docker {Markup.Escape(docker.StandardOutput.Trim())}");
        if (compose is not null)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Compose {Markup.Escape(compose.StandardOutput.Trim())}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Aspire {Markup.Escape(availability.Version ?? "ready")}");
        }

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

        var corporateBundle = await certificateService.BuildCorporateBundleAsync();
        if (corporateBundle is not null)
        {
            commonBuildArgs.AddRange(["--secret", $"id=hstack_corporate_ca,src={corporateBundle}"]);
            AnsiConsole.MarkupLine("[green]✓[/] Corporate CA bundle will be trusted during image build");
        }

        var baseTag = $"hstack/workspace-base:{toolchain.WorkspaceVersion}";
        var fullTag = $"hstack/workspace-full:{toolchain.WorkspaceVersion}";

        // M3 CI has already built and exercised the complete pinned agent image.
        // M8 reuses exactly that image instead of replacing its tag with the base
        // image or rebuilding upstream downloads after a transient rate limit.
        if (args.Contains("--ci-reuse-verified-full-image", StringComparer.Ordinal))
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("--ci-reuse-verified-full-image is restricted to CI.");
            }

            var inspection = await processRunner.RunAsync(new(
                "docker", ["image", "inspect", fullTag, "--format", "{{json .Config.Labels}}"],
                ThrowOnError: true));
            using var labels = JsonDocument.Parse(inspection.StandardOutput);
            var root = labels.RootElement;
            bool Matches(string key, string expected) =>
                root.TryGetProperty(key, out var property) &&
                string.Equals(property.GetString(), expected, StringComparison.Ordinal);

            if (!Matches("io.hstack.kind", "workspace-full") ||
                !Matches("io.hstack.toolchain.sha256", toolchainHash) ||
                !Matches("io.hstack.agent.claude", toolchain.ClaudeCodeVersion) ||
                !Matches("io.hstack.agent.codex", toolchain.CodexVersion) ||
                !Matches("io.hstack.agent.hermes", toolchain.HermesVersion) ||
                !Matches("io.hstack.agent.opencode", toolchain.OpenCodeVersion))
            {
                throw new InvalidOperationException(
                    "M8 CI requires the complete, toolchain-verified image already built and tested by the M3 gate.");
            }

            AnsiConsole.MarkupLine("[green]✓[/] Reusing CI-validated full agent image (no base-image substitution).");
            return 0;
        }

        await BuildImageAsync(
            processRunner,
            workspaceDir,
            "Dockerfile.base",
            baseTag,
            commonBuildArgs);

        if (args.Contains("--ci-base-image-only", StringComparer.Ordinal))
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("--ci-base-image-only is restricted to CI.");
            }

            await processRunner.RunAsync(new(
                "docker", ["tag", baseTag, fullTag], ThrowOnError: true));
            AnsiConsole.MarkupLine("[yellow]![/] CI smoke mode: full agent image build skipped; base image substituted.");
            return 0;
        }

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
                "--build-arg", $"OPENCODE_VERSION={toolchain.OpenCodeVersion}",
                "--build-arg", $"RTK_VERSION={toolchain.RtkVersion}",
                "--build-arg", $"RTK_RELEASE_TAG={toolchain.RtkReleaseTag}",
                "--build-arg", $"RTK_SHA256_X64={toolchain.RtkSha256X64}",
                "--build-arg", $"RTK_SHA256_ARM64={toolchain.RtkSha256Arm64}",
                "--build-arg", $"CAVEMAN_VERSION={toolchain.CavemanVersion}",
                "--build-arg", $"CAVEMAN_RELEASE_TAG={toolchain.CavemanReleaseTag}",
                "--build-arg", $"CAVEMAN_COMMIT={toolchain.CavemanCommit}"
            ]);

        AnsiConsole.MarkupLine(
            $"[green]✓[/] Workspace images built with Herdr {Markup.Escape(toolchain.HerdrVersion)}, " +
            $"Claude {Markup.Escape(toolchain.ClaudeCodeVersion)}, Codex {Markup.Escape(toolchain.CodexVersion)}, " +
            $"Hermes {Markup.Escape(toolchain.HermesVersion)}, OpenCode {Markup.Escape(toolchain.OpenCodeVersion)}, " +
            $"RTK {Markup.Escape(toolchain.RtkVersion)}, Caveman {Markup.Escape(toolchain.CavemanVersion)}");
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
        var command = args.Length == 0 ? "list" : args[0];
        var json = args.Contains("--json", StringComparer.Ordinal);

        if (command == "list")
        {
            var list = await projects.ListAsync();
            if (json)
            {
                AnsiConsole.WriteLine(JsonSerializer.Serialize(list));
                return 0;
            }

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

        if (command == "show")
        {
            if (args.Length < 2)
            {
                throw new ArgumentException("Usage: hstack project show <id> [[--json]]");
            }

            var project = await projects.GetRequiredAsync(args[1]);
            if (json)
            {
                AnsiConsole.WriteLine(JsonSerializer.Serialize(project));
            }
            else
            {
                var table = new Table().AddColumn("Property").AddColumn("Value");
                table.AddRow("Id", Markup.Escape(project.Id));
                table.AddRow("Name", Markup.Escape(project.Name));
                table.AddRow("Host path", Markup.Escape(project.HostPath));
                table.AddRow("Container path", Markup.Escape(project.ContainerPath));
                table.AddRow("State", Markup.Escape(project.StateScope));
                AnsiConsole.Write(table);
            }

            return 0;
        }

        if (command == "add")
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

            var project = await projects.AddAsync(
                args[1],
                args[2],
                GetOption(args, "--name"));
            if (!args.Contains("--quiet", StringComparer.Ordinal))
            {
                AnsiConsole.MarkupLine(
                    $"[green]✓[/] Project [bold]{Markup.Escape(project.Id)}[/] added: {Markup.Escape(project.HostPath)}");
            }

            return 0;
        }

        if (command == "edit")
        {
            if (args.Length < 2)
            {
                throw new ArgumentException(
                    "Usage: hstack project edit <id> [[--path <hostPath>]] [[--name <name>]]");
            }

            var updated = await projects.EditAsync(
                args[1],
                GetOption(args, "--path"),
                GetOption(args, "--name"));
            if (!args.Contains("--quiet", StringComparer.Ordinal))
            {
                AnsiConsole.MarkupLine(
                    $"[green]✓[/] Project {Markup.Escape(updated.Id)} updated.");
            }

            return 0;
        }

        if (command == "remove")
        {
            if (args.Length < 2)
            {
                throw new ArgumentException("Usage: hstack project remove <id> --yes");
            }

            if (!args.Contains("--yes", StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Project removal requires --yes. Project source files are never deleted.");
            }

            await projects.RemoveAsync(args[1]);
            if (!args.Contains("--quiet", StringComparer.Ordinal))
            {
                AnsiConsole.MarkupLine(
                    $"[green]✓[/] Project {Markup.Escape(args[1])} unregistered.");
            }

            return 0;
        }

        throw new ArgumentException($"Unknown project command '{command}'.");
    }

    private static async Task<int> PortAsync(
        string[] args,
        ProjectService projects)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: hstack port list|add|remove <project> [[containerPort]] [[--host <port>]]");
        }

        var command = args[0];
        var projectId = args[1];
        if (command == "list")
        {
            var project = await projects.GetRequiredAsync(projectId);
            var table = new Table()
                .AddColumn("Container")
                .AddColumn("Host")
                .AddColumn("Bind");
            foreach (var port in project.EffectivePorts
                .OrderBy(static value => value.Container))
            {
                table.AddRow(
                    port.Container.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    port.EffectiveHost.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Markup.Escape(port.Bind));
            }

            AnsiConsole.Write(table);
            return 0;
        }

        if (args.Length < 3 ||
            !int.TryParse(
                args[2],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var containerPort))
        {
            throw new ArgumentException(
                "A numeric container port is required.");
        }

        if (command == "add")
        {
            int? hostPort = null;
            var hostText = GetOption(args, "--host");
            if (hostText is not null)
            {
                if (!int.TryParse(
                    hostText,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsedHost))
                {
                    throw new ArgumentException("--host must be a numeric port.");
                }

                hostPort = parsedHost;
            }

            var updated = await projects.AddPortAsync(
                projectId,
                containerPort,
                hostPort);
            var added = updated.EffectivePorts.Single(
                value => value.Container == containerPort);
            AnsiConsole.MarkupLine(
                $"[green]✓[/] 127.0.0.1:{added.EffectiveHost}:{added.Container}");
            return 0;
        }

        if (command == "remove")
        {
            _ = await projects.RemovePortAsync(projectId, containerPort);
            AnsiConsole.MarkupLine(
                $"[green]✓[/] Removed container port {containerPort} from {Markup.Escape(projectId)}.");
            return 0;
        }

        throw new ArgumentException(
            "Usage: hstack port list|add|remove <project> [[containerPort]] [[--host <port>]]");
    }

    private static async Task<int> WorkspaceActionAsync(
        string[] args,
        ProjectService projects,
        WorkspaceDeploymentPlanBuilder plans,
        IWorkspaceOrchestrator orchestrator,
        Func<IWorkspaceOrchestrator, WorkspaceDeploymentPlan, CancellationToken, Task> action)
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
        IWorkspaceOrchestrator orchestrator)
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
        IWorkspaceOrchestrator orchestrator,
        IAgentHarnessRegistry agents,
        IntegrationRegistry integrations,
        IWorkspaceOrchestratorRegistry? orchestratorRegistry = null)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        var quiet = args.Contains("--quiet", StringComparer.Ordinal);
        var projectId = args.FirstOrDefault(static value =>
            !value.StartsWith("--", StringComparison.Ordinal));

        if (projectId is null)
        {
            if (json)
            {
                var items = new List<object>();
                foreach (var itemProject in await projects.ListAsync())
                {
                    var itemPlan = await plans.BuildAsync(itemProject);
                    var itemStatus = await orchestrator.GetStatusAsync(itemPlan);
                    items.Add(new
                    {
                        project = itemProject.Id,
                        name = itemProject.Name,
                        state = itemStatus.State.ToString(),
                        image = itemPlan.WorkspaceImage,
                        path = itemProject.HostPath
                    });
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(items));
                return 0;
            }

            if (!quiet)
            {
                await ShowDashboardAsync(
                    projects,
                    plans,
                    orchestratorRegistry ?? new WorkspaceOrchestratorRegistry([orchestrator]),
                    orchestrator,
                    integrations,
                    agents);
            }

            return 0;
        }

        var project = await projects.GetRequiredAsync(projectId);
        var plan = await plans.BuildAsync(project, GetOption(args, "--orchestrator"));
        var status = await orchestrator.GetStatusAsync(plan);

        if (json)
        {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(new
            {
                project = project.Id,
                name = project.Name,
                workspace = status.State.ToString(),
                image = plan.WorkspaceImage,
                user = "hstack",
                mount = new { source = project.HostPath, target = "/workspace" },
                agentState = project.StateScope,
                agentHarnesses = agents.All.Count,
                security = "Policy A",
                orchestrator = plan.OrchestratorId
            }));
        }
        else if (!quiet)
        {
            var table = new Table().AddColumn("Property").AddColumn("Value");
            table.AddRow("Project", project.Name);
            table.AddRow("Workspace", status.State.ToString());
            table.AddRow("Image", plan.WorkspaceImage);
            table.AddRow("User", "hstack");
            table.AddRow("Mount", $"{project.HostPath} -> /workspace");
            table.AddRow("Agent state", $"{project.StateScope} ({agents.All.Count} harnesses)");
            table.AddRow("Security", "Policy A");
            table.AddRow("Orchestrator", plan.OrchestratorId);
            if (!string.IsNullOrWhiteSpace(status.Details))
            {
                table.AddRow("Details", Markup.Escape(status.Details));
            }
            AnsiConsole.Write(table);
        }

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
            .AddColumn("Version")
            .AddColumn("Status")
            .AddColumn("MVP");
        foreach (var item in registry.All
            .OrderBy(static i => i.Kind)
            .ThenBy(static i => i.Id))
        {
            table.AddRow(
                item.DisplayName,
                item.Kind.ToString(),
                item.Strategy,
                Markup.Escape(item.Version ?? "-"),
                Markup.Escape(item.Status),
                item.RequiredForMvp ? "yes" : "no");
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static async Task ShowDashboardAsync(
        ProjectService projects,
        WorkspaceDeploymentPlanBuilder plans,
        IWorkspaceOrchestratorRegistry orchestratorRegistry,
        IWorkspaceOrchestrator orchestrator,
        IntegrationRegistry registry,
        IAgentHarnessRegistry agents)
    {
        AnsiConsole.Write(
            new Rule("[bold]HermesStack[/] — Secure Local Agent Workspaces"));

        foreach (var backend in orchestratorRegistry.All)
        {
            var availability = await backend.DetectAsync();
            AnsiConsole.MarkupLine(
                $"{Markup.Escape(backend.DisplayName),-15} " +
                (availability.IsAvailable
                    ? $"[green]Ready[/] {Markup.Escape(availability.Version ?? string.Empty)}"
                    : $"[yellow]Unavailable[/] {Markup.Escape(availability.Reason ?? string.Empty)}"));
        }

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
            .AddColumn("Orchestrator")
            .AddColumn("State")
            .AddColumn("Dashboard")
            .AddColumn("Path");
        foreach (var project in projectList)
        {
            var plan = await plans.BuildAsync(project);
            var backend = orchestratorRegistry.GetRequired(plan.OrchestratorId);
            var availability = await backend.DetectAsync();
            var status = availability.IsAvailable
                ? await orchestrator.GetStatusAsync(plan)
                : new WorkspaceStatus(
                    WorkspaceState.Unknown,
                    availability.Reason);
            var dashboard = plan.OrchestratorId == "aspire" &&
                status.Details?.StartsWith("Aspire Dashboard: ", StringComparison.Ordinal) == true
                    ? status.Details["Aspire Dashboard: ".Length..]
                    : "-";
            table.AddRow(
                Markup.Escape(project.Name),
                Markup.Escape(plan.OrchestratorId),
                status.State.ToString(),
                Markup.Escape(dashboard),
                Markup.Escape(project.HostPath));
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

    private static IntegrationRegistry CreateIntegrationRegistry(ToolchainVersions toolchain) => new(
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
            false,
            "integrate",
            toolchain.RtkVersion,
            "Ready",
            new HashSet<string>(["claude", "codex", "hermes", "opencode"], StringComparer.OrdinalIgnoreCase),
            "rtk-ai/rtk official release"),
        new(
            "caveman",
            "Caveman",
            IntegrationKind.TokenOptimizer,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.AgentHooks,
                IntegrationCapability.TokenOptimization
            },
            false,
            "integrate",
            toolchain.CavemanVersion,
            "Opt-in",
            new HashSet<string>(["claude", "codex", "hermes", "opencode"], StringComparer.OrdinalIgnoreCase),
            "JuliusBrussee/caveman signed release"),
        new(
            "openviking",
            "OpenViking",
            IntegrationKind.ContextProvider,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.SharedMemory,
                IntegrationCapability.Mcp,
                IntegrationCapability.Authentication,
                IntegrationCapability.ProjectScopedState
            },
            false,
            "integrate",
            toolchain.OpenVikingVersion,
            "Ready",
            new HashSet<string>(["claude", "codex", "hermes", "opencode"], StringComparer.OrdinalIgnoreCase),
            "volcengine/OpenViking official v0.4.23 image and first-party agent integrations"),
        new(
            "aspire",
            "Aspire",
            IntegrationKind.Orchestrator,
            new HashSet<IntegrationCapability>
            {
                IntegrationCapability.BindMounts,
                IntegrationCapability.NoNewPrivileges,
                IntegrationCapability.DropCapabilities,
                IntegrationCapability.LocalhostPortBinding,
                IntegrationCapability.InteractiveTty,
                IntegrationCapability.PersistentHome,
                IntegrationCapability.StructuredLogs,
                IntegrationCapability.Traces,
                IntegrationCapability.Metrics
            },
            false,
            "integrate",
            toolchain.AspireVersion,
            "Ready",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            "Aspire 13.6 stable CLI/AppHost integration")
    ]);

    private static async Task<int> UpdateAsync(
        string[] args,
        ToolchainVersions toolchain,
        HStackConfigStore configStore,
        DefaultDataRootProvider dataRoot,
        BackupArchiveService archives,
        ProcessRunner processRunner,
        CertificateBundleService certificateService,
        ProjectService projects,
        WorkspaceDeploymentPlanBuilder currentPlans,
        DockerComposeWorkspaceOrchestrator currentOrchestrator,
        HostMountValidator mountValidator,
        string baseCompose,
        LocalProtectedSecretStore secretStore)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack update check|plan|apply [[--json]] [[--yes]]");
        }

        var proxy = ProxyConfigurationPolicy.ValidateAndNormalize(
            await configStore.GetAsync());

        using var handler = new HttpClientHandler();
        if (proxy.Enabled)
        {
            var endpoint = proxy.Https ?? proxy.Http;
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                handler.Proxy = new WebProxy(endpoint);
                handler.UseProxy = true;
            }
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var lockService = new ToolchainLockService();
        var provider = new ToolchainUpdateMetadataProvider(client, lockService);
        var checkService = new UpdateCheckService(provider);
        var planService = new UpdatePlanService(checkService);
        var currentComponents =
            ToolchainUpdateMetadataProvider.ToManagedComponents(toolchain);
        var cli = new HermesStack.Cli.UpdateCliService(
            checkService,
            planService,
            currentComponents);

        if (!string.Equals(args[0], "apply", StringComparison.Ordinal))
        {
            return await cli.RunAsync(args);
        }

        if (!args.Contains("--yes", StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "HS7003: Update apply changes workspace images and requires --yes.");
        }

        var updatePlan = await planService.CreateAsync(currentComponents);
        if (!updatePlan.HasChanges)
        {
            AnsiConsole.MarkupLine("[green]✓[/] No managed updates are required.");
            return 0;
        }

        var manifestYaml = await provider.GetManifestYamlAsync();
        var targetToolchain = lockService.Parse(manifestYaml);
        var overridePath = Path.Combine(dataRoot.ConfigDirectory, "toolchain.lock.yaml");
        var stagedPath = overridePath + ".pending";
        var originalLock = File.Exists(overridePath)
            ? await File.ReadAllBytesAsync(overridePath)
            : null;

        var backup = await archives.BackupAsync(configOnly: true);
        var running = new List<(
            HermesStack.Domain.Projects.ProjectDefinition Project,
            WorkspaceDeploymentPlan Plan)>();
        foreach (var project in await projects.ListAsync())
        {
            var oldPlan = await currentPlans.BuildAsync(project);
            var status = await currentOrchestrator.GetStatusAsync(oldPlan);
            if (status.State == WorkspaceState.Running)
            {
                running.Add((project, oldPlan));
            }
        }

        Directory.CreateDirectory(dataRoot.ConfigDirectory);
        await File.WriteAllTextAsync(stagedPath, manifestYaml);

        string? candidateImage = null;
        try
        {
            candidateImage = await BuildCandidateWorkspaceImageAsync(
                processRunner,
                certificateService,
                targetToolchain,
                stagedPath);

            var targetContext = new OpenVikingServiceManager(
                dataRoot,
                secretStore,
                processRunner,
                targetToolchain.OpenVikingImage,
                targetToolchain.OpenVikingVersion);
            var candidatePlans = new WorkspaceDeploymentPlanBuilder(
                dataRoot,
                mountValidator,
                certificateService,
                baseCompose,
                candidateImage,
                configStore,
                configStore);
            var targetOrchestrator = new DockerComposeWorkspaceOrchestrator(
                processRunner,
                new ComposeOverrideWriter(),
                targetContext);
            var targetAgents = CreateAgentHarnessRegistry(targetOrchestrator);

            foreach (var item in running)
            {
                await currentOrchestrator.DownAsync(item.Plan);
            }

            foreach (var item in running)
            {
                var targetPlan = await candidatePlans.BuildAsync(item.Project);
                await targetOrchestrator.UpAsync(targetPlan);
                var status = await targetOrchestrator.GetStatusAsync(targetPlan);
                if (status.State != WorkspaceState.Running)
                {
                    throw new InvalidOperationException(
                        $"HS7004: Updated workspace '{item.Project.Id}' failed health validation.");
                }

                foreach (var harness in targetAgents.All)
                {
                    var installation = await harness.InspectAsync(targetPlan);
                    if (!installation.Installed)
                    {
                        throw new InvalidOperationException(
                            $"HS7004: Agent '{harness.Id}' is unavailable after updating '{item.Project.Id}'.");
                    }
                }
            }

            var finalImage =
                $"hstack/workspace-full:{targetToolchain.WorkspaceVersion}";
            _ = await processRunner.RunAsync(
                new ProcessRequest(
                    "docker",
                    ["tag", candidateImage, finalImage],
                    ThrowOnError: true));

            File.Move(stagedPath, overridePath, true);

            var finalPlans = new WorkspaceDeploymentPlanBuilder(
                dataRoot,
                mountValidator,
                certificateService,
                baseCompose,
                finalImage,
                configStore,
                configStore);
            foreach (var item in running)
            {
                var finalPlan = await finalPlans.BuildAsync(item.Project);
                await targetOrchestrator.UpAsync(finalPlan);
            }

            _ = await processRunner.RunAsync(
                new ProcessRequest(
                    "docker",
                    ["image", "rm", candidateImage],
                    ThrowOnError: false));

            AnsiConsole.MarkupLine(
                $"[green]✓[/] Managed toolchain updated transactionally. Backup: {Markup.Escape(backup.Path)}");
            if (!string.Equals(
                toolchain.WorkspaceVersion,
                targetToolchain.WorkspaceVersion,
                StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine(
                    "[yellow]![/] The workspace/toolchain is updated. Install the matching HermesStack release package to update the CLI binary itself.");
            }

            return 0;
        }
        catch (Exception exception)
        {
            if (File.Exists(stagedPath))
            {
                File.Delete(stagedPath);
            }

            if (originalLock is null)
            {
                if (File.Exists(overridePath))
                {
                    File.Delete(overridePath);
                }
            }
            else
            {
                var rollbackPath = overridePath + ".rollback";
                await File.WriteAllBytesAsync(rollbackPath, originalLock);
                File.Move(rollbackPath, overridePath, true);
            }

            foreach (var item in running)
            {
                try
                {
                    await currentOrchestrator.UpAsync(item.Plan);
                }
                catch
                {
                    // Preserve the original update failure; rollback is best-effort.
                }
            }

            if (candidateImage is not null)
            {
                _ = await processRunner.RunAsync(
                    new ProcessRequest(
                        "docker",
                        ["image", "rm", candidateImage],
                        ThrowOnError: false));
            }

            throw new InvalidOperationException(
                $"HS7005: Update failed and rollback was attempted. Configuration backup: {backup.Path}",
                exception);
        }
    }

    private static async Task<string> BuildCandidateWorkspaceImageAsync(
        ProcessRunner processRunner,
        CertificateBundleService certificateService,
        ToolchainVersions toolchain,
        string toolchainPath)
    {
        var workspaceDir = Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            "docker",
            "workspace");
        var toolchainHash = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(toolchainPath)))
            .ToLowerInvariant();
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

        var corporateBundle = await certificateService.BuildCorporateBundleAsync();
        if (corporateBundle is not null)
        {
            commonBuildArgs.AddRange(
                ["--secret", $"id=hstack_corporate_ca,src={corporateBundle}"]);
        }

        var baseTag = $"hstack/workspace-base:{toolchain.WorkspaceVersion}";
        var candidateTag =
            $"hstack/workspace-full:{toolchain.WorkspaceVersion}-candidate-{toolchainHash[..12]}";

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
            candidateTag,
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
                "--build-arg", $"OPENCODE_VERSION={toolchain.OpenCodeVersion}",
                "--build-arg", $"RTK_VERSION={toolchain.RtkVersion}",
                "--build-arg", $"RTK_RELEASE_TAG={toolchain.RtkReleaseTag}",
                "--build-arg", $"RTK_SHA256_X64={toolchain.RtkSha256X64}",
                "--build-arg", $"RTK_SHA256_ARM64={toolchain.RtkSha256Arm64}",
                "--build-arg", $"CAVEMAN_VERSION={toolchain.CavemanVersion}",
                "--build-arg", $"CAVEMAN_RELEASE_TAG={toolchain.CavemanReleaseTag}",
                "--build-arg", $"CAVEMAN_COMMIT={toolchain.CavemanCommit}"
            ]);

        return candidateTag;
    }

    private static async Task<int> ComposeAsync(
        string[] args,
        ProjectService projects,
        WorkspaceDeploymentPlanBuilder plans,
        ProcessRunner processRunner)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: hstack compose <project> config|ps|logs | hstack compose <project> -- <arguments>");
        }

        var project = await projects.GetRequiredAsync(args[0]);
        var plan = await plans.BuildAsync(project);
        await new ComposeOverrideWriter().WriteAsync(plan);

        string[] command;
        if (args[1] == "--")
        {
            command = args[2..];
            if (command.Length == 0)
            {
                throw new ArgumentException("Raw compose mode requires arguments after --.");
            }
        }
        else
        {
            if (args[1] is not ("config" or "ps" or "logs"))
            {
                throw new ArgumentException(
                    "Validated compose commands are config, ps and logs. Use -- for advanced raw mode.");
            }

            command = args[1..];
        }

        var composeArgs = new List<string>
        {
            "compose",
            "-p", $"hstack-{project.Id}",
            "-f", plan.BaseComposeFile,
            "-f", plan.OverrideComposeFile
        };
        composeArgs.AddRange(command);
        var result = await processRunner.RunAsync(
            new ProcessRequest(
                "docker",
                composeArgs,
                CaptureOutput: false));
        return result.ExitCode;
    }

    private static async Task<int> CleanAsync(
        string[] args,
        ProcessRunner processRunner)
    {
        if (!args.Contains("--yes", StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Cleanup requires --yes and only targets stopped containers carrying io.hstack.managed=true.");
        }

        var result = await processRunner.RunAsync(
            new ProcessRequest(
                "docker",
                [
                    "container",
                    "prune",
                    "--force",
                    "--filter", "label=io.hstack.managed=true"
                ],
                CaptureOutput: true));
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Docker cleanup failed: {result.StandardError}");
        }

        AnsiConsole.MarkupLine("[green]✓[/] Stopped HermesStack-managed containers cleaned.");
        return 0;
    }

    private static async Task<string?> SelectProjectAsync(
        ProjectService projects,
        string action)
    {
        var projectList = await projects.ListAsync();
        if (projectList.Count == 0)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]![/] No projects are registered for {Markup.Escape(action)}.");
            return null;
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold]{Markup.Escape(action)}[/] — select project")
                .AddChoices(projectList.Select(static project => project.Id)));
    }

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
        Console.Out.WriteLine("""
hstack
  hstack init [--orchestrator compose|aspire]
  hstack project list [--json]
  hstack project show <id> [--json]
  hstack project add <id> <hostPath> [--name <name>] [--quiet]
  hstack project edit <id> [--path <hostPath>] [--name <name>]
  hstack project remove <id> --yes
  hstack up <project> [--orchestrator compose|aspire]
  hstack down <project> [--orchestrator compose|aspire]
  hstack restart <project> [--orchestrator compose|aspire]
  hstack shell <project> [--orchestrator compose|aspire]
  hstack status [project] [--json] [--quiet] [--orchestrator compose|aspire]
  hstack ps [--json]
  hstack logs <project> [--tail <n>] [--no-follow] [--agent <agent>]

  hstack agent list [--project <project>]
  hstack agent status --project <project>
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
  hstack proxy show|set|disable
  hstack port list <project>
  hstack port add <project> <containerPort> [--host <port>]
  hstack port remove <project> <containerPort>
  hstack secret set <NAME> --project <project> --agents <csv> --from-env <ENV>
  hstack secret list --project <project>
  hstack secret remove <NAME> --project <project>
  hstack security inspect <project>
  hstack token providers
  hstack token status [project]
  hstack token enable <project> [--provider rtk] [--profile balanced] [--agents <csv>]
  hstack token disable <project> [--provider rtk|caveman|all]
  hstack token configure <project> --profile off|safe|balanced|aggressive|custom
  hstack token doctor <project>
  hstack token gain <project>
  hstack token stats <project> [--agent <agent>]

  hstack memory providers
  hstack memory status [project]
  hstack memory enable|disable <project>
  hstack memory setup
  hstack memory stop
  hstack memory inspect <project>
  hstack memory search <project> <query> [--scope project|agent|shared|global]
  hstack memory scopes [project]
  hstack memory doctor [project]
  hstack memory write <project> <name> --content <text> [--scope project|agent|shared]
  hstack memory share <project> <namespace> --with <project,...> [--write]
  hstack memory export <project> [output.ovpack]
  hstack memory import <project> <input.ovpack>
  hstack memory clear <project> [--yes]
  hstack memory integrate <project> --agent claude|codex|hermes|opencode|all
  hstack context explain <project> [--query <query>] [--agent <agent>]

  hstack doctor [project] [--network|--certificates|--security|--tokens|--memory] [--orchestrator compose|aspire]
  hstack doctor --aspire
  hstack config validate [--json]
  hstack orchestrator list|status
  hstack orchestrator set compose|aspire [--project <project>]
  hstack plan <project> [--orchestrator compose|aspire]
  hstack aspire status [project]
  hstack aspire doctor
  hstack aspire dashboard <project>
  hstack aspire inspect <project>
  hstack update check [--json]
  hstack update plan [--json]
  hstack update apply --yes
  hstack backup [project] [--config-only] [--output <archive.zip>] [--json]
  hstack restore <archive.zip> --yes
  hstack export <environment.hstack> [--include-memory] [--include-secrets --passphrase-env <ENV>]
  hstack import <environment.hstack> [--map <old>=<new>] [--passphrase-env <ENV>]
  hstack compose <project> config|ps|logs
  hstack compose <project> -- <arguments>
  hstack clean --yes
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
