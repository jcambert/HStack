using System.Text.RegularExpressions;
using HermesStack.Application.Abstractions;

namespace HermesStack.Application.Security;

public sealed partial class SecretRedactor : ISecretRedactor
{
    private const string Mask = "***REDACTED***";

    public string Redact(string input, IEnumerable<string>? knownSecrets = null)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var redacted = input;
        foreach (var secret in (knownSecrets ?? [])
            .Where(static value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(static value => value.Length))
        {
            redacted = redacted.Replace(secret, Mask, StringComparison.Ordinal);
        }

        return CommonSecretPattern().Replace(redacted, Mask);
    }

    [GeneratedRegex(
        @"(?i)\b(?:sk-[a-z0-9_-]{8,}|gh[pousr]_[a-z0-9_]{8,}|xox[baprs]-[a-z0-9-]{8,})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex CommonSecretPattern();
}
