using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Aspire;
using HermesStack.Domain.Orchestration;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class AspireCliService(
    ProjectService projects,
    WorkspaceDeploymentPlanBuilder plans,
    AspireWorkspaceOrchestrator aspire)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] == "status")
        {
            return await StatusAsync(args.Length == 0 ? [] : args[1..]);
        }

        return args[0] switch
        {
            "doctor" => await DoctorAsync(),
            "dashboard" => await DashboardAsync(args[1..]),
            "inspect" => await InspectAsync(args[1..]),
            _ => throw new ArgumentException(
                "Usage: hstack aspire status [project] | doctor | dashboard <project> | inspect <project>")
        };
    }

    public async Task<int> DoctorAsync()
    {
        var availability = await aspire.DetectAsync();
        if (!availability.IsAvailable)
        {
            AnsiConsole.MarkupLine(
                $"[red]Aspire unavailable:[/] {Markup.Escape(availability.Reason ?? "unknown reason")}");
            return 1;
        }

        var result = await aspire.DoctorAsync();
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            AnsiConsole.WriteLine(result.StandardOutput.Trim());
        }

        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.StandardError))
        {
            AnsiConsole.MarkupLine(
                $"[red]{Markup.Escape(result.StandardError.Trim())}[/]");
        }

        return result.ExitCode;
    }

    private async Task<int> StatusAsync(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException(
                "Usage: hstack aspire status [project]");
        }

        var availability = await aspire.DetectAsync();
        var table = new Table()
            .AddColumn("Property")
            .AddColumn("Value");
        table.AddRow(
            "Aspire CLI",
            availability.IsAvailable
                ? $"ready ({Markup.Escape(availability.Version ?? "detected")})"
                : $"unavailable ({Markup.Escape(availability.Reason ?? "-")})");

        if (args.Length == 1)
        {
            var project = await projects.GetRequiredAsync(args[0]);
            var plan = await plans.BuildAsync(project, "aspire");
            var status = await aspire.GetStatusAsync(plan);
            table.AddRow("Project", Markup.Escape(project.Id));
            table.AddRow("Workspace", status.State.ToString());
            var dashboard = await aspire.GetDashboardUrlAsync(project.Id);
            table.AddRow(
                "Dashboard",
                Markup.Escape(dashboard ?? "not running"));
        }

        AnsiConsole.Write(table);
        return availability.IsAvailable ? 0 : 1;
    }

    private async Task<int> DashboardAsync(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException(
                "Usage: hstack aspire dashboard <project>");
        }

        var project = await projects.GetRequiredAsync(args[0]);
        var plan = await plans.BuildAsync(project, "aspire");
        var status = await aspire.GetStatusAsync(plan);
        if (status.State != WorkspaceState.Running)
        {
            throw new InvalidOperationException(
                $"Aspire workspace '{project.Id}' is not running.");
        }

        var dashboard = await aspire.GetDashboardUrlAsync(project.Id)
            ?? throw new InvalidOperationException(
                "Aspire Dashboard URL is unavailable.");
        AnsiConsole.MarkupLine(
            $"Aspire Dashboard: [link={Markup.Escape(dashboard)}]{Markup.Escape(dashboard)}[/]");
        AnsiConsole.MarkupLine(
            "[grey]Dashboard binding remains local; HermesStack does not expose it to the LAN.[/]");
        return 0;
    }

    private async Task<int> InspectAsync(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException(
                "Usage: hstack aspire inspect <project>");
        }

        var project = await projects.GetRequiredAsync(args[0]);
        var plan = await plans.BuildAsync(project, "aspire");
        var capabilities = await aspire.InspectCapabilitiesAsync(plan);
        var preview = await aspire.PreviewAsync(plan);
        var status = await aspire.GetStatusAsync(plan);

        var table = new Table()
            .AddColumn("Property")
            .AddColumn("Value");
        table.AddRow("Project", Markup.Escape(project.Id));
        table.AddRow("Security parity", capabilities.IsSupported ? "yes" : "no");
        table.AddRow("Workspace", status.State.ToString());
        table.AddRow(
            "Missing capabilities",
            Markup.Escape(
                capabilities.MissingCapabilities.Count == 0
                    ? "-"
                    : string.Join(", ", capabilities.MissingCapabilities)));
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine(preview.Summary);

        foreach (var note in capabilities.Notes)
        {
            AnsiConsole.MarkupLine(
                $"[grey]{Markup.Escape(note)}[/]");
        }

        return capabilities.IsSupported ? 0 : 1;
    }
}
