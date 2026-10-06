using HermesStack.Application.Abstractions;
using HermesStack.Domain.Context;

namespace HermesStack.Application.Context;

public sealed class ContextSecretFilter(ISecretRedactor redactor) : IContextSecretFilter
{
    private static readonly string[] ForbiddenMarkers =
    [
        "-----BEGIN PRIVATE KEY-----",
        "-----BEGIN RSA PRIVATE KEY-----",
        "-----BEGIN OPENSSH PRIVATE KEY-----",
        "-----BEGIN EC PRIVATE KEY-----"
    ];

    public ContextSecretFilterResult Filter(string content)
    {
        if (ForbiddenMarkers.Any(marker =>
            content.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return new ContextSecretFilterResult(
                string.Empty,
                true,
                true,
                "Private-key material is forbidden in durable context.");
        }

        var redacted = redactor.Redact(content);
        return new ContextSecretFilterResult(
            redacted,
            !string.Equals(redacted, content, StringComparison.Ordinal));
    }
}
