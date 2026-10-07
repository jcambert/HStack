using HermesStack.Application.Projects;
using HermesStack.Application.Security;
using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Projects;

namespace HermesStack.UnitTests.Projects;

public sealed class ProjectServiceOperationsTests
{
    [Fact]
    public async Task Project_can_be_edited_and_unregistered_without_deleting_source()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        var first = SafePath("first");
        var second = SafePath("second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);

        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            var service = new ProjectService(
                new YamlProjectStore(dataRoot),
                new HostMountValidator(new HostMountPolicy()));

            await service.AddAsync("demo", first);
            var edited = await service.EditAsync("demo", second, "Demo project");
            Assert.Equal(second, edited.HostPath);
            Assert.Equal("Demo project", edited.Name);

            await service.RemoveAsync("demo");
            Assert.Empty(await service.ListAsync());
            Assert.True(Directory.Exists(first));
            Assert.True(Directory.Exists(second));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            if (Directory.Exists(first))
            {
                Directory.Delete(first, recursive: true);
            }
            if (Directory.Exists(second))
            {
                Directory.Delete(second, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Ports_are_loopback_only_and_persisted_by_project()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        var source = SafePath("ports");
        Directory.CreateDirectory(source);

        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            var service = new ProjectService(
                new YamlProjectStore(dataRoot),
                new HostMountValidator(new HostMountPolicy()));
            await service.AddAsync("demo", source);

            var withPort = await service.AddPortAsync("demo", 5000);
            var port = Assert.Single(withPort.EffectivePorts);
            Assert.Equal(5000, port.Container);
            Assert.Equal(5000, port.EffectiveHost);
            Assert.Equal("127.0.0.1", port.Bind);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddPortAsync("demo", 6000, 5000));

            var withoutPort = await service.RemovePortAsync("demo", 5000);
            Assert.Empty(withoutPort.EffectivePorts);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            if (Directory.Exists(source))
            {
                Directory.Delete(source, recursive: true);
            }
        }
    }

    private static string SafePath(string name) =>
        OperatingSystem.IsWindows()
            ? $@"C:\Dev\hstack-tests\{Guid.NewGuid():N}\src\{name}"
            : $"/tmp/hstack-tests/{Guid.NewGuid():N}/src/{name}";
}
