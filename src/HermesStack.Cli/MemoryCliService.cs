using HermesStack.Application.Abstractions;
using HermesStack.Application.Context;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Domain.Context;
using HermesStack.Domain.Orchestration;
using HermesStack.Docker.Context;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class MemoryCliService(
    ProjectService projects,
    WorkspaceDeploymentPlanBuilder plans,
    IWorkspaceOrchestrator orchestrator,
    ContextService context,
    ContextProviderRegistry providers,
    OpenVikingServiceManager openViking)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack memory status|providers|enable|disable|setup|inspect|search|scopes|doctor|write|share|export|import|integrate ...");
        }

        return args[0] switch
        {
            "providers" => Providers(),
            "status" => await StatusAsync(args[1..]),
            "enable" => await EnableAsync(args[1..], true),
            "disable" => await EnableAsync(args[1..], false),
            "setup" => await SetupAsync(args[1..]),
            "inspect" => await InspectAsync(args[1..]),
            "search" => await SearchAsync(args[1..]),
            "scopes" => await ScopesAsync(args[1..]),
            "doctor" => await DoctorAsync(args[1..]),
            "write" => await WriteAsync(args[1..]),
            "share" => await ShareAsync(args[1..]),
            "export" => await ExportAsync(args[1..]),
            "import" => await ImportAsync(args[1..]),
            "integrate" => await IntegrateAsync(args[1..]),
            _ => throw new ArgumentException($"Unknown memory command '{args[0]}'.")
        };
    }

    private int Providers()
    {
        var table = new Table()
            .AddColumn("Provider")
            .AddColumn("Status")
            .AddColumn("Purpose");

        foreach (var provider in providers.All.OrderBy(static item => item.Id, StringComparer.Ordinal))
        {
            table.AddRow(
                Markup.Escape(provider.Id),
                "configured",
                "durable context / memory / shared resources");
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> StatusAsync(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: hstack memory status [project]");
        }

        if (args.Length == 0)
        {
            var list = await projects.ListAsync();
            var table = new Table()
                .AddColumn("Project")
                .AddColumn("Enabled")
                .AddColumn("Provider")
                .AddColumn("Capture")
                .AddColumn("Budget");

            foreach (var project in list)
            {
                var configuration = await context.GetConfigurationAsync(project.Id);
                table.AddRow(
                    Markup.Escape(project.Id),
                    configuration.Enabled ? "yes" : "no",
                    Markup.Escape(configuration.ProviderId),
                    configuration.CaptureMode.ToString().ToLowerInvariant(),
                    $"{configuration.EffectiveBudget.MaxTokens:N0} tokens / {configuration.EffectiveBudget.MaxItems} items");
            }

            AnsiConsole.Write(table);
            return 0;
        }

        _ = await projects.GetRequiredAsync(args[0]);
        var config = await context.GetConfigurationAsync(args[0]);
        var tableOne = new Table().AddColumn("Property").AddColumn("Value");
        tableOne.AddRow("Project", Markup.Escape(args[0]));
        tableOne.AddRow("Enabled", config.Enabled ? "yes" : "no");
        tableOne.AddRow("Provider", Markup.Escape(config.ProviderId));
        tableOne.AddRow("Capture", config.CaptureMode.ToString().ToLowerInvariant());
        tableOne.AddRow("Default scope", config.DefaultScope.ToString().ToLowerInvariant());
        tableOne.AddRow("Context budget", $"{config.EffectiveBudget.MaxTokens:N0} tokens");
        tableOne.AddRow("Max items", config.EffectiveBudget.MaxItems.ToString());
        AnsiConsole.Write(tableOne);
        return 0;
    }

    private async Task<int> EnableAsync(string[] args, bool enabled)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException(
                enabled ? "Usage: hstack memory enable <project>" : "Usage: hstack memory disable <project>");
        }

        _ = await projects.GetRequiredAsync(args[0]);
        var configuration = await context.SetEnabledAsync(args[0], enabled);
        AnsiConsole.MarkupLine(
            $"[green]✓[/] Memory {(configuration.Enabled ? "enabled" : "disabled")} for {Markup.Escape(args[0])}.");
        return 0;
    }

    private async Task<int> SetupAsync(string[] args)
    {
        if (args.Length != 0)
        {
            throw new ArgumentException("Usage: hstack memory setup");
        }

        await openViking.RunSetupAsync();
        AnsiConsole.MarkupLine(
            "[green]✓[/] OpenViking setup completed and secure API-key mode re-applied.");
        return 0;
    }

    private async Task<int> InspectAsync(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("Usage: hstack memory inspect <project>");
        }

        _ = await projects.GetRequiredAsync(args[0]);
        var configuration = await context.GetConfigurationAsync(args[0]);
        var info = configuration.Enabled
            ? await context.EnsureProjectAsync(args[0])
            : null;

        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Provider", Markup.Escape(configuration.ProviderId));
        table.AddRow("Capture", configuration.CaptureMode.ToString().ToLowerInvariant());
        table.AddRow("Private scope", "provider-native project user");
        table.AddRow("Agent scope", "provider-native peer memory");
        table.AddRow("Shared scope", "viking://resources/hstack-shared/ + restricted ACL");
        table.AddRow("Global scope", "viking://resources/hstack-global/");
        if (info is not null)
        {
            table.AddRow("Account", Markup.Escape(info.AccountId));
            table.AddRow("User", Markup.Escape(info.UserId));
            table.AddRow("Endpoint", Markup.Escape(info.Endpoint));
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> SearchAsync(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: hstack memory search <project> <query> [--scope project|agent|shared|global] [--agent <agent>]");
        }

        _ = await projects.GetRequiredAsync(args[0]);
        var scope = ParseScope(GetOption(args, "--scope") ?? "project");
        var agent = GetOption(args, "--agent");
        if (scope == ContextScope.Agent && string.IsNullOrWhiteSpace(agent))
        {
            throw new ArgumentException("--agent is required for agent scope.");
        }

        var items = await context.SearchAsync(new ContextQuery(
            args[0],
            args[1],
            scope,
            agent));

        var table = new Table()
            .AddColumn("URI")
            .AddColumn("Score")
            .AddColumn("Tokens")
            .AddColumn("Context");
        foreach (var item in items)
        {
            table.AddRow(
                Markup.Escape(item.Uri),
                item.Score?.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "-",
                item.EstimatedTokens.ToString(),
                Markup.Escape(Trim(item.Content, 240)));
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> ScopesAsync(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: hstack memory scopes [project]");
        }

        if (args.Length == 1)
        {
            _ = await projects.GetRequiredAsync(args[0]);
        }

        var table = new Table()
            .AddColumn("Logical scope")
            .AddColumn("OpenViking mapping")
            .AddColumn("Isolation");
        table.AddRow("session", "OpenViking session API", "session lifecycle");
        table.AddRow("agent", "viking://~/peers/<agent>/memories/", "project user + peer");
        table.AddRow("project", "viking://~/", "dedicated project user/API key");
        table.AddRow("shared", "viking://resources/hstack-shared/", "explicit restricted ACL");
        table.AddRow("global", "viking://resources/hstack-global/", "explicit shared policy");
        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> DoctorAsync(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: hstack memory doctor [project]");
        }

        if (args.Length == 1)
        {
            _ = await projects.GetRequiredAsync(args[0]);
        }

        var checks = await context.DoctorAsync(args.FirstOrDefault());
        var table = new Table()
            .AddColumn("Provider")
            .AddColumn("Version")
            .AddColumn("Status")
            .AddColumn("Details");
        foreach (var check in checks)
        {
            table.AddRow(
                "OpenViking",
                Markup.Escape(check.Version ?? "-"),
                check.IsAvailable ? "[green]Ready[/]" : "[yellow]Unavailable[/]",
                Markup.Escape(check.Details ?? "-"));
        }

        AnsiConsole.Write(table);
        return checks.All(static item => item.IsAvailable) ? 0 : 1;
    }

    private async Task<int> WriteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: hstack memory write <project> <name> --content <text> [--scope project|agent|shared] [--agent <agent>]");
        }

        _ = await projects.GetRequiredAsync(args[0]);
        var contentValue = GetOption(args, "--content")
            ?? throw new ArgumentException("--content is required.");
        var scope = ParseScope(GetOption(args, "--scope") ?? "project");
        var agent = GetOption(args, "--agent");
        await context.StoreAsync(new ContextWriteRequest(
            args[0],
            scope,
            args[1],
            contentValue,
            agent));
        AnsiConsole.MarkupLine("[green]✓[/] Context written.");
        return 0;
    }

    private async Task<int> ShareAsync(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: hstack memory share <project> <namespace> --with <project,...> [--write]");
        }

        _ = await projects.GetRequiredAsync(args[0]);
        var targets = (GetOption(args, "--with")
            ?? throw new ArgumentException("--with is required."))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var target in targets)
        {
            _ = await projects.GetRequiredAsync(target);
        }

        await context.ShareAsync(new ContextShareRequest(
            args[0],
            args[1],
            targets,
            args.Contains("--write", StringComparer.Ordinal)));

        AnsiConsole.MarkupLine(
            $"[green]✓[/] Shared namespace {Markup.Escape(args[1])} configured with provider-native ACLs.");
        return 0;
    }

    private async Task<int> ExportAsync(string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            throw new ArgumentException("Usage: hstack memory export <project> [output.ovpack]");
        }

        _ = await projects.GetRequiredAsync(args[0]);
        var output = args.Length == 2
            ? args[1]
            : $"{args[0]}-memory-{DateTime.UtcNow:yyyyMMddHHmmss}.ovpack";
        await context.ExportAsync(args[0], output);
        AnsiConsole.MarkupLine(
            $"[green]✓[/] Memory exported to {Markup.Escape(Path.GetFullPath(output))}.");
        AnsiConsole.MarkupLine(
            "[yellow]![/] OVPack archives are plaintext; protect them as sensitive project data.");
        return 0;
    }

    private async Task<int> ImportAsync(string[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("Usage: hstack memory import <project> <input.ovpack>");
        }

        _ = await projects.GetRequiredAsync(args[0]);
        await context.ImportAsync(args[0], args[1]);
        AnsiConsole.MarkupLine("[green]✓[/] Memory imported.");
        return 0;
    }

    private async Task<int> IntegrateAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack memory integrate <project> --agent claude|codex|hermes|opencode|all");
        }

        var project = await projects.GetRequiredAsync(args[0]);
        var requested = (GetOption(args, "--agent") ?? "all").ToLowerInvariant();
        var agents = requested == "all"
            ? new[] { "claude", "codex", "hermes", "opencode" }
            : new[] { requested };
        if (agents.Any(static agent => agent is not ("claude" or "codex" or "hermes" or "opencode")))
        {
            throw new ArgumentException("Supported agents: claude, codex, hermes, opencode, all.");
        }

        _ = await context.EnsureProjectAsync(project.Id);
        var plan = await plans.BuildAsync(project);
        var status = await orchestrator.GetStatusAsync(plan);
        if (status.State != WorkspaceState.Running)
        {
            await orchestrator.UpAsync(plan);
        }

        foreach (var agent in agents)
        {
            var command = agent == "hermes"
                ? new[] { "hermes", "memory", "setup", "openviking" }
                : new[]
                {
                    "/bin/bash",
                    "-lc",
                    $"curl -fsSL https://openviking.ai/install | bash -s -- --harness {agent}"
                };

            var exit = await orchestrator.ExecAsync(new WorkspaceExecutionRequest(
                plan,
                command,
                Interactive: true));
            if (exit != 0)
            {
                return exit;
            }
        }

        AnsiConsole.MarkupLine(
            "[green]✓[/] OpenViking agent integration completed using upstream first-party installers.");
        return 0;
    }

    private static ContextScope ParseScope(string value) =>
        Enum.TryParse<ContextScope>(value, true, out var scope)
            ? scope
            : throw new ArgumentException($"Unknown context scope '{value}'.");

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
