using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HermesStack.Domain.Tokens;

namespace HermesStack.Application.Tokens;

public static partial class RtkGainParser
{
    public static TokenGainMetrics Parse(string output)
    {
        if (TryParseJson(output, out var jsonMetrics))
        {
            return jsonMetrics;
        }

        return ParseTextFallback(output);
    }

    private static bool TryParseJson(
        string output,
        out TokenGainMetrics metrics)
    {
        metrics = default!;
        if (string.IsNullOrWhiteSpace(output) ||
            !output.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            if (!document.RootElement.TryGetProperty("summary", out var summary))
            {
                return false;
            }

            var input = ReadJsonLong(summary, "total_input");
            var optimized = ReadJsonLong(summary, "total_output");
            var saved = ReadJsonLong(summary, "total_saved");
            var percent = ReadJsonDouble(summary, "avg_savings_pct");

            if (input is null && optimized is null && saved is null)
            {
                return false;
            }

            metrics = Estimated(input, optimized, saved, percent);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static TokenGainMetrics ParseTextFallback(string output)
    {
        var input = ReadLong(output, "Input Tokens");
        var optimized = ReadLong(output, "Output Tokens");
        var saved = ReadLong(output, "Saved Tokens") ?? ReadLong(output, "Saved");
        var percent = ReadPercent(output);

        return input is null && optimized is null && saved is null
            ? new TokenGainMetrics(
                "rtk",
                TokenMetricEvidence.Unavailable,
                null,
                null,
                null,
                null,
                "RTK did not expose parseable aggregate gain metrics.")
            : Estimated(input, optimized, saved, percent);
    }

    private static TokenGainMetrics Estimated(
        long? input,
        long? optimized,
        long? saved,
        double? percent) =>
        new(
            "rtk",
            TokenMetricEvidence.Estimated,
            input,
            optimized,
            saved,
            percent,
            "RTK estimates context tokens from shell-output size; this is not LLM billing or cost savings.");

    private static long? ReadJsonLong(JsonElement summary, string property) =>
        summary.TryGetProperty(property, out var value) && value.TryGetInt64(out var parsed)
            ? parsed
            : null;

    private static double? ReadJsonDouble(JsonElement summary, string property) =>
        summary.TryGetProperty(property, out var value) && value.TryGetDouble(out var parsed)
            ? parsed
            : null;

    private static long? ReadLong(string output, string label)
    {
        var match = Regex.Match(
            output,
            $@"(?im)^\s*{Regex.Escape(label)}\s*[:│]\s*([0-9][0-9,._ ]*)");
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

    [GeneratedRegex(
        @"(?im)(?:Saved|Savings)[^\r\n%]*?([0-9]+(?:\.[0-9]+)?)\s*%",
        RegexOptions.CultureInvariant)]
    private static partial Regex PercentRegex();
}
