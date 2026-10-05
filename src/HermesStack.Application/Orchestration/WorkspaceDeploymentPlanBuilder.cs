using HermesStack.Application.Abstractions;
using HermesStack.Application.Network;
using HermesStack.Application.Security;
using HermesStack.Domain.Orchestration;
using HermesStack.Domain.Projects;

namespace HermesStack.Application.Orchestration;

public sealed class WorkspaceDeploymentPlanBuilder(
    IDataRootProvider dataRoot,
    HostMountValidator mountValidator,
    ICertificateBundleService certificateBundleService,
    string baseComposeFile,
    string workspaceImage = "hstack/workspace-full:0.5.0",
    IProxyConfigurationStore? proxyConfigurationStore = null) : IWorkspaceDeploymentPlanBuilder
{
    public async Task<WorkspaceDeploymentPlan> BuildAsync(
        ProjectDefinition project,
        string? orchestratorOverride = null,
        CancellationToken cancellationToken = default)
    {
        var validation = mountValidator.Validate(project.HostPath);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException($"{validation.Code}: {validation.Message}");
        }

        if (!string.Equals(project.StateScope, "isolated", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"State scope '{project.StateScope}' is not supported. Project agent state is isolated by default.");
        }

        var projectDataRoot = dataRoot.GetProjectDataRoot(project.Id);
        var projectRuntimeRoot = dataRoot.GetProjectRuntimeRoot(project.Id);

        var home = Path.Combine(projectDataRoot, "home");
        var claude = Path.Combine(projectDataRoot, "claude");
        var codex = Path.Combine(projectDataRoot, "codex");
        var hermes = Path.Combine(projectDataRoot, "hermes");
        var openCodeConfig = Path.Combine(projectDataRoot, "opencode", "config");
        var openCodeData = Path.Combine(projectDataRoot, "opencode", "data");

        foreach (var directory in new[]
        {
            projectDataRoot,
            projectRuntimeRoot,
            home,
            // Pre-create XDG parents in the project-owned HOME before Docker
            // attaches nested OpenCode binds. Otherwise Docker creates those
            // intermediate directories as root on the host bind, preventing
            // the non-root workspace user from creating ~/.local/state.
            Path.Combine(home, ".config"),
            Path.Combine(home, ".config", "herdr"),
            Path.Combine(home, ".local"),
            Path.Combine(home, ".local", "share"),
            Path.Combine(home, ".local", "state"),
            Path.Combine(home, ".cache"),
            claude,
            codex,
            hermes,
            openCodeConfig,
            openCodeData
        })
        {
            Directory.CreateDirectory(directory);
        }

        var mounts = new List<WorkspaceMount>
        {
            new(validation.NormalizedPath, "/workspace", false, "project"),
            new(home, "/home/hstack", false, "workspace-home"),
            new(claude, "/home/hstack/.claude", false, "agent-state:claude"),
            new(codex, "/home/hstack/.codex", false, "agent-state:codex"),
            new(hermes, "/home/hstack/.hermes", false, "agent-state:hermes"),
            new(openCodeConfig, "/home/hstack/.config/opencode", false, "agent-state:opencode-config"),
            new(openCodeData, "/home/hstack/.local/share/opencode", false, "agent-state:opencode-data")
        };

        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = "/home/hstack",
            ["HSTACK_PROJECT_ID"] = project.Id,
            ["HSTACK_PROJECT_HOME"] = "/home/hstack",
            ["HERDR_SESSION"] = $"hstack-{project.Id}",
            ["CLAUDE_CONFIG_DIR"] = "/home/hstack/.claude",
            ["CODEX_HOME"] = "/home/hstack/.codex",
            ["HERMES_HOME"] = "/home/hstack/.hermes",
            ["TERMINAL_ENV"] = "local",
            ["OPENCODE_DISABLE_AUTOUPDATE"] = "1",
            ["RTK_DB_PATH"] = "/tmp/hstack-rtk-tracking.db",
            ["RTK_TELEMETRY_DISABLED"] = "1",
            ["RTK_RECALL"] = "0"
        };

        if (proxyConfigurationStore is not null)
        {
            var proxy = ProxyConfigurationPolicy.ValidateAndNormalize(
                await proxyConfigurationStore.GetAsync(cancellationToken));
            if (proxy.Enabled)
            {
                if (!string.IsNullOrWhiteSpace(proxy.Http))
                {
                    environment["HTTP_PROXY"] = proxy.Http;
                    environment["http_proxy"] = proxy.Http;
                }

                if (!string.IsNullOrWhiteSpace(proxy.Https))
                {
                    environment["HTTPS_PROXY"] = proxy.Https;
                    environment["https_proxy"] = proxy.Https;
                }

                var noProxy = string.Join(",", proxy.EffectiveNoProxy);
                environment["NO_PROXY"] = noProxy;
                environment["no_proxy"] = noProxy;
            }
        }

        var corporateBundle = await certificateBundleService.BuildCorporateBundleAsync(cancellationToken);
        if (corporateBundle is not null)
        {
            mounts.Add(new WorkspaceMount(
                corporateBundle,
                "/etc/hstack/certs/corporate-ca.crt",
                true,
                "corporate-ca"));
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
                throw new InvalidOperationException(
                    $"HS3008: Port {port.Container} must bind to 127.0.0.1.");
            }
        }

        var orchestrator = orchestratorOverride ?? project.Orchestrator ?? "compose";
        if (!string.Equals(orchestrator, "compose", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Orchestrator '{orchestrator}' is not implemented in the current milestone.");
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
