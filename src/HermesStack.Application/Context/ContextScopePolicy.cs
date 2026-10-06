using HermesStack.Application.Abstractions;
using HermesStack.Domain.Context;

namespace HermesStack.Application.Context;

public sealed class ContextScopePolicy : IContextScopePolicy
{
    public void ValidateQuery(ContextQuery query)
    {
        DenyGlobal(query.Scope);
        if (query.Scope == ContextScope.Agent && string.IsNullOrWhiteSpace(query.AgentId))
        {
            throw new InvalidOperationException("Agent context requires an explicit agent id.");
        }
    }

    public void ValidateWrite(ContextWriteRequest request)
    {
        DenyGlobal(request.Scope);
        if (request.Scope == ContextScope.Agent && string.IsNullOrWhiteSpace(request.AgentId))
        {
            throw new InvalidOperationException("Agent context writes require an explicit agent id.");
        }

        if (request.Scope == ContextScope.Shared &&
            !request.Name.Replace('\\', '/').Contains('/'))
        {
            throw new InvalidOperationException(
                "Shared context writes require an explicit '<namespace>/<item>' target.");
        }
    }

    public void ValidateShare(ContextShareRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Namespace))
        {
            throw new InvalidOperationException("A shared context namespace is required.");
        }

        if (request.ProjectIds.Count == 0)
        {
            throw new InvalidOperationException("At least one target project is required for sharing.");
        }
    }

    private static void DenyGlobal(ContextScope scope)
    {
        if (scope == ContextScope.Global)
        {
            throw new InvalidOperationException(
                "Global context is denied in M6. Use an explicitly shared namespace.");
        }
    }
}
