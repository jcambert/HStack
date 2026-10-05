using HermesStack.Application.Abstractions;

namespace HermesStack.Infrastructure.Configuration;

public sealed class HStackInitializer(IDataRootProvider dataRoot)
{
    public async Task InitializeAsync(string orchestrator = "compose", CancellationToken cancellationToken = default)
    {
        var directories = new[]
        {
            dataRoot.Root,
            dataRoot.ConfigDirectory,
            dataRoot.CorporateCertificatesDirectory,
            dataRoot.GeneratedCertificatesDirectory,
            Path.Combine(dataRoot.Root, "data", "projects"),
            Path.Combine(dataRoot.Root, "cache", "npm"),
            Path.Combine(dataRoot.Root, "cache", "pip"),
            Path.Combine(dataRoot.Root, "cache", "downloads"),
            Path.Combine(dataRoot.Root, "runtime", "compose"),
            Path.Combine(dataRoot.Root, "runtime", "projects"),
            Path.Combine(dataRoot.Root, "runtime", "locks"),
            Path.Combine(dataRoot.Root, "backups"),
            Path.Combine(dataRoot.Root, "logs"),
            Path.Combine(dataRoot.Root, "versions"),
            Path.Combine(dataRoot.Root, "secrets")
        };

        foreach (var directory in directories)
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(dataRoot.MainConfigFile))
        {
            var content = string.Join('\n',
            [
                "schemaVersion: 1",
                "orchestration:",
                $"  default: {orchestrator}",
                "  compose:",
                "    enabled: true",
                "  aspire:",
                "    enabled: false",
                "network:",
                "  bindAddress: 127.0.0.1",
                "proxy:",
                "  enabled: false",
                "  noProxy:",
                "    - localhost",
                "    - 127.0.0.1",
                "    - ::1",
                "    - host.docker.internal",
                "defaults:",
                "  workspace:",
                "    resources:",
                "      cpus: 4",
                "      memory: 8g",
                "      pids: 512",
                string.Empty
            ]);
            await File.WriteAllTextAsync(dataRoot.MainConfigFile, content, cancellationToken);
        }

        if (!File.Exists(dataRoot.ProjectsFile))
        {
            await File.WriteAllTextAsync(dataRoot.ProjectsFile, "schemaVersion: 1\nprojects: []\n", cancellationToken);
        }
    }
}
