using HermesStack.Application.Network;
using HermesStack.Application.Orchestration;
using HermesStack.Application.Security;
using HermesStack.Domain.Network;
using HermesStack.Domain.Projects;
using HermesStack.Infrastructure.Certificates;
using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Network;

public sealed class ProxyConfigurationTests
{
    [Fact]
    public void No_proxy_merges_mandatory_entries_without_duplicates()
    {
        var proxy = ProxyConfigurationPolicy.ValidateAndNormalize(
            new ProxyConfiguration(
                true,
                "http://proxy.example:8080",
                "https://proxy.example:8443",
                ["localhost", "corp.internal"]));

        Assert.Contains("localhost", proxy.EffectiveNoProxy);
        Assert.Contains("127.0.0.1", proxy.EffectiveNoProxy);
        Assert.Contains("::1", proxy.EffectiveNoProxy);
        Assert.Contains("host.docker.internal", proxy.EffectiveNoProxy);
        Assert.Contains("corp.internal", proxy.EffectiveNoProxy);
        Assert.Equal(
            proxy.EffectiveNoProxy.Count,
            proxy.EffectiveNoProxy.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Proxy_credentials_in_yaml_are_rejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            ProxyConfigurationPolicy.ValidateAndNormalize(
                new ProxyConfiguration(
                    true,
                    "http://user:password@proxy.example:8080")));

        Assert.Contains("HS4003", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_injects_all_proxy_variable_variants()
    {
        var root = Path.Combine(Path.GetTempPath(), "hstack-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(Path.Combine(root, "hstack-home"));
            var config = new HStackConfigStore(dataRoot);
            await config.SaveAsync(new ProxyConfiguration(
                true,
                "http://proxy.example:8080",
                "https://proxy.example:8443",
                ["corp.internal"]));

            var builder = new WorkspaceDeploymentPlanBuilder(
                dataRoot,
                new HostMountValidator(new HostMountPolicy()),
                new CertificateBundleService(dataRoot),
                Path.Combine(root, "compose.yaml"),
                "hstack/workspace-full:0.4.0",
                config);

            var projectPath = OperatingSystem.IsWindows()
                ? @"C:\Dev\proxy-test"
                : "/tmp/proxy-test";
            var plan = await builder.BuildAsync(
                new ProjectDefinition("proxy-test", "Proxy Test", projectPath));

            Assert.Equal("http://proxy.example:8080", plan.Environment["HTTP_PROXY"]);
            Assert.Equal(plan.Environment["HTTP_PROXY"], plan.Environment["http_proxy"]);
            Assert.Equal("https://proxy.example:8443", plan.Environment["HTTPS_PROXY"]);
            Assert.Equal(plan.Environment["HTTPS_PROXY"], plan.Environment["https_proxy"]);
            Assert.Equal(plan.Environment["NO_PROXY"], plan.Environment["no_proxy"]);
            Assert.Contains("host.docker.internal", plan.Environment["NO_PROXY"], StringComparison.Ordinal);
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
