using HermesStack.Application.Abstractions;

namespace HermesStack.Infrastructure.Configuration;

public sealed class DefaultDataRootProvider : IDataRootProvider
{
    public DefaultDataRootProvider(string? root = null)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var effectiveRoot = string.IsNullOrWhiteSpace(root) ? Path.Combine(home, ".hstack") : root;
        Root = Path.GetFullPath(effectiveRoot);
    }

    public string Root { get; }
    public string ConfigDirectory => Path.Combine(Root, "config");
    public string ProjectsFile => Path.Combine(ConfigDirectory, "projects.yaml");
    public string MainConfigFile => Path.Combine(ConfigDirectory, "hstack.yaml");
    public string RuntimeDirectory => Path.Combine(Root, "runtime");
    public string CertificatesDirectory => Path.Combine(Root, "certs");
    public string CorporateCertificatesDirectory => Path.Combine(CertificatesDirectory, "corporate");
    public string GeneratedCertificatesDirectory => Path.Combine(RuntimeDirectory, "certificates");
    public string GetProjectDataRoot(string projectId) => Path.Combine(Root, "data", "projects", projectId);
    public string GetProjectRuntimeRoot(string projectId) => Path.Combine(RuntimeDirectory, "projects", projectId);
}
