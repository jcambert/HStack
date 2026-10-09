using HermesStack.Application.Abstractions;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Domain.Orchestration;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class OrchestratorCliService(
    ProjectService projects,
    WorkspaceDeploymentPlanBuilder plans,
    IWorkspaceOrchestratorRegistry registry,
    IWorkspaceOrchestrator routed,
    IOrchestrationConfigurationStore configurationStore)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "status")
        {
            return await StatusAsync(args.Length == 0 ? [] : args[1..]);
        }

        return args[0] switch
        {
            "list" => await ListAsync(),
            "set" => await SetAsync(args[1..]),
            _ => throw new ArgumentException(
                "Usage: hstack orchestrator list|status|set <compose|aspire> [--project <project>]")
        };
    }

    public async Task<int> PlanAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack plan <project> [--orchestrator compose|aspire]");
        }

        var project = await projects.GetRequiredAsync(args[0]);
        var requested = GetOption(args, "--orchestrator");
        var plan = await plans.BuildAsync(project, requested);
        var backend = registry.GetRequired(plan.OrchestratorId);
        var availability = await backend.DetectAsync();
        if (!availability.IsAvailable)
        {
            throw new InvalidOperationException(
                $"HS2110: Orchestrator '{plan.OrchestratorId}' is unavailable: {availability.Reason}");
        }

        var preview = await backend.PreviewAsync(plan);
        var summary = new Table()
            .AddColumn("Property")
            .AddColumn("Value");
        summary.AddRow("Project", Markup.Escape(plan.Project.Id));
        summary.AddRow("Orchestrator", Markup.Escape(plan.OrchestratorId));
        summary.AddRow("Image", Markup.Escape(plan.WorkspaceImage));
        summary.AddRow("Mounts", plan.Mounts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        summary.AddRow("Ports", plan.Ports.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        summary.AddRow(
            "Environment keys",
            Markup.Escape(string.Join(
                ",",
                plan.Environment.Keys.OrderBy(static value => value, StringComparer.Ordinal))));
        summary.AddRow(
            "Security",
            "non-privileged; no-new-privileges; cap-drop=ALL; read-only-root");
        AnsiConsole.Write(summary);

        var mounts = new Table()
            .AddColumn("Source")
            .AddColumn("Target")
            .AddColumn("Mode")
            .AddColumn("Purpose");
        foreach (var mount in plan.Mounts)
        {
            mounts.AddRow(
                Markup.Escape(mount.Source),
                Markup.Escape(mount.Target),
                mount.ReadOnly ? "ro" : "rw",
                Markup.Escape(mount.Purpose));
        }

        AnsiConsole.Write(mounts);
        AnsiConsole.MarkupLine(
            $"[bold]{Markup.Escape(preview.Backend)} rendering[/]");
        AnsiConsole.WriteLine(preview.Summary);
        return 0;
    }

    private async Task<int> ListAsync()
    {
        var configuration = await configurationStore.GetOrchestrationAsync();
        var table = new Table()
            .AddColumn("Id")
            .AddColumn("Backend")
            .AddColumn("Enabled")
            .AddColumn("Available")
            .AddColumn("Version / details");

        foreach (var item in registry.All)
        {
            var availability = await item.DetectAsync();
            table.AddRow(
                Markup.Escape(item.Id),
                Markup.Escape(item.DisplayName),
                configuration.IsEnabled(item.Id) ? "yes" : "no",
                availability.IsAvailable ? "[green]yes[/]" : "[yellow]no[/]",
                Markup.Escape(
                    availability.Version ??
                    availability.Reason ??
                    "-"));
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> StatusAsync(string[] args)
    {
        var projectId = GetOption(args, "--project");
        var configuration = await configurationStore.GetOrchestrationAsync();
        var table = new Table()
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow(
            "Global default",
            Markup.Escape(configuration.DefaultOrchestrator));
        table.AddRow(
            "Compose",
            configuration.ComposeEnabled ? "enabled" : "disabled");
        table.AddRow(
            "Aspire",
            configuration.AspireEnabled ? "enabled" : "disabled");
        table.AddRow(
            "Aspire Dashboard",
            configuration.EffectiveAspireDashboard.Enabled
                ? "enabled; localhost only"
                : "disabled");

        if (projectId is not null)
        {
            var project = await projects.GetRequiredAsync(projectId);
            var plan = await plans.BuildAsync(project);
            table.AddRow("Project", Markup.Escape(project.Id));
            table.AddRow(
                "Project override",
                Markup.Escape(project.Orchestrator ?? "-"));
            table.AddRow(
                "Effective",
                Markup.Escape(plan.OrchestratorId));

            var status = await routed.GetStatusAsync(plan);
            table.AddRow("Workspace", status.State.ToString());
            if (!string.IsNullOrWhiteSpace(status.Details))
            {
                table.AddRow("Details", Markup.Escape(status.Details));
            }
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<int> SetAsync(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "Usage: hstack orchestrator set <compose|aspire> [--project <project>]");
        }

        var targetId = args[0].Trim().ToLowerInvariant();
        var target = registry.GetRequired(targetId);
        var availability = await target.DetectAsync();
        if (!availability.IsAvailable)
        {
            throw new InvalidOperationException(
                $"HS2110: Orchestrator '{targetId}' is unavailable: {availability.Reason}");
        }

        var projectId = GetOption(args, "--project");
        var originalConfiguration =
            await configurationStore.GetOrchestrationAsync();
        var enabledConfiguration = Enable(
            originalConfiguration,
            targetId);

        if (projectId is null)
        {
            foreach (var project in await projects.ListAsync())
            {
                var currentPlan = await plans.BuildAsync(project);
                var state = await routed.GetStatusAsync(currentPlan);
                if (state.State == WorkspaceState.Running)
                {
                    throw new InvalidOperationException(
                        $"HS2111: Stop project '{project.Id}' before changing the global orchestrator.");
                }
            }

            await configurationStore.SaveOrchestrationAsync(
                enabledConfiguration with
                {
                    DefaultOrchestrator = targetId
                });
            AnsiConsole.MarkupLine(
                $"[green]✓[/] Default orchestrator set to {Markup.Escape(targetId)}.");
            return 0;
        }

        var selected = await projects.GetRequiredAsync(projectId);
        var current = await plans.BuildAsync(selected);
        var status = await routed.GetStatusAsync(current);
        if (status.State == WorkspaceState.Running)
        {
            throw new InvalidOperationException(
                $"HS2111: Stop project '{selected.Id}' before switching orchestrators.");
        }

        var configurationChanged = enabledConfiguration != originalConfiguration;
        if (configurationChanged)
        {
            await configurationStore.SaveOrchestrationAsync(
                enabledConfiguration);
        }

        try
        {
            var targetPlan = await plans.BuildAsync(selected, targetId);
            _ = await target.PreviewAsync(targetPlan);
            var updated = await projects.SetOrchestratorAsync(
                selected.Id,
                targetId);
            AnsiConsole.MarkupLine(
                $"[green]✓[/] {Markup.Escape(updated.Id)} will use {Markup.Escape(targetId)}.");
            return 0;
        }
        catch
        {
            if (configurationChanged)
            {
                await configurationStore.SaveOrchestrationAsync(
                    originalConfiguration);
            }

            throw;
        }
    }

    private static OrchestrationConfiguration Enable(
        OrchestrationConfiguration configuration,
        string target) =>
        target switch
        {
            "compose" => configuration with { ComposeEnabled = true },
            "aspire" => configuration with { AspireEnabled = true },
            _ => configuration
        };

    private static string? GetOption(
        string[] args,
        string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }
}
