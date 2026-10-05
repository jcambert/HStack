namespace HermesStack.Application.Abstractions;

public interface ICertificateBundleService
{
    Task<string?> BuildCorporateBundleAsync(CancellationToken cancellationToken = default);
}
