using HermesStack.Application.Context;
using HermesStack.Application.Security;

namespace HermesStack.UnitTests.Context;

public sealed class ContextSecretFilterTests
{
    [Fact]
    public void Api_keys_are_redacted_before_durable_context()
    {
        var filter = new ContextSecretFilter(new SecretRedactor());
        var result = filter.Filter("token=sk-1234567890abcdef");

        Assert.True(result.Changed);
        Assert.False(result.Rejected);
        Assert.DoesNotContain("sk-1234567890abcdef", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Private_keys_are_rejected()
    {
        var filter = new ContextSecretFilter(new SecretRedactor());
        var result = filter.Filter("""
            -----BEGIN PRIVATE KEY-----
            secret
            -----END PRIVATE KEY-----
            """);

        Assert.True(result.Rejected);
        Assert.Empty(result.Content);
    }
}
