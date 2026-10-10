using System.ComponentModel;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;

namespace HermesStack.Docker.Execution;

// Keep Compose and Aspire as the single lifecycle owners. This provider
// represents the native Docker isolation boundary and rejects unsafe plans
// BEFORE calling either existing orchestrator.
public sealed class NativeContainerExecutionProvider(
    IWorkspaceOrchestratorRegistry orchestrators,
    IProcessRunner processRunner) : IExecutionEnvironmentProvider
{
    public string Id => "native-container";

    public async Task<ExecutionProviderAvailability> DetectAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await processRunner.RunAsync(
                new ProcessRequest("docker", ["version", "--format", "{{.Server.Version}}"]),
                cancellationToken);
            return result.IsSuccess
                ? new ExecutionProviderAvailability(true, result.StandardOutput.Trim())
                : new ExecutionProviderAvailability(false, Reason: result.StandardError.Trim());
        }
        catch (Exception error) when (error is Win32Exception or FileNotFoundException)
        {
            return new ExecutionProviderAvailability(false, Reason: error.Message);
        }
    }

    public Task<ExecutionEnvironmentPlan> PlanAsync(
        WorkspaceDeploymentPlan workspace,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(workspace);

        var capabilities = NativeContainerCapabilityPolicy.Evaluate(workspace);
        if (!capabilities.IsSupported)
        {
            throw new InvalidOperationException(
                "HS2301: Native container provider refuses this workspace: " +
                string.Join("; ", capabilities.MissingCapabilities));
        }

        // An unknown orchestrator never falls back to another implementation.
        _ = orchestrators.GetRequired(workspace.OrchestratorId);
        return Task.FromResult(new ExecutionEnvironmentPlan(Id, workspace, capabilities));
    }

    public async Task<ExecutionEnvironmentHandle> CreateAsync(
        ExecutionEnvironmentPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        RequireNative(plan.ProviderId);

        // Check again immediately before touching external resources, not only
        // when a plan was initially prepared.
        _ = await PlanAsync(plan.Workspace, cancellationToken);
        await orchestrators.GetRequired(plan.Workspace.OrchestratorId)
            .UpAsync(plan.Workspace, cancellationToken);

        return new ExecutionEnvironmentHandle(Id, plan.Workspace);
    }

    public Task DestroyAsync(
        ExecutionEnvironmentHandle environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        RequireNative(environment.ProviderId);

        // Deliberately do NOT reject teardown of legacy or now-invalid plans.
        // Stopping an existing workspace must remain possible during recovery.
        return orchestrators.GetRequired(environment.Workspace.OrchestratorId)
            .DownAsync(environment.Workspace, cancellationToken);
    }

    private void RequireNative(string requested)
    {
        if (!string.Equals(requested, Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"HS2302: Execution provider '{requested}' is not registered; " +
                $"only '{Id}' is currently supported.");
        }
    }
}

public static class NativeContainerCapabilityPolicy
{
    public static OrchestratorCapabilityReport Evaluate(WorkspaceDeploymentPlan workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var missing = new List<string>();
        var policy = workspace.Security;

        if (workspace.OrchestratorId is not ("compose" or "aspire"))
        {
            missing.Add("supported Compose/Aspire orchestrator");
        }

        if (!string.Equals(workspace.Project.StateScope, "isolated",
                StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("isolated project agent state");
        }

        if (policy.Privileged)
        {
            missing.Add("non-privileged container");
        }

        if (!policy.NoNewPrivileges)
        {
            missing.Add("no-new-privileges");
        }

        if (!policy.DropAllCapabilities)
        {
            missing.Add("drop all Linux capabilities");
        }

        if (!policy.ReadOnlyRoot)
        {
            missing.Add("read-only root filesystem");
        }

        if (policy.HostNetwork || policy.HostPid || policy.HostIpc)
        {
            missing.Add("no host namespace sharing");
        }

        if (workspace.Ports.Any(static port =>
                !string.Equals(port.Bind, "127.0.0.1", StringComparison.Ordinal)))
        {
            missing.Add("loopback-only published ports");
        }

        if (workspace.Mounts.Any(static mount =>
                IsDockerEndpoint(mount.Source) || IsDockerEndpoint(mount.Target)))
        {
            missing.Add("no Docker daemon mount or pipe");
        }

        if (workspace.Mounts.Any(static mount => IsHostRoot(mount.Source)))
        {
            missing.Add("no host filesystem root mount");
        }

        return missing.Count == 0
            ? OrchestratorCapabilityReport.Supported(
                "Native Docker uses the existing Compose/Aspire OCI lifecycle and security policy.",
                "No automatic substitution with Dagger/DevPod or remote providers.")
            : OrchestratorCapabilityReport.Unsupported(
                missing,
                "HS2301: All mandatory execution-provider security capabilities are required.");
    }

    private static bool IsDockerEndpoint(string value)
    {
        var normalized = value.Replace('\\', '/');
        return normalized.Contains("docker.sock", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("docker_engine", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHostRoot(string value)
    {
        if (value == "/")
        {
            return true;
        }

        var normalized = value.Replace('/', '\\');
        return normalized.Length == 3 &&
               char.IsLetter(normalized[0]) &&
               normalized[1] == ':' &&
               normalized[2] == '\\';
    }
}
