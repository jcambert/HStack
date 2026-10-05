using HermesStack.Application.Abstractions;
using HermesStack.Domain.Agents;

namespace HermesStack.Application.Agents;

public sealed class OpenCodeHarness(IWorkspaceOrchestrator orchestrator) : AgentHarnessBase(orchestrator)
{
    public override string Id => "opencode";
    public override string DisplayName => "OpenCode";
    protected override string Executable => "opencode";
    protected override IReadOnlyList<string> AuthenticationArguments => ["auth", "login"];

    public override async Task ConfigureAsync(
        AgentConfigureRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureDirectoryAsync(StateDirectory(request.Plan, "opencode", "data"), cancellationToken);
        await WriteManagedFileAsync(
            Path.Combine(StateDirectory(request.Plan, "opencode", "config"), "opencode.json"),
            """
            {
              "$schema": "https://opencode.ai/config.json",
              "autoupdate": false
            }
            """,
            cancellationToken);
    }
}
