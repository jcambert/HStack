namespace HermesStack.Domain.Updates;

public sealed record UpdatePlanStep(
    int Order,
    string Id,
    string Description,
    bool MutatesState);

public sealed record UpdatePlan(
    string Source,
    IReadOnlyList<UpdateCheckItem> Changes,
    IReadOnlyList<UpdatePlanStep> Steps)
{
    public bool HasChanges => Changes.Any(static item =>
        item.State is UpdateState.UpdateAvailable or UpdateState.Different);
}
