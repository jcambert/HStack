using HermesStack.Application.Abstractions;
using HermesStack.Domain.Agents;

namespace HermesStack.Application.Agents;

public sealed class HermesAgentHarness(IWorkspaceOrchestrator orchestrator) : AgentHarnessBase(orchestrator)
{
    public override string Id => "hermes";
    public override string DisplayName => "Hermes Agent";
    protected override string Executable => "hermes";
    protected override IReadOnlyList<string> AuthenticationArguments => ["setup"];

    public override Task ConfigureAsync(
        AgentConfigureRequest request,
        CancellationToken cancellationToken = default) =>
        WriteManagedFileAsync(
            Path.Combine(StateDirectory(request.Plan, "hermes"), "config.yaml"),
            """
            # HermesStack workspace policy: the workspace container is already the sandbox.
            terminal:
              backend: local
            """,
            cancellationToken);
}
