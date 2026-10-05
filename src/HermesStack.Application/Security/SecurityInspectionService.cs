using HermesStack.Domain.Security;

namespace HermesStack.Application.Security;

public sealed class SecurityInspectionService
{
    public WorkspaceSecurityInspection Evaluate(WorkspaceSecuritySnapshot snapshot)
    {
        var findings = new List<SecurityFinding>();

        if (snapshot.DockerSocket)
        {
            findings.Add(new("HS3101", "Critical", "Docker daemon socket or pipe is exposed."));
        }

        if (snapshot.Privileged)
        {
            findings.Add(new("HS3102", "Critical", "Workspace is privileged."));
        }

        if (snapshot.Networks.Any(static value =>
            string.Equals(value, "host", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new("HS3103", "Critical", "Workspace uses the host network namespace."));
        }

        if (snapshot.HostRootMount)
        {
            findings.Add(new("HS3104", "Critical", "A host filesystem root is mounted."));
        }

        if (findings.Any(static value => value.Severity == "Critical"))
        {
            return new WorkspaceSecurityInspection(SecurityScore.Critical, snapshot, findings);
        }

        var score = SecurityScore.A;

        if (string.Equals(snapshot.User, "root", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(snapshot.User, "0", StringComparison.Ordinal))
        {
            findings.Add(new("HS3110", "D", "Workspace process runs as root."));
            score = SecurityScore.D;
        }

        if (!snapshot.CapabilitiesDropped.Contains("ALL", StringComparer.OrdinalIgnoreCase) ||
            !snapshot.SecurityOptions.Any(static value =>
                value.Contains("no-new-privileges", StringComparison.OrdinalIgnoreCase)) ||
            !snapshot.ReadOnlyRoot)
        {
            findings.Add(new(
                "HS3111",
                "C",
                "Mandatory capability/no-new-privileges/read-only-root hardening is incomplete."));
            score = Max(score, SecurityScore.C);
        }

        if (snapshot.CapabilitiesAdded.Count > 0 || snapshot.DeviceMounts.Count > 0)
        {
            findings.Add(new("HS3112", "B", "Additional capabilities or device mounts are present."));
            score = Max(score, SecurityScore.B);
        }

        if (findings.Count == 0)
        {
            findings.Add(new("HS3100", "A", "Mandatory HermesStack workspace invariants are satisfied."));
        }

        return new WorkspaceSecurityInspection(score, snapshot, findings);
    }

    private static SecurityScore Max(SecurityScore left, SecurityScore right) =>
        Rank(left) >= Rank(right) ? left : right;

    private static int Rank(SecurityScore score) => score switch
    {
        SecurityScore.A => 0,
        SecurityScore.B => 1,
        SecurityScore.C => 2,
        SecurityScore.D => 3,
        SecurityScore.Critical => 4,
        _ => 4
    };
}
