using HermesStack.Domain.Projects;

namespace HermesStack.Domain.Orchestration;

public sealed record WorkspaceDeploymentPlan(
    ProjectDefinition Project,
    string OrchestratorId,
    string WorkspaceImage,
    string BaseComposeFile,
    string OverrideComposeFile,
    IReadOnlyList<WorkspaceMount> Mounts,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<ProjectPort> Ports,
    WorkspaceSecurityPolicy Security,
    string ProjectDataRoot);

public sealed record WorkspaceMount(string Source, string Target, bool ReadOnly, string Purpose);

public sealed record WorkspaceSecurityPolicy(
    bool Privileged = false,
    bool NoNewPrivileges = true,
    bool DropAllCapabilities = true,
    bool HostNetwork = false,
    bool HostPid = false,
    bool HostIpc = false,
    bool ReadOnlyRoot = true);

public sealed record OrchestratorAvailability(bool IsAvailable, string? Version = null, string? Reason = null);

public sealed record WorkspaceDeploymentPreview(string Backend, string Summary);

public sealed record WorkspaceExecutionRequest(
    WorkspaceDeploymentPlan Plan,
    IReadOnlyList<string> Command,
    bool Interactive = true);

public sealed record WorkspaceLogRequest(
    WorkspaceDeploymentPlan Plan,
    bool Follow = true,
    int? Tail = null);
