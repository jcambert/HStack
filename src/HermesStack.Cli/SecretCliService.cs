using System.Text.RegularExpressions;
using HermesStack.Application.Abstractions;
using HermesStack.Application.Projects;
using HermesStack.Domain.Security;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed partial class SecretCliService(
    ProjectService projects,
    IAgentHarnessRegistry agents,
    ISecretStore secretStore,
    ISecretPolicyStore policyStore)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack secret set|list|remove ...");
        }

        return args[0] switch
        {
            "set" => await SetAsync(args[1..]),
            "list" => await ListAsync(args[1..]),
            "remove" => await RemoveAsync(args[1..]),
            _ => throw new ArgumentException(
                "Usage: hstack secret set|list|remove ...")
        };
    }

    private async Task<int> SetAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack secret set <NAME> --project <project> --agents <csv> --from-env <ENV>");
        }

        var name = args[0];
        if (!SecretNameRegex().IsMatch(name))
        {
            throw new ArgumentException(
                "Secret names must match ^[A-Z_][A-Z0-9_]{0,127}$.");
        }

        var projectId = GetOption(args, "--project")
            ?? throw new ArgumentException("--project is required.");
        _ = await projects.GetRequiredAsync(projectId);

        var agentIds = (GetOption(args, "--agents")
                ?? throw new ArgumentException("--agents is required."))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (agentIds.Length == 0)
        {
            throw new ArgumentException("At least one allowed agent is required.");
        }

        foreach (var agentId in agentIds)
        {
            _ = agents.GetRequired(agentId);
        }

        var sourceName = GetOption(args, "--from-env")
            ?? throw new ArgumentException(
                "--from-env is required so secret values never appear in CLI arguments.");
        var value = Environment.GetEnvironmentVariable(sourceName);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Environment variable '{sourceName}' is empty or not defined.");
        }

        var reference = new SecretReference(projectId, name);
        await secretStore.SetAsync(reference, new SecretValue(value));
        await policyStore.SetAsync(new SecretPolicy(projectId, name, agentIds));

        AnsiConsole.MarkupLine(
            $"[green]✓[/] Secret {Markup.Escape(name)} stored for project " +
            $"{Markup.Escape(projectId)}; allowed agents: {Markup.Escape(string.Join(",", agentIds))}.");
        return 0;
    }

    private async Task<int> ListAsync(string[] args)
    {
        var projectId = GetOption(args, "--project")
            ?? throw new ArgumentException("--project is required.");
        _ = await projects.GetRequiredAsync(projectId);
        var policies = await policyStore.ListAsync(projectId);

        var table = new Table().AddColumn("Secret").AddColumn("Allowed agents");
        foreach (var policy in policies)
        {
            table.AddRow(
                Markup.Escape(policy.Name),
                Markup.Escape(string.Join(",", policy.Agents)));
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> RemoveAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack secret remove <NAME> --project <project>");
        }

        var projectId = GetOption(args, "--project")
            ?? throw new ArgumentException("--project is required.");
        _ = await projects.GetRequiredAsync(projectId);
        var reference = new SecretReference(projectId, args[0]);
        await secretStore.RemoveAsync(reference);
        await policyStore.RemoveAsync(projectId, args[0]);
        AnsiConsole.MarkupLine(
            $"[green]✓[/] Secret {Markup.Escape(args[0])} removed.");
        return 0;
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }

    [GeneratedRegex("^[A-Z_][A-Z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretNameRegex();
}
