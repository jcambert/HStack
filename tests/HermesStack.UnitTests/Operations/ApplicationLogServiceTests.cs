using HermesStack.Application.Security;
using HermesStack.Infrastructure.Configuration;
using HermesStack.Infrastructure.Operations;

namespace HermesStack.UnitTests.Operations;

public sealed class ApplicationLogServiceTests
{
    [Fact]
    public async Task Application_log_redacts_common_secret_patterns()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "hstack-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var dataRoot = new DefaultDataRootProvider(root);
            var logger = new ApplicationLogService(dataRoot, new SecretRedactor());

            await logger.WriteAsync(
                "error",
                "test",
                "token=ghp_abcdefghijklmnopqrstuvwxyz123456");

            var log = await File.ReadAllTextAsync(
                Path.Combine(root, "logs", "hstack.log"));
            Assert.DoesNotContain(
                "ghp_abcdefghijklmnopqrstuvwxyz123456",
                log,
                StringComparison.Ordinal);
            Assert.Contains("***REDACTED***", log, StringComparison.Ordinal);
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
