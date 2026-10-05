using HermesStack.Application.Security;
using HermesStack.Domain.Security;
using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Security;

namespace HermesStack.UnitTests.Security;

public sealed class SecretInjectionServiceTests
{
    [Fact]
    public async Task Only_secrets_allowed_for_project_and_agent_are_resolved()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            var secrets = new LocalProtectedSecretStore(dataRoot);
            var policies = new YamlSecretPolicyStore(dataRoot);

            await secrets.SetAsync(
                new SecretReference("project-a", "OPENAI_API_KEY"),
                new SecretValue("openai-secret"));
            await secrets.SetAsync(
                new SecretReference("project-a", "ANTHROPIC_API_KEY"),
                new SecretValue("anthropic-secret"));
            await secrets.SetAsync(
                new SecretReference("project-b", "OPENAI_API_KEY"),
                new SecretValue("other-project-secret"));

            await policies.SetAsync(
                new SecretPolicy("project-a", "OPENAI_API_KEY", ["codex"]));
            await policies.SetAsync(
                new SecretPolicy("project-a", "ANTHROPIC_API_KEY", ["claude"]));
            await policies.SetAsync(
                new SecretPolicy("project-b", "OPENAI_API_KEY", ["codex"]));

            var sut = new SecretInjectionService(secrets, policies);
            var codex = await sut.ResolveAsync("project-a", "codex");

            Assert.Single(codex);
            Assert.Equal("openai-secret", codex["OPENAI_API_KEY"]);
            Assert.DoesNotContain("ANTHROPIC_API_KEY", codex.Keys);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
