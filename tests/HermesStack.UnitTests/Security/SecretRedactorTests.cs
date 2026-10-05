using HermesStack.Application.Security;

namespace HermesStack.UnitTests.Security;

public sealed class SecretRedactorTests
{
    [Fact]
    public void Redacts_known_and_common_secret_shapes()
    {
        var sut = new SecretRedactor();
        const string explicitSecret = "company-super-secret-value";
        var output = sut.Redact(
            $"token={explicitSecret}; api=sk-1234567890abcdef; github=ghp_1234567890abcdef",
            [explicitSecret]);

        Assert.DoesNotContain(explicitSecret, output, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-1234567890abcdef", output, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_1234567890abcdef", output, StringComparison.Ordinal);
        Assert.Contains("***REDACTED***", output, StringComparison.Ordinal);
    }
}
