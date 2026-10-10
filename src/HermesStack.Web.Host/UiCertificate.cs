using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HermesStack.Web.Host;

internal static class UiCertificate
{
    public static X509Certificate2 Create()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=HermesStack Local UI",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature, true));
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-2),
            DateTimeOffset.UtcNow.AddHours(12));
        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx), password: null);
    }
}
