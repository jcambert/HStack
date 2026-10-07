using System.ComponentModel;
using System.Text.RegularExpressions;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Orchestration;

namespace HermesStack.Aspire;

public sealed class AspireWorkspaceOrchestrator(
    IProcessRunner processRunner,
    AspireDeploymentPlanWriter writer,
    AspireOrchestratorCapabilityEvaluator capabilityEvaluator,
    string expectedVersion,
    IWorkspaceContextManager? contextManager = null) : IWorkspaceOrchestrator
{
    public string Id => "aspire";
    public string DisplayName => "Aspire";

    public async Task<OrchestratorAvailability> DetectAsync(
        CancellationToken cancellationToken = default)
    {
        ProcessResult aspire;
        try
        {
            aspire = await processRunner.RunAsync(
                new ProcessRequest("aspire", ["--version"]),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is Win32Exception or FileNotFoundException)
        {
            return new OrchestratorAvailability(
                false,
                Reason: "Aspire CLI is not installed or not available on PATH.");
        }

        if (!aspire.IsSuccess)
        {
            return new OrchestratorAvailability(
                false,
                Reason: aspire.StandardError.Trim());
        }

        var detected = aspire.StandardOutput.Trim();
        if (!detected.Contains(expectedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new OrchestratorAvailability(
                false,
                detected,
                $"HermesStack pins Aspire {expectedVersion}; detected {detected}. Run the managed Aspire installation/update path before selecting this backend.");
        }

        var dotnet = await processRunner.RunAsync(
            new ProcessRequest("dotnet", ["--version"]),
            cancellationToken);
        if (!dotnet.IsSuccess ||
            !Version.TryParse(
                dotnet.StandardOutput.Trim().Split('-', 2)[0],
                out var sdk) ||
            sdk.Major < 10)
        {
            return new OrchestratorAvailability(
                false,
                detected,
                ".NET SDK 10 or newer is required by the HermesStack C# Aspire AppHost.");
        }

        var runtime = await processRunner.RunAsync(
            new ProcessRequest("docker", ["version", "--format", "{{.Server.Version}}"]),
            cancellationToken);
        if (!runtime.IsSuccess)
        {
            return new OrchestratorAvailability(
                false,
                detected,
                "Docker/OCI container runtime is unavailable.");
        }

        return new OrchestratorAvailability(true, detected);
    }

    public async Task<WorkspaceDeploymentPreview> PreviewAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var report = await capabilityEvaluator.EvaluateAsync(
            plan,
            cancellationToken);
        if (!report.IsSupported)
        {
            throw CapabilityException(report);
        }

        var files = await writer.WriteAsync(plan, cancellationToken);
        var summary =
            $"AppHost={files.AppHostProjectFile}{Environment.NewLine}" +
            $"Deployment={files.DeploymentFile}{Environment.NewLine}" +
            $"Image={plan.WorkspaceImage}{Environment.NewLine}" +
            $"Mounts={plan.Mounts.Count}; Ports={plan.Ports.Count}; " +
            "Security=no-new-privileges,cap-drop=ALL,read-only-root,loopback-ports";
        return new WorkspaceDeploymentPreview(DisplayName, summary);
    }

    public async Task UpAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var report = await capabilityEvaluator.EvaluateAsync(
            plan,
            cancellationToken);
        if (!report.IsSupported)
        {
            throw CapabilityException(report);
        }

        if (plan.Context?.Enabled == true)
        {
            if (contextManager is null)
            {
                throw new InvalidOperationException(
                    "OpenViking context is enabled but no context manager is registered for Aspire.");
            }

            await contextManager.EnsureWorkspaceReadyAsync(
                plan.Project.Id,
                cancellationToken);
        }

        var availability = await DetectAsync(cancellationToken);
        if (!availability.IsAvailable)
        {
            throw new InvalidOperationException(
                $"HS2101: Aspire backend is unavailable: {availability.Reason}");
        }

        var files = await writer.WriteAsync(plan, cancellationToken);
        var current = await GetStatusAsync(plan, cancellationToken);
        if (current.State == WorkspaceState.Running)
        {
            return;
        }

        var environment = AppHostEnvironment(files);
        var start = await processRunner.RunAsync(
            new ProcessRequest(
                "aspire",
                [
                    "start",
                    "--apphost", files.AppHostProjectFile,
                    "--format", "Json",
                    "--non-interactive"
                ],
                WorkingDirectory: files.RuntimeDirectory,
                Environment: environment),
            cancellationToken);
        if (!start.IsSuccess)
        {
            throw new InvalidOperationException(
                $"HS2102: Aspire AppHost failed to start: {start.StandardError.Trim()}");
        }

        await File.WriteAllTextAsync(
            files.StartResultFile,
            start.StandardOutput,
            cancellationToken);

        var wait = await processRunner.RunAsync(
            new ProcessRequest(
                "aspire",
                [
                    "wait", "workspace",
                    "--apphost", files.AppHostProjectFile,
                    "--status", "up",
                    "--timeout", "120",
                    "--non-interactive"
                ],
                WorkingDirectory: files.RuntimeDirectory,
                Environment: environment),
            cancellationToken);
        if (!wait.IsSuccess)
        {
            await StopBestEffortAsync(files, cancellationToken);
            throw new InvalidOperationException(
                $"HS2103: Aspire workspace did not become ready: {wait.StandardError.Trim()}");
        }

        if (plan.Context?.Enabled == true)
        {
            await ConnectContextNetworkAsync(plan.Project.Id, cancellationToken);
        }
    }

    public async Task DownAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var files = writer.GetFiles(plan.Project.Id);
        if (!File.Exists(files.AppHostProjectFile))
        {
            return;
        }

        var status = await GetStatusAsync(plan, cancellationToken);
        if (status.State == WorkspaceState.Stopped)
        {
            return;
        }

        var result = await processRunner.RunAsync(
            new ProcessRequest(
                "aspire",
                [
                    "stop",
                    "--apphost", files.AppHostProjectFile,
                    "--non-interactive"
                ],
                WorkingDirectory: files.RuntimeDirectory,
                Environment: AppHostEnvironment(files)),
            cancellationToken);
        if (!result.IsSuccess &&
            !result.StandardError.Contains(
                "No running AppHost",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"HS2104: Aspire AppHost failed to stop: {result.StandardError.Trim()}");
        }
    }

    public async Task RestartAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(plan, cancellationToken);
        if (status.State != WorkspaceState.Running)
        {
            await UpAsync(plan, cancellationToken);
            return;
        }

        var files = writer.GetFiles(plan.Project.Id);
        var result = await processRunner.RunAsync(
            new ProcessRequest(
                "aspire",
                [
                    "resource", "workspace", "restart",
                    "--apphost", files.AppHostProjectFile,
                    "--non-interactive"
                ],
                WorkingDirectory: files.RuntimeDirectory,
                Environment: AppHostEnvironment(files)),
            cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"HS2105: Aspire workspace restart failed: {result.StandardError.Trim()}");
        }

        var wait = await processRunner.RunAsync(
            new ProcessRequest(
                "aspire",
                [
                    "wait", "workspace",
                    "--apphost", files.AppHostProjectFile,
                    "--status", "up",
                    "--timeout", "120",
                    "--non-interactive"
                ],
                WorkingDirectory: files.RuntimeDirectory,
                Environment: AppHostEnvironment(files)),
            cancellationToken);
        if (!wait.IsSuccess)
        {
            throw new InvalidOperationException(
                $"HS2103: Aspire workspace did not become ready after restart: {wait.StandardError.Trim()}");
        }

        if (plan.Context?.Enabled == true)
        {
            await ConnectContextNetworkAsync(plan.Project.Id, cancellationToken);
        }
    }

    public async Task<WorkspaceStatus> GetStatusAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        var files = writer.GetFiles(plan.Project.Id);
        if (!File.Exists(files.AppHostProjectFile))
        {
            return new WorkspaceStatus(WorkspaceState.Stopped);
        }

        try
        {
            var result = await processRunner.RunAsync(
                new ProcessRequest(
                    "aspire",
                    [
                        "wait", "workspace",
                        "--apphost", files.AppHostProjectFile,
                        "--status", "up",
                        "--timeout", "1",
                        "--non-interactive"
                    ],
                    WorkingDirectory: files.RuntimeDirectory,
                    Environment: AppHostEnvironment(files)),
                cancellationToken);

            if (result.IsSuccess)
            {
                var dashboard = await GetDashboardUrlAsync(
                    plan.Project.Id,
                    cancellationToken);
                return new WorkspaceStatus(
                    WorkspaceState.Running,
                    dashboard is null ? "Aspire" : $"Aspire Dashboard: {dashboard}");
            }

            return result.ExitCode is 7 or 17
                ? new WorkspaceStatus(WorkspaceState.Stopped)
                : new WorkspaceStatus(
                    WorkspaceState.Unknown,
                    result.StandardError.Trim());
        }
        catch (Exception exception) when (
            exception is Win32Exception or FileNotFoundException)
        {
            return new WorkspaceStatus(
                WorkspaceState.Unknown,
                exception.Message);
        }
    }

    public Task<int> ExecAsync(
        WorkspaceExecutionRequest request,
        CancellationToken cancellationToken = default) =>
        ExecInternalAsync(request, capture: false, cancellationToken)
            .ContinueWith(
                static task => task.Result.ExitCode,
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

    public Task<WorkspaceExecutionResult> ExecCaptureAsync(
        WorkspaceExecutionRequest request,
        CancellationToken cancellationToken = default) =>
        ExecInternalAsync(request, capture: true, cancellationToken);

    public async Task<int> StreamLogsAsync(
        WorkspaceLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var files = writer.GetFiles(request.Plan.Project.Id);
        if (!File.Exists(files.AppHostProjectFile))
        {
            return 0;
        }

        var args = new List<string>
        {
            "logs",
            "workspace",
            "--apphost", files.AppHostProjectFile,
            "--non-interactive"
        };
        if (request.Follow)
        {
            args.Add("--follow");
        }

        if (request.Tail is int tail)
        {
            args.Add("--tail");
            args.Add(tail.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var result = await processRunner.RunAsync(
            new ProcessRequest(
                "aspire",
                args,
                WorkingDirectory: files.RuntimeDirectory,
                Environment: AppHostEnvironment(files),
                CaptureOutput: false),
            cancellationToken);
        return result.ExitCode;
    }

    public Task<OrchestratorCapabilityReport> InspectCapabilitiesAsync(
        WorkspaceDeploymentPlan plan,
        CancellationToken cancellationToken = default) =>
        capabilityEvaluator.EvaluateAsync(plan, cancellationToken);

    public Task<ProcessResult> DoctorAsync(
        CancellationToken cancellationToken = default) =>
        processRunner.RunAsync(
            new ProcessRequest(
                "aspire",
                ["doctor", "--format", "Json", "--non-interactive"]),
            cancellationToken);

    public async Task<string?> GetDashboardUrlAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var files = writer.GetFiles(projectId);
        if (File.Exists(files.StartResultFile))
        {
            var persisted = await File.ReadAllTextAsync(
                files.StartResultFile,
                cancellationToken);
            var url = DashboardUrl(persisted);
            if (url is not null)
            {
                return url;
            }
        }

        if (!File.Exists(files.AppHostProjectFile))
        {
            return null;
        }

        var result = await processRunner.RunAsync(
            new ProcessRequest(
                "aspire",
                ["ps", "--format", "Json", "--non-interactive"],
                WorkingDirectory: files.RuntimeDirectory),
            cancellationToken);
        return result.IsSuccess ? DashboardUrl(result.StandardOutput) : null;
    }

    public AspireRuntimeFiles GetRuntimeFiles(string projectId) =>
        writer.GetFiles(projectId);

    private async Task<WorkspaceExecutionResult> ExecInternalAsync(
        WorkspaceExecutionRequest request,
        bool capture,
        CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(request.Plan, cancellationToken);
        if (status.State != WorkspaceState.Running)
        {
            throw new InvalidOperationException(
                $"Workspace '{request.Plan.Project.Id}' is not running under Aspire.");
        }

        var args = new List<string> { "exec" };
        if (request.Detached)
        {
            args.Add("-d");
        }

        if (request.Interactive && !capture)
        {
            args.Add("-i");
            args.Add("-t");
        }

        foreach (var key in request.EffectiveEnvironment.Keys
            .OrderBy(static value => value, StringComparer.Ordinal))
        {
            args.Add("-e");
            args.Add(key);
        }

        args.Add($"hstack-{request.Plan.Project.Id}-workspace");
        args.AddRange(request.Command);

        var result = await processRunner.RunAsync(
            new ProcessRequest(
                "docker",
                args,
                Environment: request.EffectiveEnvironment.ToDictionary(
                    static item => item.Key,
                    static item => (string?)item.Value,
                    StringComparer.Ordinal),
                CaptureOutput: capture || !request.Interactive || request.Detached),
            cancellationToken);

        return new WorkspaceExecutionResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError);
    }

    private async Task ConnectContextNetworkAsync(
        string projectId,
        CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new ProcessRequest(
                "docker",
                [
                    "network", "connect",
                    "hstack-context",
                    $"hstack-{projectId}-workspace"
                ]),
            cancellationToken);

        if (!result.IsSuccess &&
            !result.StandardError.Contains(
                "already exists",
                StringComparison.OrdinalIgnoreCase) &&
            !result.StandardError.Contains(
                "already connected",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"HS2106: Could not attach Aspire workspace to the OpenViking context network: {result.StandardError.Trim()}");
        }
    }

    private async Task StopBestEffortAsync(
        AspireRuntimeFiles files,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await processRunner.RunAsync(
                new ProcessRequest(
                    "aspire",
                    [
                        "stop",
                        "--apphost", files.AppHostProjectFile,
                        "--non-interactive"
                    ],
                    WorkingDirectory: files.RuntimeDirectory,
                    Environment: AppHostEnvironment(files)),
                cancellationToken);
        }
        catch
        {
        }
    }

    private static IReadOnlyDictionary<string, string?> AppHostEnvironment(
        AspireRuntimeFiles files) =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["HSTACK_ASPIRE_DEPLOYMENT"] = files.DeploymentFile,
            ["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "false"
        };

    private static InvalidOperationException CapabilityException(
        OrchestratorCapabilityReport report) =>
        new(
            "HS2107: Aspire cannot preserve the mandatory HermesStack deployment policy. " +
            $"Missing: {string.Join(", ", report.MissingCapabilities)}. " +
            "Use Docker Compose for this project until the required Aspire capability is available.");

    private static string? DashboardUrl(string content)
    {
        var match = Regex.Match(
            content,
            @"https?://(?:localhost|127\.0\.0\.1|\[::1\])[^\s\""']*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success
            ? match.Value.TrimEnd(',', '}', ']')
            : null;
    }
}
