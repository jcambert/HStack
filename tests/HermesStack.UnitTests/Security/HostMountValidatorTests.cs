using HermesStack.Application.Security;
using HermesStack.Domain.Security;
using HermesStack.Infrastructure.Configuration;

namespace HermesStack.UnitTests.Security;

public sealed class HostMountValidatorTests
{
    private readonly HostMountValidator _sut = new(new HostMountPolicy());

    [Theory]
    [InlineData("C:\\")]
    [InlineData("D:\\")]
    [InlineData("/")]
    [InlineData("C:\\Users\\JC\\Documents")]
    [InlineData("C:\\Users\\JC\\.ssh")]
    [InlineData("/home/jc/.ssh")]
    [InlineData("/var/run/docker.sock")]
    [InlineData("C:\\var\\run\\docker.sock")]
    [InlineData("//./pipe/docker_engine")]
    [InlineData("\\\\server\\share")]
    public void Forbidden_paths_are_rejected(string path)
    {
        var result = _sut.Validate(path);
        Assert.Equal(MountClassification.Forbidden, result.Classification);
        Assert.False(result.IsAllowed);
    }

    [Theory]
    [InlineData("C:\\Dev\\Mascara")]
    [InlineData("D:\\src\\hstack")]
    [InlineData("/home/jc/dev/mascara")]
    public void Project_directories_are_allowed(string path)
    {
        var result = _sut.Validate(path);
        Assert.Equal(MountClassification.Safe, result.Classification);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void Broad_windows_parent_is_suspicious_but_allowed()
    {
        var result = _sut.Validate("C:\\Dev");
        Assert.Equal(MountClassification.Suspicious, result.Classification);
        Assert.True(result.IsAllowed);
    }
}
