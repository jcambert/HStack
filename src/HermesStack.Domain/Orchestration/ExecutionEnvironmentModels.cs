namespace HermesStack.Domain.Orchestration;

// An execution provider chooses the isolation boundary; the orchestrator
// continues to own resource topology, CLI behavior and concrete OCI lifecycle.
public sealed record ExecutionProviderAvailability(
    bool IsAvailable,
    string? Version = null,
    string? Reason = null);

public sealed record ExecutionEnvironmentPlan(
    string ProviderId,
    WorkspaceDeploymentPlan Workspace,
    OrchestratorCapabilityReport Capabilities);

public sealed record ExecutionEnvironmentHandle(
    string ProviderId,
    WorkspaceDeploymentPlan Workspace);
