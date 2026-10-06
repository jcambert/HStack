using System.Text;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Context;

namespace HermesStack.Application.Context;

public sealed class OpenVikingContextScopeMapper : IContextScopeMapper
{
    public string GetSearchRoot(ContextQuery query) => query.Scope switch
    {
        ContextScope.Session => "viking://~/memories/",
        ContextScope.Agent => $"viking://~/peers/{Segment(query.AgentId ?? "agent")}/memories/",
        ContextScope.Project => "viking://~/",
        ContextScope.Shared => "viking://resources/hstack-shared/",
        ContextScope.Global => throw new InvalidOperationException(
            "Global context is denied in M6. Use an explicitly shared namespace with provider-native ACLs."),
        _ => throw new ArgumentOutOfRangeException(nameof(query))
    };

    public string GetWriteUri(ContextWriteRequest request)
    {
        var relative = RelativePath(request.Name);
        if (request.Scope == ContextScope.Shared && !relative.Contains('/'))
        {
            throw new InvalidOperationException(
                "Shared context writes must target an explicitly shared namespace using '<namespace>/<item>'.");
        }

        var root = request.Scope switch
        {
            ContextScope.Session => "viking://~/memories/hstack-session/",
            ContextScope.Agent => $"viking://~/peers/{Segment(request.AgentId ?? "agent")}/memories/",
            ContextScope.Project => "viking://~/memories/hstack-project/",
            ContextScope.Shared => "viking://resources/hstack-shared/",
            ContextScope.Global => throw new InvalidOperationException(
                "Global context is denied in M6. Use an explicitly shared namespace with provider-native ACLs."),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };

        return root + relative;
    }

    public string GetSharedNamespaceUri(string @namespace) =>
        $"viking://resources/hstack-shared/{Segment(@namespace)}/";

    private static string RelativePath(string value)
    {
        var parts = value
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Segment)
            .Where(static item => item.Length > 0)
            .ToArray();

        if (parts.Length == 0)
        {
            throw new ArgumentException("Context item name is required.", nameof(value));
        }

        var result = string.Join('/', parts);
        return Path.HasExtension(result) ? result : result + ".md";
    }

    private static string Segment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "item";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-', '.');
    }
}
