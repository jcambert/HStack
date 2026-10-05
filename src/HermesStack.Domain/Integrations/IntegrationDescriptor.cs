namespace HermesStack.Domain.Integrations;

public enum IntegrationKind
{
    Orchestrator,
    ExecutionProvider,
    Agent,
    SessionManager,
    TokenOptimizer,
    ContextProvider,
    CertificateProvider,
    SecretStore,
    ObservabilityProvider
}

public enum IntegrationCapability
{
    BindMounts,
    NamedVolumes,
    ReadOnlyRoot,
    NoNewPrivileges,
    DropCapabilities,
    LocalhostPortBinding,
    HealthChecks,
    InteractiveTty,
    PersistentHome,
    Mcp,
    AgentHooks,
    SharedMemory,
    TokenOptimization,
    StructuredLogs,
    Traces,
    Metrics,
    ParallelWorktrees
}

public sealed record IntegrationDescriptor(
    string Id,
    string DisplayName,
    IntegrationKind Kind,
    IReadOnlySet<IntegrationCapability> Capabilities,
    bool RequiredForMvp = false,
    string Strategy = "integrate");
