namespace HermesStack.Application.Abstractions;

public interface IDataRootProvider
{
    string Root { get; }
    string ConfigDirectory { get; }
    string ProjectsFile { get; }
    string MainConfigFile { get; }
    string RuntimeDirectory { get; }
    string CertificatesDirectory { get; }
    string CorporateCertificatesDirectory { get; }
    string GeneratedCertificatesDirectory { get; }
    string GetProjectDataRoot(string projectId);
    string GetProjectRuntimeRoot(string projectId);
}
