using HermesStack.Domain.Security;

namespace HermesStack.Application.Security;

public sealed class HostMountValidator(IHostMountPolicy policy)
{
    public MountValidationResult Validate(string hostPath)
    {
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            return new MountValidationResult(MountClassification.Forbidden, string.Empty, "HS1001", "Host mount path is empty.");
        }

        return policy.Classify(hostPath);
    }
}
