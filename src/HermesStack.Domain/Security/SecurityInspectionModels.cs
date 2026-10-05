namespace HermesStack.Domain.Security;

public enum SecurityScore
{
    A,
    B,
    C,
    D,
    Critical
}

public sealed record WorkspaceSecuritySnapshot(
    IReadOnlyList<string> HostMounts,
    IReadOnlyList<string> WritableMounts,
    IReadOnlyList<string> PublishedPorts,
    IReadOnlyList<string> EnvironmentVariables,
    IReadOnlyList<string> SecretNames,
    string User,
    IReadOnlyList<string> CapabilitiesAdded,
    IReadOnlyList<string> CapabilitiesDropped,
    IReadOnlyList<string> SecurityOptions,
    IReadOnlyList<string> Networks,
    bool Privileged,
    bool DockerSocket,
    IReadOnlyList<string> DeviceMounts,
    string PidMode,
    string IpcMode,
    bool HostRootMount,
    bool ReadOnlyRoot,
    bool Live);

public sealed record SecurityFinding(
    string Code,
    string Severity,
    string Message);

public sealed record WorkspaceSecurityInspection(
    SecurityScore Score,
    WorkspaceSecuritySnapshot Snapshot,
    IReadOnlyList<SecurityFinding> Findings);
