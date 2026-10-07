using System.Text.Json;
using HermesStack.Application.Updates;
using HermesStack.Domain.Updates;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class UpdateCliService(
    UpdateCheckService updates,
    UpdatePlanService plans,
    IReadOnlyList<ManagedComponentVersion> current)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack update check|plan [[--json]]");
        }

        return args[0] switch
        {
            "check" => await CheckAsync(args),
            "plan" => await PlanAsync(args),
            _ => throw new ArgumentException(
                "Usage: hstack update check|plan [[--json]]")
        };
    }

    private async Task<int> CheckAsync(string[] args)
    {
        var result = await updates.CheckAsync(current);
        if (args.Contains("--json", StringComparer.Ordinal))
        {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(result));
            return 0;
        }

        var table = new Table()
            .AddColumn("Component")
            .AddColumn("Pinned")
            .AddColumn("Available")
            .AddColumn("Status");

        foreach (var item in result.Items)
        {
            table.AddRow(
                Markup.Escape(item.DisplayName),
                Markup.Escape(item.CurrentVersion),
                Markup.Escape(item.AvailableVersion),
                StatusMarkup(item.State));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(
            $"[grey]Managed update source:[/] {Markup.Escape(result.Source)}");

        if (result.HasUnknowns)
        {
            AnsiConsole.MarkupLine(
                "[yellow]![/] Some managed components are missing from the update manifest.");
        }

        AnsiConsole.MarkupLine(
            result.HasUpdates
                ? "[yellow]![/] Managed updates are available."
                : "[green]✓[/] Managed components match the update channel.");
        return 0;
    }

    private async Task<int> PlanAsync(string[] args)
    {
        var plan = await plans.CreateAsync(current);
        if (args.Contains("--json", StringComparer.Ordinal))
        {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(plan));
            return 0;
        }

        var changes = new Table()
            .AddColumn("Component")
            .AddColumn("Current")
            .AddColumn("Target")
            .AddColumn("Status");
        foreach (var item in plan.Changes)
        {
            changes.AddRow(
                Markup.Escape(item.DisplayName),
                Markup.Escape(item.CurrentVersion),
                Markup.Escape(item.AvailableVersion),
                StatusMarkup(item.State));
        }

        if (plan.Changes.Count > 0)
        {
            AnsiConsole.Write(changes);
        }
        else
        {
            AnsiConsole.MarkupLine("[green]✓[/] No managed changes are required.");
        }

        var steps = new Table()
            .AddColumn("#")
            .AddColumn("Step")
            .AddColumn("Mutation");
        foreach (var step in plan.Steps)
        {
            steps.AddRow(
                step.Order.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Markup.Escape(step.Description),
                step.MutatesState ? "yes" : "no");
        }

        AnsiConsole.Write(steps);
        AnsiConsole.MarkupLine(
            $"[grey]Managed update source:[/] {Markup.Escape(plan.Source)}");
        return 0;
    }

    private static string StatusMarkup(UpdateState state) => state switch
    {
        UpdateState.UpToDate => "[green]current[/]",
        UpdateState.UpdateAvailable => "[yellow]update available[/]",
        UpdateState.Ahead => "[blue]ahead[/]",
        UpdateState.Different => "[yellow]changed[/]",
        _ => "[red]unavailable[/]"
    };
}
