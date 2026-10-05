using HermesStack.Application.Abstractions;
using HermesStack.Infrastructure.Processes;

namespace HermesStack.IntegrationTests;

public sealed class DockerComposeConfigTests
{
    private readonly ProcessRunner _processRunner = new();

    [Fact]
    [Trait("Category", "Docker")]
    public async Task Base_compose_is_valid_when_docker_is_available()
    {
        if (!await DockerComposeAvailableAsync()) return;

        var repoRoot = FindRepoRoot();
        var composeFile = Path.Combine(repoRoot, "docker", "compose", "compose.yaml");
        var result = await _processRunner.RunAsync(new ProcessRequest(
            "docker",
            ["compose", "-f", composeFile, "config"]));

        Assert.True(result.IsSuccess, result.StandardError);
    }

    private async Task<bool> DockerComposeAvailableAsync()
    {
        try
        {
            var result = await _processRunner.RunAsync(new ProcessRequest(
                "docker",
                ["compose", "version"]));
            return result.IsSuccess;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HermesStack.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
