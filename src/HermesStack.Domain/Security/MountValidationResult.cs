namespace HermesStack.Domain.Security;

public sealed record MountValidationResult(
    MountClassification Classification,
    string NormalizedPath,
    string Code,
    string Message)
{
    public bool IsAllowed => Classification is MountClassification.Safe or MountClassification.Suspicious;
}
