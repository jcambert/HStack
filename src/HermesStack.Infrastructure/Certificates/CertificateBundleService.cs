using System.Text;
using HermesStack.Application.Abstractions;

namespace HermesStack.Infrastructure.Certificates;

public sealed class CertificateBundleService(IDataRootProvider dataRoot) : ICertificateBundleService
{
    private static readonly string[] Extensions = ["*.crt", "*.pem", "*.cer"];

    public async Task<string?> BuildCorporateBundleAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(dataRoot.CorporateCertificatesDirectory);
        Directory.CreateDirectory(dataRoot.GeneratedCertificatesDirectory);

        var files = Extensions
            .SelectMany(pattern => Directory.EnumerateFiles(dataRoot.CorporateCertificatesDirectory, pattern, SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            return null;
        }

        var target = Path.Combine(dataRoot.GeneratedCertificatesDirectory, "corporate-ca-bundle.crt");
        var builder = new StringBuilder();
        foreach (var file in files)
        {
            var text = await File.ReadAllTextAsync(file, cancellationToken);
            if (!text.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Certificate file is not PEM encoded: {file}");
            }

            builder.AppendLine(text.Trim());
        }

        await File.WriteAllTextAsync(target, builder.ToString(), cancellationToken);
        return target;
    }
}
