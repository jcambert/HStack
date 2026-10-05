using HermesStack.Domain.Orchestration;

namespace HermesStack.Domain.Agents;

public enum AgentLaunchMode
{
    Run,
    Authenticate
}

public sealed record AgentInstallationInfo(
    string Id,
    string DisplayName,
    bool Installed,
    string? Version = null,
    string? Details = null);

public sealed record AgentLaunchRequest(
    WorkspaceDeploymentPlan Plan,
    AgentLaunchMode Mode = AgentLaunchMode.Run,
    IReadOnlyList<string>? Arguments = null,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    public IReadOnlyList<string> EffectiveArguments => Arguments ?? [];
    public IReadOnlyDictionary<string, string> EffectiveEnvironment =>
        Environment ?? new Dictionary<string, string>();
}

public sealed record AgentConfigureRequest(WorkspaceDeploymentPlan Plan);
