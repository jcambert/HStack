using HermesStack.Application.Security;
using HermesStack.Domain.Security;

namespace HermesStack.UnitTests.Security;

public sealed class SecurityInspectionServiceTests
{
    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Critical_invariants_force_critical_score(
        bool dockerSocket,
        bool privileged,
        bool hostNetwork,
        bool hostRoot)
    {
        var networks = hostNetwork ? new[] { "host" } : new[] { "workspace" };
        var snapshot = CreateSnapshot(
            dockerSocket,
            privileged,
            networks,
            hostRoot);

        var result = new SecurityInspectionService().Evaluate(snapshot);

        Assert.Equal(SecurityScore.Critical, result.Score);
    }

    [Fact]
    public void Hardened_workspace_scores_A()
    {
        var result = new SecurityInspectionService().Evaluate(
            CreateSnapshot(false, false, ["workspace"], false));

        Assert.Equal(SecurityScore.A, result.Score);
    }

    private static WorkspaceSecuritySnapshot CreateSnapshot(
        bool dockerSocket,
        bool privileged,
        IReadOnlyList<string> networks,
        bool hostRoot) => new(
        ["/tmp/project -> /workspace (rw)"],
        ["/tmp/project -> /workspace"],
        [],
        ["HOME"],
        [],
        "hstack",
        [],
        ["ALL"],
        ["no-new-privileges:true"],
        networks,
        privileged,
        dockerSocket,
        [],
        string.Empty,
        string.Empty,
        hostRoot,
        true,
        true);
}
