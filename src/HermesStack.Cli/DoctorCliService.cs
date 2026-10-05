using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using HermesStack.Application.Abstractions;
using HermesStack.Application.Network;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Application.Security;
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
    ISecretPolicyStore secretPolicies)
{
    public async Task<int> RunAsync(string[] args)
    {
        var projectId = args.FirstOrDefault(static value => !value.StartsWith("--", StringComparison.Ordinal));
        var networkOnly = args.Contains("--network", StringComparer.Ordinal);
        var certificatesOnly = args.Contains("--certificates", StringComparer.Ordinal);
        var securityOnly = args.Contains("--security", StringComparer.Ordinal);
        var targeted = networkOnly || certificatesOnly || securityOnly;
        var checks = new List<DoctorCheck>();

        if (!targeted)
        {
            checks.Add(new DoctorCheck(
                "HOST",
                "OS",
                DoctorStatus.Pass,
                $"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}"));

            var availability = await orchestrator.DetectAsync();
            checks.Add(new DoctorCheck(
                "HOST",
                "Docker Compose",
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
                await AddProjectChecksAsync(projectId, checks);
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
                await AddSecurityChecksAsync(projectId, checks);
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
                        await AddSecurityChecksAsync(project.Id, checks);
                    }
                }
            }
        }

        Render(checks);
        return checks.Any(static value => value.Status == DoctorStatus.Fail) ? 1 : 0;
    }

    private async Task AddProjectChecksAsync(
        string projectId,
        List<DoctorCheck> checks)
    {
        try
        {
            var project = await projects.GetRequiredAsync(projectId);
            var plan = await plans.BuildAsync(project);
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
        List<DoctorCheck> checks)
    {
        try
        {
            var project = await projects.GetRequiredAsync(projectId);
            var plan = await plans.BuildAsync(project);
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

    private static string FirstLine(params string[] values) =>
        values.SelectMany(static value => value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .FirstOrDefault()
        ?? "-";

    private static void Render(IReadOnlyList<DoctorCheck> checks)
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
                Markup.Escape(check.Details));
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
}
