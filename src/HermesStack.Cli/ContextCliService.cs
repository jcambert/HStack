using HermesStack.Application.Context;
using HermesStack.Application.Projects;
using HermesStack.Domain.Context;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class ContextCliService(
    ProjectService projects,
    ContextService context)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2 || args[0] != "explain")
        {
            throw new ArgumentException(
                "Usage: hstack context explain <project> [--agent <agent>] [--query <query>] [--scope project|agent|shared|global]");
        }

        var project = await projects.GetRequiredAsync(args[1]);
        var query = GetOption(args, "--query");
        if (!string.IsNullOrWhiteSpace(query))
        {
            var scope = ParseScope(GetOption(args, "--scope") ?? "project");
            var agent = GetOption(args, "--agent");
            _ = await context.SearchAsync(new ContextQuery(
                project.Id,
                query,
                scope,
                agent));
        }

        var trace = await context.GetLatestTraceAsync(project.Id);
        if (trace is null)
        {
            AnsiConsole.MarkupLine(
                "[yellow]No context retrieval trace is available yet.[/]");
            return 1;
        }

        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Query", Markup.Escape(trace.Query));
        table.AddRow("Provider", Markup.Escape(trace.ProviderId));
        table.AddRow("Scope", trace.Scope.ToString().ToLowerInvariant());
        table.AddRow("Namespace searched", Markup.Escape(trace.TargetUri));
        table.AddRow("Items requested", trace.RequestedItems.ToString());
        table.AddRow("Items retrieved", trace.ReturnedItems.ToString());
        table.AddRow("Estimated tokens", trace.EstimatedTokens.ToString("N0"));
        table.AddRow(
            "Included",
            Markup.Escape(trace.IncludedUris.Count == 0 ? "-" : string.Join(", ", trace.IncludedUris)));
        table.AddRow(
            "Excluded",
            Markup.Escape(trace.ExcludedUris.Count == 0 ? "-" : string.Join(", ", trace.ExcludedUris)));
        table.AddRow(
            "Reason for exclusion",
            trace.ExcludedUris.Count == 0 ? "-" : "context budget / max items");
        AnsiConsole.Write(table);
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
}
