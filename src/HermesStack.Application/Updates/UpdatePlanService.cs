using HermesStack.Domain.Updates;

namespace HermesStack.Application.Updates;

public sealed class UpdatePlanService(UpdateCheckService checks)
{
    public async Task<UpdatePlan> CreateAsync(
        IReadOnlyList<ManagedComponentVersion> current,
        CancellationToken cancellationToken = default)
    {
        var result = await checks.CheckAsync(current, cancellationToken);
        var changes = result.Items
            .Where(static item => item.State != UpdateState.UpToDate)
            .ToArray();

        return new UpdatePlan(
            result.Source,
            changes,
            [
                new(1, "check", "Check managed versions", false),
                new(2, "metadata", "Download and validate pinned update metadata", false),
                new(3, "checksums", "Validate required checksums/digests", false),
                new(4, "backup", "Back up HermesStack configuration", true),
                new(5, "lock", "Stage the new toolchain lock atomically", true),
                new(6, "images", "Build the pinned workspace images", true),
                new(7, "stop", "Stop workspaces that were running", true),
                new(8, "recreate", "Recreate those workspaces with the new image", true),
                new(9, "health", "Validate workspace health and agent availability", false),
                new(10, "commit", "Commit the toolchain lock; otherwise roll back", true)
            ]);
    }
}
