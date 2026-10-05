using HermesStack.Application.Abstractions;
using HermesStack.Domain.Agents;

namespace HermesStack.Application.Agents;

public sealed class CodexHarness(IWorkspaceOrchestrator orchestrator) : AgentHarnessBase(orchestrator)
{
    public override string Id => "codex";
    public override string DisplayName => "Codex";
    protected override string Executable => "codex";
    protected override IReadOnlyList<string> AuthenticationArguments => ["login"];

    public override Task ConfigureAsync(
        AgentConfigureRequest request,
        CancellationToken cancellationToken = default) =>
        WriteManagedFileAsync(
            Path.Combine(StateDirectory(request.Plan, "codex"), "config.toml"),
            """
            # Managed defaults created by HermesStack.
            # Credentials remain inside this project's CODEX_HOME.
            cli_auth_credentials_store = "file"
            check_for_update_on_startup = false
            """,
            cancellationToken);
}
