using System.Text.Json;
using HermesStack.Application.Abstractions;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Infrastructure.Operations;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class OperationsCliService(
    ProjectService projects,
    WorkspaceDeploymentPlanBuilder plans,
    IWorkspaceOrchestrator orchestrator,
    BackupArchiveService archives)
{
    public async Task<int> BackupAsync(string[] args)
    {
        var projectId = args.Length > 0 &&
            !args[0].StartsWith("--", StringComparison.Ordinal)
                ? args[0]
                : null;
        var configOnly = args.Contains("--config-only", StringComparer.Ordinal);
        var output = GetOption(args, "--output");
        var json = args.Contains("--json", StringComparer.Ordinal);

        if (projectId is not null)
        {
            _ = await projects.GetRequiredAsync(projectId);
        }

        var result = await archives.BackupAsync(projectId, configOnly, output);
        if (json)
        {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(result));
        }
        else
        {
            AnsiConsole.MarkupLine(
                $"[green]✓[/] Backup created: {Markup.Escape(result.Path)}");
        }

        return 0;
    }

    public async Task<int> RestoreAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Usage: hstack restore <archive.zip> --yes");
        }

        if (!args.Contains("--yes", StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "HS8005: Restore is destructive. Re-run with --yes.");
        }

        await archives.RestoreAsync(args[0]);
        AnsiConsole.MarkupLine("[green]✓[/] HermesStack backup restored.");
        return 0;
    }

    public async Task<int> ExportAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Usage: hstack export <environment.hstack>");
        }

        if (args.Contains("--include-secrets", StringComparer.Ordinal))
        {
            throw new NotSupportedException(
                "HS8006: Portable secret export requires encrypted secret packaging and is intentionally disabled.");
        }

        var result = await archives.ExportAsync(args[0]);
        AnsiConsole.MarkupLine(
            $"[green]✓[/] Portable environment exported: {Markup.Escape(result.Path)}");
        return 0;
    }

    public async Task<int> ImportAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Usage: hstack import <environment.hstack> [[--map <old>=<new>]]");
        }

        await archives.ImportAsync(args[0]);

        var mappings = GetOptions(args, "--map")
            .Select(ParseMapping)
            .ToDictionary(
                static item => item.OldPath,
                static item => item.NewPath,
                StringComparer.OrdinalIgnoreCase);

        var invalid = new List<string>();
        foreach (var project in await projects.ListAsync())
        {
            if (Directory.Exists(project.HostPath))
            {
                continue;
            }

            if (mappings.TryGetValue(project.HostPath, out var newPath))
            {
                await projects.EditAsync(project.Id, newPath, project.Name);
                continue;
            }

            invalid.Add(project.Id);
        }

        if (invalid.Count > 0)
        {
            throw new InvalidOperationException(
                "HS8004: Imported project paths are invalid on this host: " +
                string.Join(", ", invalid) +
                ". Re-run import with --map <old>=<new>.");
        }

        AnsiConsole.MarkupLine("[green]✓[/] Portable environment imported.");
        return 0;
    }

    public async Task<int> LogsAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack logs <project> [[--tail <n>]] [[--no-follow]] [[--agent <agent>]]");
        }

        var project = await projects.GetRequiredAsync(args[0]);
        var plan = await plans.BuildAsync(project);
        var tailText = GetOption(args, "--tail");
        int? tail = null;
        if (tailText is not null &&
            (!int.TryParse(tailText, out var parsed) || parsed < 0))
        {
            throw new ArgumentException("--tail must be a non-negative integer.");
        }
        else if (tailText is not null)
        {
            tail = int.Parse(tailText, System.Globalization.CultureInfo.InvariantCulture);
        }

        var agent = GetOption(args, "--agent");
        if (agent is not null)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]![/] Separate {Markup.Escape(agent)} logs are not exposed by the Compose backend; showing workspace logs.");
        }

        return await orchestrator.StreamLogsAsync(
            new HermesStack.Domain.Orchestration.WorkspaceLogRequest(
                plan,
                Follow: !args.Contains("--no-follow", StringComparer.Ordinal),
                Tail: tail));
    }

    private static (string OldPath, string NewPath) ParseMapping(string value)
    {
        var separator = value.IndexOf('=');
        if (separator <= 0 || separator == value.Length - 1)
        {
            throw new ArgumentException("--map must use <old>=<new>.");
        }

        return (value[..separator], value[(separator + 1)..]);
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }

    private static IReadOnlyList<string> GetOptions(string[] args, string name)
    {
        var result = new List<string>();
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                result.Add(args[index + 1]);
                index++;
            }
        }

        return result;
    }
}
