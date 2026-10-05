using HermesStack.Application.Abstractions;
using HermesStack.Application.Agents;
using HermesStack.Domain.Agents;
using HermesStack.Domain.Orchestration;

namespace HermesStack.UnitTests.Agents;

public sealed class AgentHarnessRegistryTests
{
    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var orchestrator = new RecordingOrchestrator();
        var first = new ClaudeCodeHarness(orchestrator);
        var second = new DuplicateClaudeHarness(orchestrator);

        Assert.Throws<InvalidOperationException>(
            () => new AgentHarnessRegistry([first, second]));
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        var harness = new ClaudeCodeHarness(new RecordingOrchestrator());
        var registry = new AgentHarnessRegistry([harness]);

        Assert.Same(harness, registry.GetRequired("CLAUDE"));
    }

    private sealed class DuplicateClaudeHarness(IWorkspaceOrchestrator orchestrator)
        : AgentHarnessBase(orchestrator)
    {
        public override string Id => "claude";
        public override string DisplayName => "duplicate";
        protected override string Executable => "duplicate";
        protected override IReadOnlyList<string> AuthenticationArguments => [];

        public override Task ConfigureAsync(
            AgentConfigureRequest request,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
