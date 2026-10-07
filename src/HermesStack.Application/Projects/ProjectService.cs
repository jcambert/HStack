using System.Text.RegularExpressions;
using HermesStack.Application.Abstractions;
using HermesStack.Application.Security;
using HermesStack.Domain.Projects;

namespace HermesStack.Application.Projects;

public sealed partial class ProjectService(IProjectStore store, HostMountValidator mountValidator)
{
    public async Task<ProjectDefinition> AddAsync(string id, string hostPath, string? name = null, CancellationToken cancellationToken = default)
    {
        if (!ProjectIdRegex().IsMatch(id))
        {
            throw new ArgumentException("Project id must match ^[a-z0-9][a-z0-9-]{0,62}$.", nameof(id));
        }

        var mount = mountValidator.Validate(hostPath);
        if (!mount.IsAllowed)
        {
            throw new InvalidOperationException($"{mount.Code}: {mount.Message}");
        }

        if (!Directory.Exists(hostPath))
        {
            throw new DirectoryNotFoundException($"Project directory does not exist: {hostPath}");
        }

        if (await store.FindAsync(id, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Project '{id}' already exists.");
        }

        var project = new ProjectDefinition(id, name ?? id, mount.NormalizedPath);
        await store.SaveAsync(project, cancellationToken);
        return project;
    }

    public HermesStack.Domain.Security.MountValidationResult ValidateHostPath(string hostPath) =>
        mountValidator.Validate(hostPath);

    public Task<IReadOnlyList<ProjectDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
        store.ListAsync(cancellationToken);

    public async Task<ProjectDefinition> GetRequiredAsync(string id, CancellationToken cancellationToken = default) =>
        await store.FindAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"Unknown project '{id}'.");

    public async Task<ProjectDefinition> EditAsync(
        string id,
        string? hostPath = null,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var current = await GetRequiredAsync(id, cancellationToken);
        var effectivePath = hostPath is null ? current.HostPath : mountValidator.Validate(hostPath).NormalizedPath;
        if (hostPath is not null)
        {
            var mount = mountValidator.Validate(hostPath);
            if (!mount.IsAllowed)
            {
                throw new InvalidOperationException($"{mount.Code}: {mount.Message}");
            }

            if (!Directory.Exists(mount.NormalizedPath))
            {
                throw new DirectoryNotFoundException(
                    $"Project directory does not exist: {mount.NormalizedPath}");
            }

            effectivePath = mount.NormalizedPath;
        }

        var updated = current with
        {
            HostPath = effectivePath,
            Name = string.IsNullOrWhiteSpace(name) ? current.Name : name.Trim()
        };
        await store.SaveAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<ProjectDefinition> SetOrchestratorAsync(
        string id,
        string orchestrator,
        CancellationToken cancellationToken = default)
    {
        var normalized = orchestrator.Trim().ToLowerInvariant();
        if (normalized is not ("compose" or "aspire"))
        {
            throw new ArgumentException(
                "Orchestrator must be 'compose' or 'aspire'.",
                nameof(orchestrator));
        }

        var current = await GetRequiredAsync(id, cancellationToken);
        var updated = current with { Orchestrator = normalized };
        await store.SaveAsync(updated, cancellationToken);
        return updated;
    }

    public async Task RemoveAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        _ = await GetRequiredAsync(id, cancellationToken);
        await store.RemoveAsync(id, cancellationToken);
    }

    public async Task<ProjectDefinition> AddPortAsync(
        string id,
        int containerPort,
        int? hostPort = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePort(containerPort, nameof(containerPort));
        var effectiveHost = hostPort ?? containerPort;
        ValidatePort(effectiveHost, nameof(hostPort));

        var current = await GetRequiredAsync(id, cancellationToken);
        if (current.EffectivePorts.Any(port =>
            port.Container == containerPort ||
            port.EffectiveHost == effectiveHost))
        {
            throw new InvalidOperationException(
                $"Project '{id}' already declares container port {containerPort} or host port {effectiveHost}.");
        }

        var updated = current with
        {
            Ports =
            [
                .. current.EffectivePorts,
                new ProjectPort(containerPort, effectiveHost, "127.0.0.1")
            ]
        };
        await store.SaveAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<ProjectDefinition> RemovePortAsync(
        string id,
        int containerPort,
        CancellationToken cancellationToken = default)
    {
        ValidatePort(containerPort, nameof(containerPort));
        var current = await GetRequiredAsync(id, cancellationToken);
        var ports = current.EffectivePorts
            .Where(port => port.Container != containerPort)
            .ToArray();
        if (ports.Length == current.EffectivePorts.Count)
        {
            throw new KeyNotFoundException(
                $"Project '{id}' does not declare container port {containerPort}.");
        }

        var updated = current with { Ports = ports };
        await store.SaveAsync(updated, cancellationToken);
        return updated;
    }

    private static void ValidatePort(int port, string parameterName)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Ports must be between 1 and 65535.");
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectIdRegex();
}
