using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using HermesStack.Application.Abstractions;
using HermesStack.Application.Network;
using HermesStack.Application.Context;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Application.Security;
using HermesStack.Application.Tokens;
using HermesStack.Domain.Orchestration;
using HermesStack.Docker.Security;
using HermesStack.Infrastructure.Certificates;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class DoctorCliService(
    ProjectService projects,
    WorkspaceDeploymentPlanBuilder plans,
    IWorkspaceOrchestrator orchestrator,
    IAgentHarnessRegistry agents,
    IProxyConfigurationStore proxyStore,
    CertificateBundleService certificates,
    DockerWorkspaceSecurityInspector securityInspector,
    SecurityInspectionService securityEvaluator,
    ISecretPolicyStore secretPolicies,
    TokenOptimizationService tokenOptimization,
    ContextService contextService,
    ISecretRedactor redactor,
    IWorkspaceOrchestratorRegistry orchestratorRegistry)
{
    public async Task<int> RunAsync(string[] args)
    {
        var orchestratorOverride = GetOption(args, "--orchestrator");
        var projectId = ProjectArgument(args);
        var networkOnly = args.Contains("--network", StringComparer.Ordinal);
        var certificatesOnly = args.Contains("--certificates", StringComparer.Ordinal);
        var securityOnly = args.Contains("--security", StringComparer.Ordinal);
        var tokensOnly = args.Contains("--tokens", StringComparer.Ordinal);
        var memoryOnly = args.Contains("--memory", StringComparer.Ordinal);
        var targeted = networkOnly || certificatesOnly || securityOnly || tokensOnly || memoryOnly;
        var checks = new List<DoctorCheck>();

        if (!targeted)
        {
            checks.Add(new DoctorCheck(
                "HOST",
                "OS",
                DoctorStatus.Pass,
                $"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}"));

            var selectedOrchestrator = orchestratorOverride is null
                ? orchestrator
                : orchestratorRegistry.GetRequired(orchestratorOverride);
            var availability = await selectedOrchestrator.DetectAsync();
            checks.Add(new DoctorCheck(
                "HOST",
                orchestratorOverride is null
                    ? "Workspace orchestrators"
                    : $"Orchestrator {orchestratorOverride}",
                availability.IsAvailable ? DoctorStatus.Pass : DoctorStatus.Fail,
                availability.Version ?? availability.Reason ?? "unavailable"));

            var projectList = await projects.ListAsync();
            checks.Add(new DoctorCheck(
                "CONFIGURATION",
                "Projects",
                DoctorStatus.Pass,
                $"{projectList.Count} registered"));

            if (projectId is not null)
            {
                await AddProjectChecksAsync(projectId, checks, orchestratorOverride);
            }
        }

        if (!targeted || networkOnly)
        {
            await AddNetworkChecksAsync(checks);
        }

        if (!targeted || certificatesOnly)
        {
            await AddCertificateChecksAsync(checks);
        }

        if (!targeted || securityOnly)
        {
            if (projectId is not null)
            {
                await AddSecurityChecksAsync(projectId, checks, orchestratorOverride);
            }
            else if (securityOnly)
            {
                var projectList = await projects.ListAsync();
                if (projectList.Count == 0)
                {
                    checks.Add(new DoctorCheck(
                        "SECURITY",
                        "Projects",
                        DoctorStatus.Warn,
                        "No projects registered."));
                }
                else
                {
                    foreach (var project in projectList)
                    {
                        await AddSecurityChecksAsync(project.Id, checks, orchestratorOverride);
                    }
                }
            }
        }

        if (!targeted || tokensOnly)
        {
            if (projectId is not null)
            {
                await AddTokenChecksAsync(projectId, checks, orchestratorOverride);
            }
            else if (tokensOnly)
            {
                var projectList = await projects.ListAsync();
                if (projectList.Count == 0)
                {
                    checks.Add(new DoctorCheck(
                        "TOKENS",
                        "Projects",
                        DoctorStatus.Warn,
                        "No projects registered."));
                }
                else
                {
                    foreach (var project in projectList)
                    {
                        await AddTokenChecksAsync(project.Id, checks, orchestratorOverride);
                    }
                }
            }
        }


        if (!targeted || memoryOnly)
        {
            await AddMemoryChecksAsync(projectId, checks);
        }

        Render(checks, redactor);
        return checks.Any(static value => value.Status == DoctorStatus.Fail) ? 1 : 0;
    }

    private async Task AddProjectChecksAsync(
        string projectId,
        List<DoctorCheck> checks,
        string? orchestratorOverride)
    {
        try
        {
            var project = await projects.GetRequiredAsync(projectId);
            var plan = await plans.BuildAsync(project, orchestratorOverride);
            checks.Add(new DoctorCheck(
                "PROJECT",
                "Mount policy",
                DoctorStatus.Pass,
                $"{project.HostPath} -> /workspace"));

            var status = await orchestrator.GetStatusAsync(plan);
            checks.Add(new DoctorCheck(
                "WORKSPACE",
                "State",
                status.State == WorkspaceState.Unknown ? DoctorStatus.Fail : DoctorStatus.Pass,
                status.State.ToString()));

            var policies = await secretPolicies.ListAsync(project.Id);
            checks.Add(new DoctorCheck(
                "AUTHENTICATION",
                "Secret policies",
                DoctorStatus.Pass,
                $"{policies.Count} project-scoped allow-list entries"));

            if (status.State == WorkspaceState.Running)
            {
                foreach (var harness in agents.All.OrderBy(static value => value.Id, StringComparer.Ordinal))
                {
                    var info = await harness.InspectAsync(plan);
                    checks.Add(new DoctorCheck(
                        "AGENTS",
                        harness.DisplayName,
                        info.Installed ? DoctorStatus.Pass : DoctorStatus.Fail,
                        info.Version ?? info.Details ?? "not detected"));
                }

                var herdr = await orchestrator.ExecCaptureAsync(
                    new WorkspaceExecutionRequest(
                        plan,
                        ["herdr", "--version"],
                        Interactive: false));
                checks.Add(new DoctorCheck(
                    "SESSIONS",
                    "Herdr",
                    herdr.IsSuccess ? DoctorStatus.Pass : DoctorStatus.Fail,
                    FirstLine(herdr.StandardOutput, herdr.StandardError)));

                var tmux = await orchestrator.ExecCaptureAsync(
                    new WorkspaceExecutionRequest(
                        plan,
                        ["tmux", "-V"],
                        Interactive: false));
                checks.Add(new DoctorCheck(
                    "SESSIONS",
                    "tmux",
                    tmux.IsSuccess ? DoctorStatus.Pass : DoctorStatus.Fail,
                    FirstLine(tmux.StandardOutput, tmux.StandardError)));
            }
            else
            {
                checks.Add(new DoctorCheck(
                    "AGENTS",
                    "Runtime checks",
                    DoctorStatus.Warn,
                    "Workspace is not running."));
            }
        }
        catch (Exception exception)
        {
            checks.Add(new DoctorCheck(
                "PROJECT",
                projectId,
                DoctorStatus.Fail,
                exception.Message));
        }
    }

    private async Task AddNetworkChecksAsync(List<DoctorCheck> checks)
    {
        try
        {
            var proxy = ProxyConfigurationPolicy.ValidateAndNormalize(
                await proxyStore.GetAsync());
            checks.Add(new DoctorCheck(
                "NETWORK",
                "Proxy",
                DoctorStatus.Pass,
                proxy.Enabled
                    ? $"enabled ({proxy.Https ?? proxy.Http}); NO_PROXY={string.Join(",", proxy.EffectiveNoProxy)}"
                    : "disabled"));

            var addresses = await Dns.GetHostAddressesAsync("github.com");
            checks.Add(new DoctorCheck(
                "NETWORK",
                "DNS",
                addresses.Length > 0 ? DoctorStatus.Pass : DoctorStatus.Fail,
                addresses.Length > 0 ? addresses[0].ToString() : "no addresses"));

            using var handler = new HttpClientHandler();
            if (proxy.Enabled)
            {
                var endpoint = proxy.Https ?? proxy.Http;
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    handler.Proxy = new WebProxy(endpoint);
                    handler.UseProxy = true;
                }
            }

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            using var response = await client.GetAsync(
                "https://github.com/",
                HttpCompletionOption.ResponseHeadersRead);
            checks.Add(new DoctorCheck(
                "NETWORK",
                "HTTPS/TLS",
                (int)response.StatusCode < 500 ? DoctorStatus.Pass : DoctorStatus.Fail,
                $"github.com -> {(int)response.StatusCode}"));
        }
        catch (Exception exception)
        {
            checks.Add(new DoctorCheck(
                "NETWORK",
                "Connectivity",
                DoctorStatus.Fail,
                exception.Message));
        }
    }

    private async Task AddCertificateChecksAsync(List<DoctorCheck> checks)
    {
        try
        {
            var bundle = await certificates.BuildCorporateBundleAsync();
            checks.Add(new DoctorCheck(
                "CERTIFICATES",
                "Corporate CA",
                DoctorStatus.Pass,
                bundle is null
                    ? "No additional corporate CA configured; system/public trust remains active."
                    : bundle));
        }
        catch (Exception exception)
        {
            checks.Add(new DoctorCheck(
                "CERTIFICATES",
                "Corporate CA",
                DoctorStatus.Fail,
                exception.Message));
        }
    }

    private async Task AddSecurityChecksAsync(
        string projectId,
        List<DoctorCheck> checks,
        string? orchestratorOverride)
    {
        try
        {
            var project = await projects.GetRequiredAsync(projectId);
            var plan = await plans.BuildAsync(project, orchestratorOverride);
            var snapshot = await securityInspector.InspectAsync(plan);
            var policies = await secretPolicies.ListAsync(project.Id);
            snapshot = snapshot with
            {
                SecretNames = policies.Select(static value => value.Name).ToArray()
            };
            var result = securityEvaluator.Evaluate(snapshot);
            checks.Add(new DoctorCheck(
                "SECURITY",
                project.Id,
                result.Score is HermesStack.Domain.Security.SecurityScore.A or
                    HermesStack.Domain.Security.SecurityScore.B
                    ? DoctorStatus.Pass
                    : DoctorStatus.Fail,
                $"score {result.Score}; docker.sock={(snapshot.DockerSocket ? "present" : "absent")}; " +
                $"privileged={(snapshot.Privileged ? "yes" : "no")}; user={snapshot.User}"));
        }
        catch (Exception exception)
        {
            checks.Add(new DoctorCheck(
                "SECURITY",
                projectId,
                DoctorStatus.Fail,
                exception.Message));
        }
    }

    private async Task AddTokenChecksAsync(
        string projectId,
        List<DoctorCheck> checks,
        string? orchestratorOverride)
    {
        try
        {
            var project = await projects.GetRequiredAsync(projectId);
            var plan = await plans.BuildAsync(project, orchestratorOverride);
            var configuration = await tokenOptimization.GetAsync(project.Id);
            checks.Add(new DoctorCheck(
                "TOKENS",
                $"{project.Id} policy",
                DoctorStatus.Pass,
                configuration.Enabled
                    ? $"{configuration.Profile.ToString().ToLowerInvariant()}: " +
                      string.Join(",", configuration.EffectiveProviders.Select(static value => value.ProviderId))
                    : "disabled"));

            var status = await orchestrator.GetStatusAsync(plan);
            if (status.State != WorkspaceState.Running)
            {
                checks.Add(new DoctorCheck(
                    "TOKENS",
                    $"{project.Id} runtime",
                    DoctorStatus.Warn,
                    "Workspace is not running; runtime optimizer health was not probed."));
                return;
            }

            var health = await tokenOptimization.DoctorAsync(plan);
            foreach (var item in health)
            {
                checks.Add(new DoctorCheck(
                    "TOKENS",
                    item.DisplayName,
                    item.Available ? DoctorStatus.Pass : DoctorStatus.Fail,
                    $"{item.Version}: {item.Details}"));
            }
        }
        catch (Exception exception)
        {
            checks.Add(new DoctorCheck(
                "TOKENS",
                projectId,
                DoctorStatus.Fail,
                exception.Message));
        }
    }

    private async Task AddMemoryChecksAsync(
        string? projectId,
        List<DoctorCheck> checks)
    {
        try
        {
            var results = await contextService.DoctorAsync(projectId);
            foreach (var item in results)
            {
                checks.Add(new DoctorCheck(
                    "MEMORY",
                    projectId is null ? "OpenViking" : $"{projectId} / OpenViking",
                    item.IsAvailable ? DoctorStatus.Pass : DoctorStatus.Warn,
                    $"{item.Version ?? "-"}: {item.Details ?? "no details"}"));
            }

            if (projectId is not null)
            {
                var configuration = await contextService.GetConfigurationAsync(projectId);
                checks.Add(new DoctorCheck(
                    "MEMORY",
                    $"{projectId} policy",
                    DoctorStatus.Pass,
                    configuration.Enabled
                        ? $"provider={configuration.ProviderId}; capture={configuration.CaptureMode.ToString().ToLowerInvariant()}; budget={configuration.EffectiveBudget.MaxTokens}"
                        : "disabled"));
            }
        }
        catch (Exception exception)
        {
            checks.Add(new DoctorCheck(
                "MEMORY",
                projectId ?? "OpenViking",
                DoctorStatus.Fail,
                exception.Message));
        }
    }

    private static string FirstLine(params string[] values) =>
        values.SelectMany(static value => value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .FirstOrDefault()
        ?? "-";

    private static void Render(
        IReadOnlyList<DoctorCheck> checks,
        ISecretRedactor redactor)
    {
        var table = new Table()
            .AddColumn("Section")
            .AddColumn("Check")
            .AddColumn("Status")
            .AddColumn("Details");

        foreach (var check in checks)
        {
            var status = check.Status switch
            {
                DoctorStatus.Pass => "[green]✓ PASS[/]",
                DoctorStatus.Warn => "[yellow]! WARN[/]",
                _ => "[red]✗ FAIL[/]"
            };
            table.AddRow(
                Markup.Escape(check.Section),
                Markup.Escape(check.Name),
                status,
                Markup.Escape(redactor.Redact(check.Details)));
        }

        AnsiConsole.Write(table);
    }

    private sealed record DoctorCheck(
        string Section,
        string Name,
        DoctorStatus Status,
        string Details);

    private enum DoctorStatus
    {
        Pass,
        Warn,
        Fail
    }
    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }

    private static string? ProjectArgument(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] is "--orchestrator")
            {
                index++;
                continue;
            }

            if (!args[index].StartsWith("--", StringComparison.Ordinal))
            {
                return args[index];
            }
        }

        return null;
    }

}
