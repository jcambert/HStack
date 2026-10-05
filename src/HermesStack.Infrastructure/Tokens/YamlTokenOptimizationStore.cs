using HermesStack.Application.Abstractions;
using HermesStack.Domain.Tokens;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HermesStack.Infrastructure.Tokens;

public sealed class YamlTokenOptimizationStore(IDataRootProvider dataRoot) : ITokenOptimizationStore
{
    private readonly string _path = Path.Combine(dataRoot.ConfigDirectory, "token-optimization.yaml");

    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<TokenOptimizationConfiguration> GetAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        var item = document.Projects.FirstOrDefault(value =>
            string.Equals(value.Project, projectId, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            return new TokenOptimizationConfiguration(
                projectId,
                false,
                TokenOptimizationProfile.Off,
                []);
        }

        if (!Enum.TryParse<TokenOptimizationProfile>(
            item.Profile,
            ignoreCase: true,
            out var profile))
        {
            throw new InvalidDataException(
                $"Unknown token optimization profile '{item.Profile}' for project '{projectId}'.");
        }

        return new TokenOptimizationConfiguration(
            projectId,
            item.Enabled,
            profile,
            item.Providers.Select(static provider =>
                new TokenProviderSelection(
                    provider.Id,
                    provider.Agents
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(static value => value, StringComparer.Ordinal)
                        .ToArray()))
                .ToArray());
    }

    public async Task SaveAsync(
        TokenOptimizationConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        document.Projects = document.Projects
            .Where(value => !string.Equals(
                value.Project,
                configuration.ProjectId,
                StringComparison.OrdinalIgnoreCase))
            .Append(new ProjectTokenOptimizationDto
            {
                Project = configuration.ProjectId,
                Enabled = configuration.Enabled,
                Profile = configuration.Profile.ToString().ToLowerInvariant(),
                Providers = configuration.EffectiveProviders
                    .Select(static value => new ProviderDto
                    {
                        Id = value.ProviderId,
                        Agents = value.Agents
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(static agent => agent, StringComparer.Ordinal)
                            .ToList()
                    })
                    .OrderBy(static value => value.Id, StringComparer.Ordinal)
                    .ToList()
            })
            .OrderBy(static value => value.Project, StringComparer.Ordinal)
            .ToList();

        Directory.CreateDirectory(dataRoot.ConfigDirectory);
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, _serializer.Serialize(document), cancellationToken);
        File.Move(temp, _path, true);
    }

    private async Task<DocumentDto> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new DocumentDto();
        }

        var yaml = await File.ReadAllTextAsync(_path, cancellationToken);
        var document = _deserializer.Deserialize<DocumentDto>(yaml) ?? new DocumentDto();
        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported token-optimization.yaml schemaVersion {document.SchemaVersion}.");
        }

        document.Projects ??= [];
        foreach (var project in document.Projects)
        {
            project.Providers ??= [];
            foreach (var provider in project.Providers)
            {
                provider.Agents ??= [];
            }
        }

        return document;
    }

    public sealed class DocumentDto
    {
        public int SchemaVersion { get; set; } = 1;
        public List<ProjectTokenOptimizationDto> Projects { get; set; } = [];
    }

    public sealed class ProjectTokenOptimizationDto
    {
        public string Project { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string Profile { get; set; } = "off";
        public List<ProviderDto> Providers { get; set; } = [];
    }

    public sealed class ProviderDto
    {
        public string Id { get; set; } = string.Empty;
        public List<string> Agents { get; set; } = [];
    }
}
