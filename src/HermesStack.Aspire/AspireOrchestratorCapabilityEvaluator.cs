using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;

namespace HermesStack.Aspire;

public sealed class AspireOrchestratorCapabilityEvaluator : IOrchestratorCapabilityEvaluator
{
    public string OrchestratorId => "aspire";

    public Task<OrchestratorCapabilityReport> EvaluateAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var missing = new List<string>();

        if (plan.Security.Privileged)
        {
            missing.Add("non-privileged container");
        }

        if (!plan.Security.NoNewPrivileges)
        {
            missing.Add("no-new-privileges");
        }

        if (!plan.Security.DropAllCapabilities)
        {
            missing.Add("drop all Linux capabilities");
        }

        if (plan.Security.HostNetwork || plan.Security.HostPid || plan.Security.HostIpc)
        {
            missing.Add("no host namespace sharing");
        }

        if (!plan.Security.ReadOnlyRoot)
        {
            missing.Add("read-only root filesystem");
        }

        if (plan.Ports.Any(static port =>
            !string.Equals(port.Bind, "127.0.0.1", StringComparison.Ordinal)))
        {
            missing.Add("loopback-only published ports");
        }

        if (plan.Mounts.Any(static mount =>
            mount.Source.Contains("docker.sock", StringComparison.OrdinalIgnoreCase) ||
            mount.Source.Contains("docker_engine", StringComparison.OrdinalIgnoreCase)))
        {
            missing.Add("no Docker daemon mount");
        }

        return Task.FromResult(
            missing.Count == 0
                ? OrchestratorCapabilityReport.Supported(
                    "Aspire 13.6 maps the validated plan to AddContainer/WithBindMount/WithEnvironment and OCI runtime security arguments.",
                    "Interactive in-container commands use the detected OCI runtime because Aspire 13.6 exposes lifecycle resource commands but no stable arbitrary exec command.")
                : OrchestratorCapabilityReport.Unsupported(
                    missing,
                    "Aspire backend refused the plan because a mandatory HermesStack security invariant cannot be preserved."));
    }
}
