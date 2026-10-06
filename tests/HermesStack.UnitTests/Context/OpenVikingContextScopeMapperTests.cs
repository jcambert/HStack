using HermesStack.Application.Context;
using HermesStack.Domain.Context;

namespace HermesStack.UnitTests.Context;

public sealed class OpenVikingContextScopeMapperTests
{
    private readonly OpenVikingContextScopeMapper _mapper = new();

    [Fact]
    public void Project_and_agent_scopes_stay_in_private_user_space()
    {
        Assert.Equal(
            "viking://~/",
            _mapper.GetSearchRoot(new ContextQuery("a", "q")));
        Assert.Equal(
            "viking://~/peers/claude/memories/",
            _mapper.GetSearchRoot(new ContextQuery("a", "q", ContextScope.Agent, "Claude")));
    }

    [Fact]
    public void Shared_scope_uses_provider_native_shared_resource_root()
    {
        Assert.Equal(
            "viking://resources/hstack-shared/architecture/",
            _mapper.GetSharedNamespaceUri("Architecture"));
    }

    [Fact]
    public void Write_names_are_sanitized_and_receive_text_extension()
    {
        var uri = _mapper.GetWriteUri(
            new ContextWriteRequest("a", ContextScope.Project, "../Decision 001", "content"));

        Assert.Equal("viking://~/memories/hstack-project/decision-001.md", uri);
        Assert.DoesNotContain("..", uri, StringComparison.Ordinal);
    }
}
