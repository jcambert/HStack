using HermesStack.Application.Abstractions;
using HermesStack.Domain.Context;
using HermesStack.Domain.Network;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HermesStack.Infrastructure.Configuration;

public sealed class HStackConfigStore(IDataRootProvider dataRoot) :
    IProxyConfigurationStore,
    IContextConfigurationStore
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
        await SaveDocumentAsync(document, cancellationToken);
    }

    public async Task<ContextConfiguration> GetAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        var memory = document.Memory ?? new MemoryDto();
        memory.Projects ??= new Dictionary<string, ProjectMemoryDto>(StringComparer.OrdinalIgnoreCase);
        memory.Projects.TryGetValue(projectId, out var project);

        var globalBudget = memory.ContextBudget ?? new ContextBudgetDto();
        var projectBudget = project?.ContextBudget;
        return new ContextConfiguration(
            projectId,
            project?.Enabled ?? memory.Enabled,
            project?.Provider ?? memory.Provider ?? "openviking",
            ParseCapture(project?.CaptureMode ?? memory.Capture?.Mode ?? "selective"),
            ParseScope(project?.DefaultScope ?? memory.DefaultScope ?? "project"),
            new ContextBudgetOptions(
                projectBudget?.MaxTokens ?? globalBudget.MaxTokens,
                projectBudget?.MaxItems ?? globalBudget.MaxItems,
                projectBudget?.PreferSummary ?? globalBudget.PreferSummary,
                projectBudget?.ExpandOnDemand ?? globalBudget.ExpandOnDemand));
    }

    public async Task SaveAsync(
        ContextConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken);
        document.Memory ??= new MemoryDto();
        document.Memory.Projects ??= new Dictionary<string, ProjectMemoryDto>(StringComparer.OrdinalIgnoreCase);
        document.Memory.Projects[configuration.ProjectId] = new ProjectMemoryDto
        {
            Enabled = configuration.Enabled,
            Provider = configuration.ProviderId,
            CaptureMode = configuration.CaptureMode.ToString().ToLowerInvariant(),
            DefaultScope = configuration.DefaultScope.ToString().ToLowerInvariant(),
            ContextBudget = new ContextBudgetDto
            {
                MaxTokens = configuration.EffectiveBudget.MaxTokens,
                MaxItems = configuration.EffectiveBudget.MaxItems,
                PreferSummary = configuration.EffectiveBudget.PreferSummary,
                ExpandOnDemand = configuration.EffectiveBudget.ExpandOnDemand
            }
        };
        await SaveDocumentAsync(document, cancellationToken);
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
        document.Memory ??= new MemoryDto();
        document.Memory.Projects ??= new Dictionary<string, ProjectMemoryDto>(StringComparer.OrdinalIgnoreCase);
        return document;
    }

    private async Task SaveDocumentAsync(
        HStackConfigDocument document,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dataRoot.ConfigDirectory);
        var temp = dataRoot.MainConfigFile + ".tmp";
        await File.WriteAllTextAsync(temp, _serializer.Serialize(document), cancellationToken);
        File.Move(temp, dataRoot.MainConfigFile, true);
    }

    private static ContextCaptureMode ParseCapture(string value) =>
        Enum.TryParse<ContextCaptureMode>(value, true, out var result)
            ? result
            : ContextCaptureMode.Selective;

    private static ContextScope ParseScope(string value) =>
        Enum.TryParse<ContextScope>(value, true, out var result)
            ? result
            : ContextScope.Project;

    public sealed class HStackConfigDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public OrchestrationDto? Orchestration { get; set; } = new();
        public NetworkDto? Network { get; set; } = new();
        public DefaultsDto? Defaults { get; set; } = new();
        public ProxyDto? Proxy { get; set; } = new();
        public MemoryDto? Memory { get; set; } = new();
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

    public sealed class MemoryDto
    {
        public bool Enabled { get; set; } = false;
        public string? Provider { get; set; } = "openviking";
        public CaptureDto? Capture { get; set; } = new();
        public ContextBudgetDto? ContextBudget { get; set; } = new();
        public string? DefaultScope { get; set; } = "project";
        public Dictionary<string, ProjectMemoryDto>? Projects { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class CaptureDto
    {
        public string Mode { get; set; } = "selective";
    }

    public sealed class ProjectMemoryDto
    {
        public bool? Enabled { get; set; }
        public string? Provider { get; set; }
        public string? CaptureMode { get; set; }
        public string? DefaultScope { get; set; }
        public ContextBudgetDto? ContextBudget { get; set; }
    }

    public sealed class ContextBudgetDto
    {
        public int MaxTokens { get; set; } = 12000;
        public int MaxItems { get; set; } = 20;
        public bool PreferSummary { get; set; } = true;
        public bool ExpandOnDemand { get; set; } = true;
    }
}
