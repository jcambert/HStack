using HermesStack.Application.Abstractions;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Application.Sessions;
using HermesStack.Domain.Orchestration;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace HermesStack.Cli;

internal sealed class SessionCliService(
    ProjectService projects,
    WorkspaceDeploymentPlanBuilder plans,
    IWorkspaceOrchestrator orchestrator,
    HerdrSessionService sessions)
{
    private static readonly Regex TmuxNamePattern = new(
        "^[A-Za-z0-9_.-]{1,64}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<int> SessionAsync(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: hstack session init|status|list|agents|stop <project> | hstack session run <agent> <project> --name <name> [-- <args>]");
        }

        return args[0] switch
        {
            "init" => await InitAsync(args[1]),
            "status" => await StatusAsync(args[1]),
            "list" => await PrintAsync(args[1], static (service, plan, ct) => service.ListSessionsAsync(plan, ct)),
            "agents" => await PrintAsync(args[1], static (service, plan, ct) => service.ListAgentsAsync(plan, ct)),
            "stop" => await StopAsync(args[1]),
            "run" => await RunAsync(args),
            _ => throw new ArgumentException(
                $"Unknown session command '{args[0]}'.")
        };
    }

    public async Task<int> HerdrAsync(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("Usage: hstack herdr <project>");
        }

        var plan = await PlanAsync(args[0]);
        return await sessions.AttachAsync(plan);
    }

    public async Task<int> TmuxAsync(string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            throw new ArgumentException("Usage: hstack tmux <project> [session-name]");
        }

        var plan = await PlanAsync(args[0]);
        var status = await orchestrator.GetStatusAsync(plan);
        if (status.State != WorkspaceState.Running)
        {
            await orchestrator.UpAsync(plan);
        }

        var name = args.Length == 2 ? args[1] : $"hstack-{plan.Project.Id}";
        if (!TmuxNamePattern.IsMatch(name))
        {
            throw new ArgumentException(
                "tmux session names may contain only letters, digits, '.', '_' and '-'.");
        }

        return await orchestrator.ExecAsync(
            new WorkspaceExecutionRequest(
                plan,
                ["tmux", "new-session", "-A", "-s", name],
                Interactive: true));
    }

    private async Task<int> InitAsync(string projectId)
    {
        var plan = await PlanAsync(projectId);
        var session = await sessions.EnsureAsync(plan);
        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Project", Markup.Escape(plan.Project.Id));
        table.AddRow("Herdr session", Markup.Escape(session.SessionName));
        table.AddRow("Herdr workspace", Markup.Escape(session.WorkspaceLabel));
        table.AddRow("Workspace id", Markup.Escape(session.WorkspaceId));
        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> StatusAsync(string projectId)
    {
        var plan = await PlanAsync(projectId);
        var session = await sessions.EnsureAsync(plan);
        AnsiConsole.MarkupLine(
            $"[green]Herdr ready[/] {Markup.Escape(session.SessionName)} / {Markup.Escape(session.WorkspaceLabel)}");
        var integrationStatus = await sessions.IntegrationStatusAsync(plan);
        Console.Write(integrationStatus.StandardOutput);
        return 0;
    }

    private async Task<int> PrintAsync(
        string projectId,
        Func<HerdrSessionService, WorkspaceDeploymentPlan, CancellationToken, Task<WorkspaceExecutionResult>> action)
    {
        var plan = await PlanAsync(projectId);
        var result = await action(sessions, plan, CancellationToken.None);
        Console.Write(result.StandardOutput);
        return result.ExitCode;
    }

    private async Task<int> StopAsync(string projectId)
    {
        var plan = await PlanAsync(projectId);
        return await sessions.StopAsync(plan);
    }

    private async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException(
                "Usage: hstack session run <agent> <project> --name <name> [-- <args>]");
        }

        var name = GetOption(args, "--name")
            ?? throw new ArgumentException("--name is required for a managed Herdr agent.");
        var plan = await PlanAsync(args[2]);
        var result = await sessions.RunAgentAsync(
            plan,
            args[1],
            name,
            Passthrough(args));
        Console.Write(result.StandardOutput);
        return result.ExitCode;
    }

    private async Task<WorkspaceDeploymentPlan> PlanAsync(string projectId)
    {
        var project = await projects.GetRequiredAsync(projectId);
        return await plans.BuildAsync(project);
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
