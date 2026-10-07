using System.IO.Compression;
using System.Text.Json;
using HermesStack.Application.Abstractions;

namespace HermesStack.Infrastructure.Operations;

public sealed record BackupArchiveResult(
    string Path,
    string Kind,
    string? ProjectId,
    long Length);

public sealed class BackupArchiveService(IDataRootProvider dataRoot)
{
    private static readonly string[] PortableRoots = ["config", "certs"];

    public Task<BackupArchiveResult> BackupAsync(
        string? projectId = null,
        bool configOnly = false,
        string? outputPath = null,
        CancellationToken cancellationToken = default) =>
        CreateAsync(
            kind: "backup",
            projectId,
            includeProjectData: !configOnly,
            outputPath,
            cancellationToken);

    public Task<BackupArchiveResult> ExportAsync(
        string outputPath,
        CancellationToken cancellationToken = default) =>
        CreateAsync(
            kind: "portable-export",
            projectId: null,
            includeProjectData: false,
            outputPath,
            cancellationToken);

    public async Task RestoreAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(archivePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("HS8001: Backup archive was not found.", source);
        }

        Directory.CreateDirectory(dataRoot.Root);
        using var archive = ZipFile.OpenRead(source);
        ValidateManifest(archive);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name) ||
                string.Equals(entry.FullName, "manifest.json", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsAllowedEntry(entry.FullName))
            {
                throw new InvalidDataException(
                    $"HS8002: Backup contains unsupported entry '{entry.FullName}'.");
            }

            var destination = SafeDestination(entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = entry.Open();
            await using var output = File.Create(destination);
            await input.CopyToAsync(output, cancellationToken);
        }
    }

    public Task ImportAsync(
        string archivePath,
        CancellationToken cancellationToken = default) =>
        RestoreAsync(archivePath, cancellationToken);

    private async Task<BackupArchiveResult> CreateAsync(
        string kind,
        string? projectId,
        bool includeProjectData,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        var backupsRoot = Path.Combine(dataRoot.Root, "backups");
        Directory.CreateDirectory(backupsRoot);
        var effectiveOutput = Path.GetFullPath(
            outputPath
            ?? Path.Combine(
                backupsRoot,
                $"hstack-{kind}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip"));

        Directory.CreateDirectory(Path.GetDirectoryName(effectiveOutput)!);
        if (File.Exists(effectiveOutput))
        {
            File.Delete(effectiveOutput);
        }

        using (var archive = ZipFile.Open(effectiveOutput, ZipArchiveMode.Create))
        {
            var manifest = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var manifestStream = manifest.Open())
            {
                await JsonSerializer.SerializeAsync(
                    manifestStream,
                    new ArchiveManifest(1, kind, projectId, DateTimeOffset.UtcNow),
                    cancellationToken: cancellationToken);
            }

            foreach (var root in PortableRoots)
            {
                var source = Path.Combine(dataRoot.Root, root);
                AddDirectory(archive, source, root, cancellationToken);
            }

            if (includeProjectData)
            {
                if (projectId is null)
                {
                    AddDirectory(
                        archive,
                        Path.Combine(dataRoot.Root, "data", "projects"),
                        "data/projects",
                        cancellationToken);
                }
                else
                {
                    AddDirectory(
                        archive,
                        dataRoot.GetProjectDataRoot(projectId),
                        $"data/projects/{projectId}",
                        cancellationToken);
                }
            }
        }

        var info = new FileInfo(effectiveOutput);
        return new BackupArchiveResult(
            effectiveOutput,
            kind,
            projectId,
            info.Length);
    }

    private static void AddDirectory(
        ZipArchive archive,
        string source,
        string archiveRoot,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, file)
                .Replace('\\', '/');
            archive.CreateEntryFromFile(
                file,
                $"{archiveRoot}/{relative}",
                CompressionLevel.Optimal);
        }
    }

    private static void ValidateManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException("HS8003: Backup manifest is missing.");
        using var stream = entry.Open();
        var manifest = JsonSerializer.Deserialize<ArchiveManifest>(stream)
            ?? throw new InvalidDataException("HS8003: Backup manifest is invalid.");
        if (manifest.SchemaVersion != 1 ||
            manifest.Kind is not ("backup" or "portable-export"))
        {
            throw new InvalidDataException("HS8003: Unsupported backup archive format.");
        }
    }

    private static bool IsAllowedEntry(string name) =>
        name.StartsWith("config/", StringComparison.Ordinal) ||
        name.StartsWith("certs/", StringComparison.Ordinal) ||
        name.StartsWith("data/projects/", StringComparison.Ordinal);

    private string SafeDestination(string entryName)
    {
        var normalized = entryName.Replace('/', Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(Path.Combine(dataRoot.Root, normalized));
        var root = Path.GetFullPath(dataRoot.Root) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"HS8002: Backup entry escapes the HermesStack data root: '{entryName}'.");
        }

        return destination;
    }

    private sealed record ArchiveManifest(
        int SchemaVersion,
        string Kind,
        string? ProjectId,
        DateTimeOffset CreatedUtc);
}
