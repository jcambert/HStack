using HermesStack.Application.Context;
using HermesStack.Domain.Context;

namespace HermesStack.UnitTests.Context;

public sealed class ContextScopePolicyTests
{
    private readonly ContextScopePolicy _policy = new();

    [Fact]
    public void Global_scope_is_denied()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _policy.ValidateQuery(new ContextQuery("a", "q", ContextScope.Global)));
    }

    [Fact]
    public void Agent_scope_requires_agent_identity()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _policy.ValidateWrite(new ContextWriteRequest(
                "a",
                ContextScope.Agent,
                "note",
                "content")));
    }

    [Fact]
    public void Shared_write_requires_namespace()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _policy.ValidateWrite(new ContextWriteRequest(
                "a",
                ContextScope.Shared,
                "note",
                "content")));

        _policy.ValidateWrite(new ContextWriteRequest(
            "a",
            ContextScope.Shared,
            "standards/note",
            "content"));
    }

    [Fact]
    public void Sharing_requires_a_target_project()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _policy.ValidateShare(new ContextShareRequest(
                "a",
                "standards",
                [])));
    }
}
