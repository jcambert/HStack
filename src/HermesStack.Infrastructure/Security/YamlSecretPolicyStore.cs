using HermesStack.Application.Abstractions;
using HermesStack.Domain.Security;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HermesStack.Infrastructure.Security;

public sealed class YamlSecretPolicyStore(IDataRootProvider dataRoot) : ISecretPolicyStore
{
    private readonly string _path = Path.Combine(dataRoot.ConfigDirectory, "secrets.yaml");

    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task SetAsync(
        SecretPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        document.Secrets = document.Secrets
            .Where(item =>
                !string.Equals(item.Project, policy.ProjectId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(item.Name, policy.Name, StringComparison.Ordinal))
            .Append(new SecretPolicyDto
            {
                Project = policy.ProjectId,
                Name = policy.Name,
                Agents = policy.Agents
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToList()
            })
            .OrderBy(static item => item.Project, StringComparer.Ordinal)
            .ThenBy(static item => item.Name, StringComparer.Ordinal)
            .ToList();

        await SaveAsync(document, cancellationToken);
    }

    public async Task<IReadOnlyList<SecretPolicy>> ListAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        return document.Secrets
            .Where(item => string.Equals(item.Project, projectId, StringComparison.OrdinalIgnoreCase))
            .Select(static item => new SecretPolicy(item.Project, item.Name, item.Agents))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<SecretPolicy>> FindForAgentAsync(
        string projectId,
        string agentId,
        CancellationToken cancellationToken = default)
    {
        var policies = await ListAsync(projectId, cancellationToken);
        return policies
            .Where(policy => policy.Agents.Contains(agentId, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    public async Task RemoveAsync(
        string projectId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        document.Secrets = document.Secrets
            .Where(item =>
                !string.Equals(item.Project, projectId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(item.Name, name, StringComparison.Ordinal))
            .ToList();
        await SaveAsync(document, cancellationToken);
    }

    private async Task<SecretPolicyDocument> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new SecretPolicyDocument();
        }

        var yaml = await File.ReadAllTextAsync(_path, cancellationToken);
        var document = _deserializer.Deserialize<SecretPolicyDocument>(yaml)
            ?? new SecretPolicyDocument();
        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported secrets.yaml schemaVersion {document.SchemaVersion}.");
        }

        document.Secrets ??= [];
        return document;
    }

    private async Task SaveAsync(
        SecretPolicyDocument document,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, _serializer.Serialize(document), cancellationToken);
        File.Move(temp, _path, true);
    }

    public sealed class SecretPolicyDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public List<SecretPolicyDto> Secrets { get; set; } = [];
    }

    public sealed class SecretPolicyDto
    {
        public string Project { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> Agents { get; set; } = [];
    }
}
