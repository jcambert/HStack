namespace HermesStack.Domain.Orchestration;

public sealed record AspireDashboardConfiguration(
    bool Enabled = true,
    bool ExposeToLan = false);

public sealed record OrchestrationConfiguration(
    string DefaultOrchestrator = "compose",
    bool ComposeEnabled = true,
    bool AspireEnabled = true,
    AspireDashboardConfiguration? AspireDashboard = null)
{
    public AspireDashboardConfiguration EffectiveAspireDashboard =>
        AspireDashboard ?? new AspireDashboardConfiguration();

    public bool IsEnabled(string orchestratorId) =>
        orchestratorId.ToLowerInvariant() switch
        {
            "compose" => ComposeEnabled,
            "aspire" => AspireEnabled,
            _ => false
        };
}
