using HermesStack.Application.Abstractions;
using HermesStack.Application.Security;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;

namespace HermesStack.Application.Orchestration;

public sealed class WorkspaceDeploymentPlanBuilder(
    IDataRootProvider dataRoot,
    HostMountValidator mountValidator,
    ICertificateBundleService certificateBundleService,
    string baseComposeFile,
    string workspaceImage = "hstack/workspace-full:0.1.0") : IWorkspaceDeploymentPlanBuilder
{
    public async Task<WorkspaceDeploymentPlan> BuildAsync(ProjectDefinition project, string? orchestratorOverride = null, CancellationToken cancellationToken = default)
    {
        var validation = mountValidator.Validate(project.HostPath);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException($"{validation.Code}: {validation.Message}");
        }

        var projectDataRoot = dataRoot.GetProjectDataRoot(project.Id);
        var projectRuntimeRoot = dataRoot.GetProjectRuntimeRoot(project.Id);
        Directory.CreateDirectory(projectDataRoot);
        Directory.CreateDirectory(projectRuntimeRoot);
        Directory.CreateDirectory(Path.Combine(projectDataRoot, "home"));

        var mounts = new List<WorkspaceMount>
        {
            new(validation.NormalizedPath, "/workspace", false, "project"),
            new(Path.Combine(projectDataRoot, "home"), "/home/hstack", false, "workspace-home")
        };

        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = "/home/hstack",
            ["HSTACK_PROJECT_ID"] = project.Id,
            ["HSTACK_PROJECT_HOME"] = "/home/hstack"
        };

        var corporateBundle = await certificateBundleService.BuildCorporateBundleAsync(cancellationToken);
        if (corporateBundle is not null)
        {
            mounts.Add(new WorkspaceMount(corporateBundle, "/etc/hstack/certs/corporate-ca.crt", true, "corporate-ca"));
            environment["HSTACK_CORPORATE_CA_FILE"] = "/etc/hstack/certs/corporate-ca.crt";
            environment["SSL_CERT_FILE"] = "/home/hstack/.hstack/certs/ca-bundle.crt";
            environment["REQUESTS_CA_BUNDLE"] = "/home/hstack/.hstack/certs/ca-bundle.crt";
            environment["CURL_CA_BUNDLE"] = "/home/hstack/.hstack/certs/ca-bundle.crt";
            environment["GIT_SSL_CAINFO"] = "/home/hstack/.hstack/certs/ca-bundle.crt";
            environment["NODE_EXTRA_CA_CERTS"] = "/home/hstack/.hstack/certs/ca-bundle.crt";
        }

        foreach (var port in project.EffectivePorts)
        {
            if (!string.Equals(port.Bind, "127.0.0.1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"HS3008: Port {port.Container} must bind to 127.0.0.1 in M1.");
            }
        }

        var orchestrator = orchestratorOverride ?? project.Orchestrator ?? "compose";
        if (!string.Equals(orchestrator, "compose", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Orchestrator '{orchestrator}' is not implemented in M1.");
        }

        return new WorkspaceDeploymentPlan(
            project,
            orchestrator,
            workspaceImage,
            baseComposeFile,
            Path.Combine(projectRuntimeRoot, "compose.override.yaml"),
            mounts,
            environment,
            project.EffectivePorts,
            new WorkspaceSecurityPolicy(),
            projectDataRoot);
    }
}
