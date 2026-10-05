using HermesStack.Infrastructure.Certificates;
using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Certificates;

public sealed class CertificateBundleServiceTests
{
    [Fact]
    public async Task Corporate_pem_files_are_combined_in_stable_order()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            Directory.CreateDirectory(dataRoot.CorporateCertificatesDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(dataRoot.CorporateCertificatesDirectory, "b.crt"),
                "-----BEGIN CERTIFICATE-----\nBBB\n-----END CERTIFICATE-----\n");
            await File.WriteAllTextAsync(
                Path.Combine(dataRoot.CorporateCertificatesDirectory, "a.pem"),
                "-----BEGIN CERTIFICATE-----\nAAA\n-----END CERTIFICATE-----\n");

            var bundle = await new CertificateBundleService(dataRoot).BuildCorporateBundleAsync();

            Assert.NotNull(bundle);
            var content = await File.ReadAllTextAsync(bundle!);
            Assert.True(content.IndexOf("AAA", StringComparison.Ordinal) < content.IndexOf("BBB", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Non_pem_certificate_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            Directory.CreateDirectory(dataRoot.CorporateCertificatesDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataRoot.CorporateCertificatesDirectory, "bad.crt"), "not a certificate");

            await Assert.ThrowsAsync<InvalidDataException>(
                () => new CertificateBundleService(dataRoot).BuildCorporateBundleAsync());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
