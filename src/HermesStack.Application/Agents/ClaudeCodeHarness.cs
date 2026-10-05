using HermesStack.Application.Abstractions;
using HermesStack.Domain.Agents;

namespace HermesStack.Application.Agents;

public sealed class ClaudeCodeHarness(IWorkspaceOrchestrator orchestrator) : AgentHarnessBase(orchestrator)
{
    public override string Id => "claude";
    public override string DisplayName => "Claude Code";
    protected override string Executable => "claude";
    protected override IReadOnlyList<string> AuthenticationArguments => ["auth", "login"];

    public override Task ConfigureAsync(
        AgentConfigureRequest request,
        CancellationToken cancellationToken = default) =>
        EnsureDirectoryAsync(StateDirectory(request.Plan, "claude"), cancellationToken);
}
