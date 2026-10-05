using HermesStack.Application.Abstractions;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Projects;
using HermesStack.Application.Security;
using HermesStack.Application.Network;
using HermesStack.Docker.Security;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class SecurityCliService(
    ProjectService projects,
    WorkspaceDeploymentPlanBuilder plans,
    DockerWorkspaceSecurityInspector inspector,
    SecurityInspectionService evaluator,
    ISecretPolicyStore secretPolicies,
    ITokenOptimizationStore tokenStore,
    IProxyConfigurationStore proxyStore)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 2 || args[0] != "inspect")
        {
            throw new ArgumentException(
                "Usage: hstack security inspect <project>");
        }

        var project = await projects.GetRequiredAsync(args[1]);
        var plan = await plans.BuildAsync(project);
        var snapshot = await inspector.InspectAsync(plan);
        var policies = await secretPolicies.ListAsync(project.Id);
        snapshot = snapshot with
        {
            SecretNames = policies
                .Select(static policy => policy.Name)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray()
        };
        var result = evaluator.Evaluate(snapshot);
        var tokenConfiguration = await tokenStore.GetAsync(project.Id);
        var proxy = ProxyConfigurationPolicy.ValidateAndNormalize(await proxyStore.GetAsync());
        var tokenHooks = tokenConfiguration.EffectiveProviders.Count == 0
            ? "-"
            : string.Join(
                "; ",
                tokenConfiguration.EffectiveProviders.Select(value =>
                    $"{value.ProviderId}[{string.Join(",", value.Agents)}]"));
        var proxyEndpoints = proxy.Enabled
            ? string.Join(
                ", ",
                new[] { proxy.Http, proxy.Https }
                    .Where(static value => !string.IsNullOrWhiteSpace(value)))
            : "-";

        var summary = new Table().AddColumn("Property").AddColumn("Value");
        summary.AddRow("Security score", result.Score.ToString());
        summary.AddRow("Inspection", snapshot.Live ? "live Docker" : "declared plan");
        summary.AddRow("User", Markup.Escape(snapshot.User));
        summary.AddRow("Privileged", snapshot.Privileged ? "yes" : "no");
        summary.AddRow("Docker socket", snapshot.DockerSocket ? "present" : "absent");
        summary.AddRow("Read-only root", snapshot.ReadOnlyRoot ? "yes" : "no");
        summary.AddRow("PID mode", Markup.Escape(Empty(snapshot.PidMode)));
        summary.AddRow("IPC mode", Markup.Escape(Empty(snapshot.IpcMode)));
        summary.AddRow("Networks", Markup.Escape(List(snapshot.Networks)));
        summary.AddRow("Capabilities +", Markup.Escape(List(snapshot.CapabilitiesAdded)));
        summary.AddRow("Capabilities -", Markup.Escape(List(snapshot.CapabilitiesDropped)));
        summary.AddRow("Security options", Markup.Escape(List(snapshot.SecurityOptions)));
        summary.AddRow("Devices", Markup.Escape(List(snapshot.DeviceMounts)));
        summary.AddRow("Published ports", Markup.Escape(List(snapshot.PublishedPorts)));
        summary.AddRow("Environment", Markup.Escape(List(snapshot.EnvironmentVariables)));
        summary.AddRow("Secrets", Markup.Escape(List(snapshot.SecretNames)));
        summary.AddRow("Token optimizer hooks", Markup.Escape(tokenHooks));
        summary.AddRow("Proxy endpoints", Markup.Escape(proxyEndpoints));
        summary.AddRow(
            "Unexpected prompt/content logging",
            tokenConfiguration.EffectiveProviders.Any(value =>
                string.Equals(value.ProviderId, "rtk", StringComparison.OrdinalIgnoreCase))
                ? "blocked: RTK recall disabled; tracking DB on /tmp tmpfs"
                : "none configured");
        AnsiConsole.Write(summary);

        var mounts = new Table()
            .AddColumn("Host mounts")
            .AddColumn("Writable");
        var count = Math.Max(snapshot.HostMounts.Count, snapshot.WritableMounts.Count);
        for (var index = 0; index < count; index++)
        {
            mounts.AddRow(
                index < snapshot.HostMounts.Count
                    ? Markup.Escape(snapshot.HostMounts[index])
                    : string.Empty,
                index < snapshot.WritableMounts.Count
                    ? Markup.Escape(snapshot.WritableMounts[index])
                    : string.Empty);
        }

        AnsiConsole.Write(mounts);

        foreach (var finding in result.Findings)
        {
            var color = finding.Severity switch
            {
                "Critical" => "red",
                "D" or "C" => "yellow",
                "B" => "yellow",
                _ => "green"
            };
            AnsiConsole.MarkupLine(
                $"[{color}]{Markup.Escape(finding.Severity)} {Markup.Escape(finding.Code)}[/] " +
                Markup.Escape(finding.Message));
        }

        return result.Score == HermesStack.Domain.Security.SecurityScore.Critical ? 3 : 0;
    }

    private static string List(IReadOnlyList<string> values) =>
        values.Count == 0 ? "-" : string.Join(", ", values);

    private static string Empty(string value) =>
        string.IsNullOrWhiteSpace(value) ? "default" : value;
}
