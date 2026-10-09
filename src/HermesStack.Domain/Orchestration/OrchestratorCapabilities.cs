namespace HermesStack.Domain.Orchestration;

public sealed record OrchestratorCapabilityReport(
    bool IsSupported,
    IReadOnlyList<string> MissingCapabilities,
    IReadOnlyList<string> Notes)
{
    public static OrchestratorCapabilityReport Supported(params string[] notes) =>
        new(true, [], notes);

    public static OrchestratorCapabilityReport Unsupported(
        IReadOnlyList<string> missingCapabilities,
        params string[] notes) =>
        new(false, missingCapabilities, notes);
}
