using HermesStack.Application.Abstractions;
using HermesStack.Domain.Security;

namespace HermesStack.Application.Security;

public sealed class SecretInjectionService(
    ISecretStore secretStore,
    ISecretPolicyStore policyStore)
{
    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string projectId,
        string agentId,
        CancellationToken cancellationToken = default)
    {
        var policies = await policyStore.FindForAgentAsync(
            projectId,
            agentId,
            cancellationToken);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var policy in policies.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            var value = await secretStore.GetAsync(
                new SecretReference(projectId, policy.Name),
                cancellationToken);
            if (value is not null)
            {
                result[policy.Name] = value.Value;
            }
        }

        return result;
    }
}
