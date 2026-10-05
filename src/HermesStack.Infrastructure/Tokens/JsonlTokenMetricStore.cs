using System.Text.Json;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Tokens;

namespace HermesStack.Infrastructure.Tokens;

public sealed class JsonlTokenMetricStore(IDataRootProvider dataRoot) : ITokenMetricStore
{
    public async Task AppendAsync(
        TokenMetricRecord record,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(
            dataRoot.GetProjectDataRoot(record.ProjectId),
            "metrics");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "token-gain.jsonl");

        var line = JsonSerializer.Serialize(record);
        await File.AppendAllTextAsync(
            path,
            line + Environment.NewLine,
            cancellationToken);
    }

    public async Task<IReadOnlyList<TokenMetricRecord>> ReadAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(
            dataRoot.GetProjectDataRoot(projectId),
            "metrics",
            "token-gain.jsonl");
        if (!File.Exists(path))
        {
            return [];
        }

        var result = new List<TokenMetricRecord>();
        foreach (var line in await File.ReadAllLinesAsync(path, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<TokenMetricRecord>(line);
            if (item is not null)
            {
                result.Add(item);
            }
        }

        return result
            .OrderByDescending(static value => value.Timestamp)
            .ToArray();
    }
}
