using System.Text.Json;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Context;

namespace HermesStack.Infrastructure.Context;

public sealed class JsonContextTraceStore(IDataRootProvider dataRoot) : IContextTraceStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task SaveAsync(
        ContextRetrievalTrace trace,
        CancellationToken cancellationToken = default)
    {
        var directory = dataRoot.GetProjectRuntimeRoot(trace.ProjectId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "context-last.json");
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(
            temp,
            JsonSerializer.Serialize(trace, Options),
            cancellationToken);
        File.Move(temp, path, true);
    }

    public async Task<ContextRetrievalTrace?> GetLatestAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(dataRoot.GetProjectRuntimeRoot(projectId), "context-last.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<ContextRetrievalTrace>(json, Options);
    }
}
