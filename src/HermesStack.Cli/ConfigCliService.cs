using System.Text.Json;
using HermesStack.Application.Network;
using HermesStack.Application.Projects;
using HermesStack.Infrastructure.Configuration;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class ConfigCliService(
    HStackConfigStore configStore,
    ProjectService projects)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 ||
            !string.Equals(args[0], "validate", StringComparison.Ordinal))
        {
            throw new ArgumentException("Usage: hstack config validate [[--json]]");
        }

        var issues = new List<ConfigValidationIssue>();
        try
        {
            _ = ProxyConfigurationPolicy.ValidateAndNormalize(
                await configStore.GetAsync());
        }
        catch (Exception exception)
        {
            issues.Add(new("HS1001", "hstack.yaml", exception.Message));
        }

        foreach (var project in await projects.ListAsync())
        {
            var mount = projects.ValidateHostPath(project.HostPath);
            if (!mount.IsAllowed)
            {
                issues.Add(new(mount.Code, project.Id, mount.Message));
            }
            else if (!Directory.Exists(project.HostPath))
            {
                issues.Add(new(
                    "HS1008",
                    project.Id,
                    $"Project directory does not exist on this host: {project.HostPath}"));
            }
        }

        var json = args.Contains("--json", StringComparer.Ordinal);
        if (json)
        {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(new
            {
                valid = issues.Count == 0,
                issues
            }));
        }
        else if (issues.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓[/] HermesStack configuration is valid.");
        }
        else
        {
            var table = new Table()
                .AddColumn("Code")
                .AddColumn("Scope")
                .AddColumn("Message");
            foreach (var issue in issues)
            {
                table.AddRow(
                    Markup.Escape(issue.Code),
                    Markup.Escape(issue.Scope),
                    Markup.Escape(issue.Message));
            }

            AnsiConsole.Write(table);
        }

        return issues.Count == 0 ? 0 : 1;
    }

    private sealed record ConfigValidationIssue(
        string Code,
        string Scope,
        string Message);
}
