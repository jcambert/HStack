namespace HermesStack.Domain.Projects;

public sealed record ProjectDefinition(
    string Id,
    string Name,
    string HostPath,
    string ContainerPath = "/workspace",
    string Access = "read-write",
    string? Orchestrator = null,
    WorkspaceResources? Resources = null,
    IReadOnlyList<ProjectPort>? Ports = null)
{
    public WorkspaceResources EffectiveResources => Resources ?? new WorkspaceResources();
    public IReadOnlyList<ProjectPort> EffectivePorts => Ports ?? [];
}

public sealed record WorkspaceResources(double Cpus = 4, string Memory = "8g", int Pids = 512);

public sealed record ProjectPort(int Container, int? Host = null, string Bind = "127.0.0.1")
{
    public int EffectiveHost => Host ?? Container;
}
