using HermesStack.Application.Abstractions;

namespace HermesStack.Infrastructure.Operations;

public sealed class ApplicationLogService(
    IDataRootProvider dataRoot,
    ISecretRedactor redactor)
{
    private readonly string _logPath = Path.Combine(dataRoot.Root, "logs", "hstack.log");

    public async Task WriteAsync(
        string level,
        string eventName,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
        var safeDetails = redactor.Redact(details ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        var line =
            $"{DateTimeOffset.UtcNow:O} {level.ToUpperInvariant()} {eventName}" +
            (string.IsNullOrWhiteSpace(safeDetails) ? string.Empty : $" {safeDetails}") +
            Environment.NewLine;
        await File.AppendAllTextAsync(_logPath, line, cancellationToken);
    }
}
