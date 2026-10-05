using System.Text.RegularExpressions;
using HermesStack.Domain.Security;

namespace HermesStack.Infrastructure.Configuration;

public sealed partial class HostMountPolicy : IHostMountPolicy
{
    private static readonly string[] ForbiddenSegments =
    [
        ".ssh", ".aws", ".azure", ".docker", ".kube", ".gnupg",
        "appdata", "documents", "desktop"
    ];

    public MountValidationResult Classify(string hostPath)
    {
        var trimmed = hostPath.Trim().Trim('"');
        var daemonProbe = trimmed.Replace('\\', '/').ToLowerInvariant();
        if (daemonProbe.Contains("docker.sock", StringComparison.Ordinal) || daemonProbe.Contains("docker_engine", StringComparison.Ordinal))
        {
            return Forbidden(trimmed, "HS3001", "Docker daemon sockets and pipes are forbidden in agent workspaces.");
        }

        if (IsWindowsPath(trimmed))
        {
            return ClassifyWindows(trimmed);
        }

        if (!trimmed.StartsWith('/', StringComparison.Ordinal))
        {
            return Forbidden(trimmed, "HS1002", "Host mount paths must be absolute.");
        }

        return ClassifyUnix(trimmed);
    }

    private static MountValidationResult ClassifyWindows(string path)
    {
        if (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return Forbidden(path, "HS3009", "UNC/network-share mounts are forbidden by the default M1 policy.");
        }

        var normalized = NormalizeWindows(path);
        if (WindowsRootRegex().IsMatch(normalized))
        {
            return Forbidden(normalized, "HS1007", "Mounting a Windows filesystem root is forbidden.");
        }

        var segments = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Skip(1).Any(segment => ForbiddenSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
        {
            return Forbidden(normalized, "HS3002", "The requested mount includes a sensitive user or system directory.");
        }

        if (segments.Length >= 2 && string.Equals(segments[1], "Users", StringComparison.OrdinalIgnoreCase) && segments.Length <= 3)
        {
            return Forbidden(normalized, "HS3003", "Mounting a Windows user profile is forbidden.");
        }

        if (segments.Length >= 2 && (string.Equals(segments[1], "Windows", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(segments[1], "Program Files", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(segments[1], "Program Files (x86)", StringComparison.OrdinalIgnoreCase)))
        {
            return Forbidden(normalized, "HS3004", "Mounting Windows system directories is forbidden.");
        }

        if (segments.Length <= 2)
        {
            return new MountValidationResult(MountClassification.Suspicious, normalized, "HS3005", "The mount is unusually broad; prefer the exact project directory.");
        }

        return Safe(normalized);
    }

    private static MountValidationResult ClassifyUnix(string path)
    {
        var normalized = NormalizeUnix(path);
        if (normalized == "/")
        {
            return Forbidden(normalized, "HS1007", "Mounting the host filesystem root is forbidden.");
        }

        var lower = normalized.ToLowerInvariant();
        if (lower == "/var/run" || lower.StartsWith("/var/run/", StringComparison.Ordinal))
        {
            return Forbidden(normalized, "HS3001", "Docker/runtime paths are forbidden in agent workspaces.");
        }

        if (lower == "/etc" || lower.StartsWith("/etc/", StringComparison.Ordinal) || lower == "/root" || lower.StartsWith("/root/", StringComparison.Ordinal))
        {
            return Forbidden(normalized, "HS3004", "Mounting host system directories is forbidden.");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(segment => ForbiddenSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
        {
            return Forbidden(normalized, "HS3002", "The requested mount includes a sensitive user directory.");
        }

        var currentHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(currentHome) &&
            currentHome.StartsWith('/', StringComparison.Ordinal) &&
            string.Equals(normalized, NormalizeUnix(currentHome), StringComparison.Ordinal))
        {
            return Forbidden(normalized, "HS3003", "Mounting the host HOME directory is forbidden.");
        }

        if (segments.Length <= 1)
        {
            return new MountValidationResult(MountClassification.Dangerous, normalized, "HS3006", "The mount exposes a top-level host directory.");
        }

        return Safe(normalized);
    }

    private static bool IsWindowsPath(string path) => WindowsAbsoluteRegex().IsMatch(path) || path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);

    private static string NormalizeWindows(string path)
    {
        var canonical = path.Replace('/', '\\');
        var drive = char.ToUpperInvariant(canonical[0]);
        var segments = NormalizeSegments(
            canonical[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return segments.Count == 0
            ? $"{drive}:\\"
            : $"{drive}:\\{string.Join('\\', segments)}";
    }

    private static string NormalizeUnix(string path)
    {
        var segments = NormalizeSegments(
            path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return segments.Count == 0 ? "/" : $"/{string.Join('/', segments)}";
    }

    private static IReadOnlyList<string> NormalizeSegments(IEnumerable<string> segments)
    {
        var normalized = new List<string>();
        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (normalized.Count > 0)
                {
                    normalized.RemoveAt(normalized.Count - 1);
                }

                continue;
            }

            normalized.Add(segment);
        }

        return normalized;
    }

    private static MountValidationResult Safe(string path) => new(MountClassification.Safe, path, "HS0000", "Mount is allowed by the default policy.");
    private static MountValidationResult Forbidden(string path, string code, string message) => new(MountClassification.Forbidden, path, code, message);

    [GeneratedRegex("^[A-Za-z]:[\\\\/]", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsAbsoluteRegex();

    [GeneratedRegex("^[A-Za-z]:\\\\?$", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsRootRegex();
}
