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

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectIdRegex();
}
