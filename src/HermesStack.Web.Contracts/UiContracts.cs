namespace HermesStack.Web.Contracts;

// Display-only DTOs: never carry agent credentials, Docker endpoints or
// contents of secret stores into the browser.
public sealed record PortInfo(int Container, int Host);
public sealed record ProjectInfo(
    string Id, string Name, string HostPath, string Orchestrator, string State,
    IReadOnlyList<PortInfo> Ports, string? Diagnostic);
public sealed record AgentInfo(string Id, string Name, bool Installed, string? Version);
public sealed record BackendInfo(string Id, bool Available, string? Version, string? Reason);
public sealed record DiagnosticsInfo(
    string AspireRequiredVersion, IReadOnlyList<BackendInfo> Backends);
public sealed record SessionInfo(string Csrf);
public sealed record CreateProjectRequest(string Id, string HostPath, string? Name);
public sealed record EditProjectRequest(string? HostPath, string? Name, string? Orchestrator);
public sealed record LifecycleRequest(string Action);
public sealed record PortRequest(int Container, int? Host);
public sealed record LogsInfo(string Text, int Tail);
public sealed record MemoryInfo(bool Enabled, string ProviderId);
