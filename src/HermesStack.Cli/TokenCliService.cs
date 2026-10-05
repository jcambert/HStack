using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Application.Tokens;
using HermesStack.Domain.Tokens;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class TokenCliService(
    ProjectService projects,
    WorkspaceDeploymentPlanBuilder plans,
    TokenOptimizationService tokens,
    TokenOptimizerRegistry registry)
{
    private static readonly string[] DefaultAgents =
        ["claude", "codex", "hermes", "opencode"];

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack token status|providers|enable|disable|configure|doctor|gain|stats ...");
        }

        return args[0] switch
        {
            "providers" => Providers(),
            "status" => await StatusAsync(args[1..]),
            "enable" => await EnableAsync(args[1..]),
            "disable" => await DisableAsync(args[1..]),
            "configure" => await ConfigureAsync(args[1..]),
            "doctor" => await DoctorAsync(args[1..]),
            "gain" => await GainAsync(args[1..]),
            "stats" => await StatsAsync(args[1..]),
            _ => throw new ArgumentException(
                $"Unknown token command '{args[0]}'.")
        };
    }

    private int Providers()
    {
        var table = new Table()
            .AddColumn("Provider")
            .AddColumn("Version")
            .AddColumn("Agents")
            .AddColumn("Metrics")
            .AddColumn("Policy");

        foreach (var provider in registry.All.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            table.AddRow(
                Markup.Escape(provider.Id),
                Markup.Escape(provider.Version),
                Markup.Escape(string.Join(",", provider.SupportedAgents.OrderBy(static value => value, StringComparer.Ordinal))),
                provider.SupportsGainMetrics ? "Estimated" : "Unavailable",
                provider.Id == "caveman"
                    ? "opt-in; aggressive/custom; stacking potentially lossy"
                    : "default; safe/balanced");
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> StatusAsync(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: hstack token status [project]");
        }

        if (args.Length == 0)
        {
            var projectList = await projects.ListAsync();
            var table = new Table()
                .AddColumn("Project")
                .AddColumn("Enabled")
                .AddColumn("Profile")
                .AddColumn("Providers");

            foreach (var project in projectList)
            {
                var configuration = await tokens.GetAsync(project.Id);
                table.AddRow(
                    Markup.Escape(project.Id),
                    configuration.Enabled ? "yes" : "no",
                    configuration.Profile.ToString().ToLowerInvariant(),
                    Markup.Escape(string.Join(
                        ",",
                        configuration.EffectiveProviders.Select(static value => value.ProviderId))));
            }

            AnsiConsole.Write(table);
            return 0;
        }

        var configuration = await tokens.GetAsync(args[0]);
        RenderConfiguration(configuration);
        return 0;
    }

    private async Task<int> EnableAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack token enable <project> [--provider rtk] [--profile balanced] [--agents <csv>] [--allow-lossy-stack]");
        }

        var provider = GetOption(args, "--provider") ?? "rtk";
        var profile = ParseProfile(GetOption(args, "--profile") ?? "balanced");
        var agents = (GetOption(args, "--agents") ?? string.Join(",", DefaultAgents))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var allowLossy = args.Contains("--allow-lossy-stack", StringComparer.Ordinal);

        var plan = await PlanAsync(args[0]);
        var configuration = await tokens.EnableAsync(
            plan,
            provider,
            profile,
            agents,
            allowLossy);

        AnsiConsole.MarkupLine(
            $"[green]✓[/] Token optimizer {Markup.Escape(provider)} enabled for " +
            $"{Markup.Escape(plan.Project.Id)}.");
        RenderConfiguration(configuration);
        return 0;
    }

    private async Task<int> DisableAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack token disable <project> [--provider rtk|caveman|all]");
        }

        var plan = await PlanAsync(args[0]);
        var provider = GetOption(args, "--provider") ?? "all";
        var current = await tokens.GetAsync(plan.Project.Id);
        TokenOptimizationConfiguration updated = current;

        var ids = string.Equals(provider, "all", StringComparison.OrdinalIgnoreCase)
            ? current.EffectiveProviders.Select(static value => value.ProviderId).ToArray()
            : [provider];

        foreach (var id in ids)
        {
            updated = await tokens.DisableAsync(plan, id);
        }

        AnsiConsole.MarkupLine("[green]✓[/] Token optimization updated.");
        RenderConfiguration(updated);
        return 0;
    }

    private async Task<int> ConfigureAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack token configure <project> --profile off|safe|balanced|aggressive|custom");
        }

        var profileText = GetOption(args, "--profile")
            ?? throw new ArgumentException("--profile is required.");
        _ = await projects.GetRequiredAsync(args[0]);
        var configuration = await tokens.ConfigureProfileAsync(
            args[0],
            ParseProfile(profileText));
        RenderConfiguration(configuration);
        return 0;
    }

    private async Task<int> DoctorAsync(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("Usage: hstack token doctor <project>");
        }

        var plan = await PlanAsync(args[0]);
        var health = await tokens.DoctorAsync(plan);
        var table = new Table()
            .AddColumn("Provider")
            .AddColumn("Version")
            .AddColumn("Status")
            .AddColumn("Details");

        foreach (var item in health)
        {
            table.AddRow(
                Markup.Escape(item.DisplayName),
                Markup.Escape(item.Version),
                item.Available ? "[green]Ready[/]" : "[red]Unavailable[/]",
                Markup.Escape(item.Details));
        }

        AnsiConsole.Write(table);
        return health.All(static value => value.Available) ? 0 : 1;
    }

    private async Task<int> GainAsync(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("Usage: hstack token gain <project>");
        }

        var plan = await PlanAsync(args[0]);
        var gains = await tokens.GainAsync(plan);
        var table = new Table()
            .AddColumn("Provider")
            .AddColumn("Evidence")
            .AddColumn("Raw tokens")
            .AddColumn("Optimized")
            .AddColumn("Saved")
            .AddColumn("Reduction")
            .AddColumn("Meaning");

        foreach (var gain in gains)
        {
            table.AddRow(
                Markup.Escape(gain.ProviderId),
                gain.Evidence.ToString(),
                Number(gain.RawTokens),
                Number(gain.OptimizedTokens),
                Number(gain.SavedTokens),
                gain.SavingsPercent is double percent
                    ? $"{percent:F1}%"
                    : "-",
                Markup.Escape(gain.Details));
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> StatsAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack token stats <project> [--agent <agent>]");
        }

        _ = await projects.GetRequiredAsync(args[0]);
        var requestedAgent = GetOption(args, "--agent");
        var history = await tokens.HistoryAsync(args[0]);
        if (!string.IsNullOrWhiteSpace(requestedAgent))
        {
            history = history
                .Where(value => string.Equals(
                    value.Agent,
                    requestedAgent,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var table = new Table()
            .AddColumn("Timestamp")
            .AddColumn("Agent")
            .AddColumn("Provider")
            .AddColumn("Evidence")
            .AddColumn("Raw")
            .AddColumn("Optimized")
            .AddColumn("Saved")
            .AddColumn("Reduction");

        foreach (var item in history.Take(50))
        {
            table.AddRow(
                item.Timestamp.ToString("u"),
                item.Agent,
                item.ProviderId,
                item.Evidence.ToString(),
                Number(item.RawTokens),
                Number(item.OptimizedTokens),
                Number(item.SavedTokens),
                item.SavingsPercent is double percent ? $"{percent:F1}%" : "-");
        }

        AnsiConsole.Write(table);
        if (!string.IsNullOrWhiteSpace(requestedAgent) && history.Count == 0)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Unavailable:[/] RTK currently exposes project aggregate gain, not reliable per-agent attribution.");
        }

        return 0;
    }

    private async Task<HermesStack.Domain.Orchestration.WorkspaceDeploymentPlan> PlanAsync(
        string projectId)
    {
        var project = await projects.GetRequiredAsync(projectId);
        return await plans.BuildAsync(project);
    }

    private static TokenOptimizationProfile ParseProfile(string value) =>
        Enum.TryParse<TokenOptimizationProfile>(value, ignoreCase: true, out var profile)
            ? profile
            : throw new ArgumentException(
                $"Unknown token optimization profile '{value}'.");

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }

    private static string Number(long? value) =>
        value?.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) ?? "-";

    private static void RenderConfiguration(TokenOptimizationConfiguration configuration)
    {
        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Project", Markup.Escape(configuration.ProjectId));
        table.AddRow("Enabled", configuration.Enabled ? "yes" : "no");
        table.AddRow("Profile", configuration.Profile.ToString().ToLowerInvariant());
        table.AddRow(
            "Providers",
            configuration.EffectiveProviders.Count == 0
                ? "-"
                : Markup.Escape(string.Join(
                    "; ",
                    configuration.EffectiveProviders.Select(value =>
                        $"{value.ProviderId}[{string.Join(",", value.Agents)}]"))));
        AnsiConsole.Write(table);
    }
}
