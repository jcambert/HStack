namespace HermesStack.Domain.Security;

public interface IHostMountPolicy
{
    MountValidationResult Classify(string hostPath);
}
