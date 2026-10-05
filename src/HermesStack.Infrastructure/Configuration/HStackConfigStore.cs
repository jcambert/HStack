using HermesStack.Application.Abstractions;
using HermesStack.Domain.Network;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HermesStack.Infrastructure.Configuration;

public sealed class HStackConfigStore(IDataRootProvider dataRoot) : IProxyConfigurationStore
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<ProxyConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        var proxy = document.Proxy ?? new ProxyDto();
        return new ProxyConfiguration(
            proxy.Enabled,
            proxy.Http,
            proxy.Https,
            proxy.NoProxy ?? []);
    }

    public async Task SaveAsync(
        ProxyConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        document.Proxy = new ProxyDto
        {
            Enabled = configuration.Enabled,
            Http = configuration.Http,
            Https = configuration.Https,
            NoProxy = configuration.EffectiveNoProxy.ToList()
        };

        Directory.CreateDirectory(dataRoot.ConfigDirectory);
        var temp = dataRoot.MainConfigFile + ".tmp";
        await File.WriteAllTextAsync(temp, _serializer.Serialize(document), cancellationToken);
        File.Move(temp, dataRoot.MainConfigFile, true);
    }

    private async Task<HStackConfigDocument> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(dataRoot.MainConfigFile))
        {
            return new HStackConfigDocument();
        }

        var yaml = await File.ReadAllTextAsync(dataRoot.MainConfigFile, cancellationToken);
        var document = _deserializer.Deserialize<HStackConfigDocument>(yaml)
            ?? new HStackConfigDocument();

        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported hstack.yaml schemaVersion {document.SchemaVersion}.");
        }

        document.Orchestration ??= new OrchestrationDto();
        document.Network ??= new NetworkDto();
        document.Defaults ??= new DefaultsDto();
        document.Proxy ??= new ProxyDto();
        return document;
    }

    public sealed class HStackConfigDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public OrchestrationDto? Orchestration { get; set; } = new();
        public NetworkDto? Network { get; set; } = new();
        public DefaultsDto? Defaults { get; set; } = new();
        public ProxyDto? Proxy { get; set; } = new();
    }

    public sealed class OrchestrationDto
    {
        public string Default { get; set; } = "compose";
        public BackendDto Compose { get; set; } = new() { Enabled = true };
        public BackendDto Aspire { get; set; } = new();
    }

    public sealed class BackendDto
    {
        public bool Enabled { get; set; }
    }

    public sealed class NetworkDto
    {
        public string BindAddress { get; set; } = "127.0.0.1";
    }

    public sealed class DefaultsDto
    {
        public WorkspaceDefaultsDto Workspace { get; set; } = new();
    }

    public sealed class WorkspaceDefaultsDto
    {
        public ResourceDto Resources { get; set; } = new();
    }

    public sealed class ResourceDto
    {
        public double Cpus { get; set; } = 4;
        public string Memory { get; set; } = "8g";
        public int Pids { get; set; } = 512;
    }

    public sealed class ProxyDto
    {
        public bool Enabled { get; set; }
        public string? Http { get; set; }
        public string? Https { get; set; }
        public List<string>? NoProxy { get; set; } = [];
    }
}
