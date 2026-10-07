using HermesStack.Application.Updates;
using HermesStack.Domain.Updates;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class UpdateCliService(
    UpdateCheckService updates,
    IReadOnlyList<ManagedComponentVersion> current)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1 || !string.Equals(args[0], "check", StringComparison.Ordinal))
        {
            throw new ArgumentException("Usage: hstack update check");
        }

        var result = await updates.CheckAsync(current);
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

    private static string StatusMarkup(UpdateState state) => state switch
    {
        UpdateState.UpToDate => "[green]current[/]",
        UpdateState.UpdateAvailable => "[yellow]update available[/]",
        UpdateState.Ahead => "[blue]ahead[/]",
        UpdateState.Different => "[yellow]changed[/]",
        _ => "[red]unavailable[/]"
    };
}
