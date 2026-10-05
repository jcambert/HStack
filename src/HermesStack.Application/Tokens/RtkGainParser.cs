using System.Globalization;
using System.Text.RegularExpressions;
using HermesStack.Domain.Tokens;

namespace HermesStack.Application.Tokens;

public static partial class RtkGainParser
{
    public static TokenGainMetrics Parse(string output)
    {
        var input = ReadLong(output, "Input Tokens");
        var optimized = ReadLong(output, "Output Tokens");
        var saved = ReadLong(output, "Saved Tokens") ?? ReadLong(output, "Saved");
        var percent = ReadPercent(output);

        if (input is null && optimized is null && saved is null)
        {
            return new TokenGainMetrics(
                "rtk",
                TokenMetricEvidence.Unavailable,
                null,
                null,
                null,
                null,
                "RTK did not expose parseable aggregate gain metrics.");
        }

        return new TokenGainMetrics(
            "rtk",
            TokenMetricEvidence.Estimated,
            input,
            optimized,
            saved,
            percent,
            "RTK estimates context tokens from shell-output size; this is not LLM billing or cost savings.");
    }

    private static long? ReadLong(string output, string label)
    {
        var match = Regex.Match(
            output,
            $@"(?im)^s*{Regex.Escape(label)}s*[:│]s*([0-9][0-9,._ ]*)");
        if (!match.Success)
        {
            return null;
        }

        var digits = new string(match.Groups[1].Value.Where(char.IsDigit).ToArray());
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double? ReadPercent(string output)
    {
        var match = PercentRegex().Match(output);
        return match.Success &&
               double.TryParse(
                   match.Groups[1].Value,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out var value)
            ? value
            : null;
    }

    [GeneratedRegex(@"(?im)(?:Saved|Savings)[^
%]*(?([0-9]+(?:.[0-9]+)?)s*%)?",
        RegexOptions.CultureInvariant)]
    private static partial Regex PercentRegex();
}
