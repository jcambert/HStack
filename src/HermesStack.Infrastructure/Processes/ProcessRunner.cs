using System.Diagnostics;
using HermesStack.Application.Abstractions;

namespace HermesStack.Infrastructure.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.Executable,
            UseShellExecute = false,
            RedirectStandardOutput = request.CaptureOutput,
            RedirectStandardError = request.CaptureOutput,
            RedirectStandardInput = false,
            WorkingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory
        };

        foreach (var argument in request.Arguments) startInfo.ArgumentList.Add(argument);
        if (request.Environment is not null)
        {
            foreach (var (key, value) in request.Environment) startInfo.Environment[key] = value;
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start()) throw new InvalidOperationException($"Failed to start process '{request.Executable}'.");

        using var cancellationRegistration = cancellationToken.Register(static state =>
        {
            var target = (Process)state!;
            try
            {
                if (!target.HasExited) target.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }, process);

        var stdoutTask = request.CaptureOutput ? process.StandardOutput.ReadToEndAsync(cancellationToken) : Task.FromResult(string.Empty);
        var stderrTask = request.CaptureOutput ? process.StandardError.ReadToEndAsync(cancellationToken) : Task.FromResult(string.Empty);

        await process.WaitForExitAsync(cancellationToken);
        var result = new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
        if (request.ThrowOnError && !result.IsSuccess)
        {
            throw new InvalidOperationException($"Process '{request.Executable}' failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        return result;
    }
}
