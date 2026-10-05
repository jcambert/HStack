using HermesStack.Application.Abstractions;
using HermesStack.Domain.Projects;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HermesStack.Infrastructure.Projects;

public sealed class YamlProjectStore(IDataRootProvider dataRoot) : IProjectStore
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<IReadOnlyList<ProjectDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        return document.Projects
            .Select(static p => p.ToDefinition())
            .OrderBy(static p => p.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<ProjectDefinition?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        var projects = await ListAsync(cancellationToken);
        return projects.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveAsync(ProjectDefinition project, CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        document.Projects = document.Projects
            .Where(p => !string.Equals(p.Id, project.Id, StringComparison.OrdinalIgnoreCase))
            .Append(ProjectDto.FromDefinition(project))
            .OrderBy(static p => p.Id, StringComparer.Ordinal)
            .ToList();

        await SaveDocumentAsync(document, cancellationToken);
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        document.Projects = document.Projects
            .Where(p => !string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await SaveDocumentAsync(document, cancellationToken);
    }

    private async Task<ProjectsDocument> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(dataRoot.ProjectsFile))
        {
            return new ProjectsDocument();
        }

        var yaml = await File.ReadAllTextAsync(dataRoot.ProjectsFile, cancellationToken);
        var document = _deserializer.Deserialize<ProjectsDocument>(yaml) ?? new ProjectsDocument();
        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported projects.yaml schemaVersion {document.SchemaVersion}.");
        }

        document.Projects ??= [];
        return document;
    }

    private async Task SaveDocumentAsync(ProjectsDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dataRoot.ConfigDirectory);
        var temp = dataRoot.ProjectsFile + ".tmp";
        await File.WriteAllTextAsync(temp, _serializer.Serialize(document), cancellationToken);
        File.Move(temp, dataRoot.ProjectsFile, true);
    }

    public sealed class ProjectsDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public List<ProjectDto> Projects { get; set; } = [];
    }

    public sealed class ProjectDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string HostPath { get; set; } = string.Empty;
        public string ContainerPath { get; set; } = "/workspace";
        public string Access { get; set; } = "read-write";
        public string? Orchestrator { get; set; }
        public ResourceDto? Resources { get; set; }
        public List<PortDto>? Ports { get; set; }

        public ProjectDefinition ToDefinition() => new(
            Id,
            string.IsNullOrWhiteSpace(Name) ? Id : Name,
            HostPath,
            ContainerPath,
            Access,
            Orchestrator,
            Resources?.ToDefinition(),
            Ports?.Select(static p => p.ToDefinition()).ToArray());

        public static ProjectDto FromDefinition(ProjectDefinition project) => new()
        {
            Id = project.Id,
            Name = project.Name,
            HostPath = project.HostPath,
            ContainerPath = project.ContainerPath,
            Access = project.Access,
            Orchestrator = project.Orchestrator,
            Resources = ResourceDto.FromDefinition(project.EffectiveResources),
            Ports = project.EffectivePorts.Select(PortDto.FromDefinition).ToList()
        };
    }

    public sealed class ResourceDto
    {
        public double Cpus { get; set; } = 4;
        public string Memory { get; set; } = "8g";
        public int Pids { get; set; } = 512;

        public WorkspaceResources ToDefinition() => new(Cpus, Memory, Pids);
        public static ResourceDto FromDefinition(WorkspaceResources value) => new() { Cpus = value.Cpus, Memory = value.Memory, Pids = value.Pids };
    }

    public sealed class PortDto
    {
        public int Container { get; set; }
        public int? Host { get; set; }
        public string Bind { get; set; } = "127.0.0.1";

        public ProjectPort ToDefinition() => new(Container, Host, Bind);
        public static PortDto FromDefinition(ProjectPort value) => new() { Container = value.Container, Host = value.Host, Bind = value.Bind };
    }
}
