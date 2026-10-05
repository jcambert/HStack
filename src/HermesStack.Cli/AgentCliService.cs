using HermesStack.Application.Abstractions;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Domain.Agents;
using HermesStack.Domain.Orchestration;
using HermesStack.Docker.Compose;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class AgentCliService(
    ProjectService projects,
    WorkspaceDeploymentPlanBuilder plans,
    DockerComposeWorkspaceOrchestrator orchestrator,
    IAgentHarnessRegistry registry)
{
    public async Task<int> AgentAsync(string[] args)
    {
        if (args.Length == 0 || args[0] == "list")
        {
            return await ListAsync(args);
        }

        if (args[0] != "run" || args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: hstack agent list [--project <project>] | hstack agent run <agent> --project <project> [-- <args>]");
        }

        var projectId = GetOption(args, "--project")
            ?? throw new ArgumentException("--project is required.");
        return await LaunchAsync(
            args[1],
            projectId,
            AgentLaunchMode.Run,
            Passthrough(args));
    }

    public Task<int> AliasAsync(string agentId, string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException($"Usage: hstack {agentId} <project> [-- <args>]");
        }

        return LaunchAsync(
            agentId,
            args[0],
            AgentLaunchMode.Run,
            Passthrough(args));
    }

    public Task<int> AuthAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Usage: hstack auth <agent> --project <project>");
        }

        var projectId = GetOption(args, "--project")
            ?? throw new ArgumentException("--project is required.");
        return LaunchAsync(
            args[0],
            projectId,
            AgentLaunchMode.Authenticate,
            Passthrough(args));
    }

    private async Task<int> ListAsync(string[] args)
    {
        var projectId = GetOption(args, "--project");
        if (projectId is null)
        {
            var table = new Table().AddColumn("Id").AddColumn("Agent");
            foreach (var harness in registry.All.OrderBy(static value => value.Id, StringComparer.Ordinal))
            {
                table.AddRow(harness.Id, harness.DisplayName);
            }

            AnsiConsole.Write(table);
            return 0;
        }

        var project = await projects.GetRequiredAsync(projectId);
        var plan = await plans.BuildAsync(project);
        var status = await orchestrator.GetStatusAsync(plan);
        if (status.State != WorkspaceState.Running)
        {
            throw new InvalidOperationException(
                $"Workspace '{project.Id}' is not running. Start it with 'hstack up {project.Id}'.");
        }

        var versions = new Table()
            .AddColumn("Agent")
            .AddColumn("Installed")
            .AddColumn("Version");

        foreach (var harness in registry.All.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            await harness.ConfigureAsync(new AgentConfigureRequest(plan));
            var info = await harness.InspectAsync(plan);
            versions.AddRow(
                info.DisplayName,
                info.Installed ? "[green]yes[/]" : "[red]no[/]",
                Markup.Escape(info.Version ?? info.Details ?? string.Empty));
        }

        AnsiConsole.Write(versions);
        return 0;
    }

    private async Task<int> LaunchAsync(
        string agentId,
        string projectId,
        AgentLaunchMode mode,
        IReadOnlyList<string> arguments)
    {
        var harness = registry.GetRequired(agentId);
        var project = await projects.GetRequiredAsync(projectId);
        var plan = await plans.BuildAsync(project);

        await harness.ConfigureAsync(new AgentConfigureRequest(plan));

        var status = await orchestrator.GetStatusAsync(plan);
        if (status.State != WorkspaceState.Running)
        {
            await orchestrator.UpAsync(plan);
        }

        return await harness.LaunchAsync(
            new AgentLaunchRequest(plan, mode, arguments));
    }

    private static IReadOnlyList<string> Passthrough(string[] args)
    {
        var separator = Array.IndexOf(args, "--");
        return separator >= 0 && separator + 1 < args.Length
            ? args[(separator + 1)..]
            : [];
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }
}
