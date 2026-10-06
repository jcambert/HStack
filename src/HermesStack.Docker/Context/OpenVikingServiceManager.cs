using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using HermesStack.Application.Abstractions;
using HermesStack.Domain.Context;
using HermesStack.Domain.Security;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HermesStack.Docker.Context;

public sealed record OpenVikingConnection(
    string ProjectId,
    string AccountId,
    string UserId,
    string HostEndpoint,
    string WorkspaceEndpoint,
    string ApiKey);

public sealed record OpenVikingAdminConnection(
    string AccountId,
    string UserId,
    string HostEndpoint,
    string ApiKey);

public sealed class OpenVikingServiceManager(
    IDataRootProvider dataRoot,
    ISecretStore secretStore,
    IProcessRunner processRunner,
    string image,
    string version)
{
    private const string AccountId = "hstack";
    private const string AdminUserId = "hstack-admin";
    private const string HostEndpoint = "http://127.0.0.1:1933";
    private const string WorkspaceEndpoint = "http://openviking:1933";
    private const string RootSecretName = "OPENVIKING_ROOT_API_KEY";
    private const string AdminSecretName = "OPENVIKING_ADMIN_API_KEY";
    private const string ProjectSecretName = "OPENVIKING_API_KEY";
    private const string SystemProjectId = "_hstack";

    private readonly ISerializer _yaml = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public string Version => version;
    public string Image => image;
    public string DataDirectory => Path.Combine(dataRoot.Root, "data", "openviking");
    public string RuntimeDirectory => Path.Combine(dataRoot.RuntimeDirectory, "openviking");
    public string ComposeFile => Path.Combine(RuntimeDirectory, "compose.yaml");
    public string ServerConfigFile => Path.Combine(DataDirectory, "ov.conf");

    public async Task<ContextProviderAvailability> DetectAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode
                ? new ContextProviderAvailability(true, version, $"OpenViking {version} at {HostEndpoint}")
                : new ContextProviderAvailability(false, version, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            return new ContextProviderAvailability(false, version, exception.Message);
        }
    }

    public async Task<OpenVikingConnection> EnsureProjectAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectId(projectId);
        await EnsureServiceAsync(cancellationToken);
        _ = await EnsureAdminKeyAsync(cancellationToken);
        var userId = UserId(projectId);
        var apiKey = await EnsureProjectKeyAsync(projectId, userId, cancellationToken);
        await WriteProjectClientConfigAsync(projectId, apiKey, cancellationToken);

        return new OpenVikingConnection(
            projectId,
            AccountId,
            userId,
            HostEndpoint,
            WorkspaceEndpoint,
            apiKey);
    }

    public async Task<OpenVikingAdminConnection> GetAdminConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureServiceAsync(cancellationToken);
        var key = await EnsureAdminKeyAsync(cancellationToken);
        return new OpenVikingAdminConnection(AccountId, AdminUserId, HostEndpoint, key);
    }

    public async Task EnsureServiceAsync(CancellationToken cancellationToken = default)
    {
        var rootKey = await GetOrCreateSecretAsync(
            new SecretReference(SystemProjectId, RootSecretName),
            cancellationToken);

        await WriteServerConfigAsync(rootKey, cancellationToken);
        await WriteComposeAsync(cancellationToken);

        _ = await processRunner.RunAsync(
            new ProcessRequest(
                "docker",
                ["compose", "-p", "hstack-context", "-f", ComposeFile, "up", "-d"],
                ThrowOnError: true),
            cancellationToken);

        await WaitForHealthAsync(cancellationToken);
    }

    public async Task RunSetupAsync(CancellationToken cancellationToken = default)
    {
        var rootKey = await GetOrCreateSecretAsync(
            new SecretReference(SystemProjectId, RootSecretName),
            cancellationToken);
        await WriteServerConfigAsync(rootKey, cancellationToken);
        await WriteComposeAsync(cancellationToken);

        _ = await processRunner.RunAsync(
            new ProcessRequest(
                "docker",
                [
                    "compose", "-p", "hstack-context", "-f", ComposeFile,
                    "run", "--rm", "--entrypoint", "openviking-server",
                    "openviking", "init"
                ],
                CaptureOutput: false),
            cancellationToken);

        await WriteServerConfigAsync(rootKey, cancellationToken);
        _ = await processRunner.RunAsync(
            new ProcessRequest(
                "docker",
                ["compose", "-p", "hstack-context", "-f", ComposeFile, "up", "-d"],
                ThrowOnError: true),
            cancellationToken);
        await WaitForHealthAsync(cancellationToken);
    }

    private async Task<string> EnsureAdminKeyAsync(CancellationToken cancellationToken)
    {
        var reference = new SecretReference(SystemProjectId, AdminSecretName);
        var existing = await secretStore.GetAsync(reference, cancellationToken);
        if (existing is not null)
        {
            return existing.Value;
        }

        var rootKey = (await secretStore.GetAsync(
            new SecretReference(SystemProjectId, RootSecretName),
            cancellationToken))?.Value
            ?? throw new InvalidOperationException("OpenViking root key is not initialized.");

        using var client = CreateClient(rootKey);
        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { account_id = AccountId, admin_user_id = AdminUserId },
            cancellationToken);

        string key;
        if (createResponse.IsSuccessStatusCode)
        {
            key = await ReadUserKeyAsync(createResponse, cancellationToken);
        }
        else if (createResponse.StatusCode == HttpStatusCode.Conflict)
        {
            key = await RegenerateKeyAsync(
                client,
                AccountId,
                AdminUserId,
                cancellationToken);
        }
        else
        {
            await ThrowApiErrorAsync(createResponse, "create OpenViking account", cancellationToken);
            throw new InvalidOperationException("OpenViking account provisioning failed.");
        }

        await secretStore.SetAsync(reference, new SecretValue(key), cancellationToken);
        return key;
    }

    private async Task<string> EnsureProjectKeyAsync(
        string projectId,
        string userId,
        CancellationToken cancellationToken)
    {
        var reference = new SecretReference(projectId, ProjectSecretName);
        var existing = await secretStore.GetAsync(reference, cancellationToken);
        if (existing is not null)
        {
            return existing.Value;
        }

        var rootKey = (await secretStore.GetAsync(
            new SecretReference(SystemProjectId, RootSecretName),
            cancellationToken))?.Value
            ?? throw new InvalidOperationException("OpenViking root key is not initialized.");

        using var client = CreateClient(rootKey);
        using var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/accounts/{Uri.EscapeDataString(AccountId)}/users",
            new { user_id = userId, role = "user" },
            cancellationToken);

        string key;
        if (createResponse.IsSuccessStatusCode)
        {
            key = await ReadUserKeyAsync(createResponse, cancellationToken);
        }
        else if (createResponse.StatusCode == HttpStatusCode.Conflict)
        {
            key = await RegenerateKeyAsync(
                client,
                AccountId,
                userId,
                cancellationToken);
        }
        else
        {
            await ThrowApiErrorAsync(createResponse, "register OpenViking project user", cancellationToken);
            throw new InvalidOperationException("OpenViking project user provisioning failed.");
        }

        await secretStore.SetAsync(reference, new SecretValue(key), cancellationToken);
        return key;
    }

    private static async Task<string> RegenerateKeyAsync(
        HttpClient client,
        string accountId,
        string userId,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/accounts/{Uri.EscapeDataString(accountId)}/users/{Uri.EscapeDataString(userId)}/key",
            new { },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiErrorAsync(response, "regenerate OpenViking user key", cancellationToken);
        }

        return await ReadUserKeyAsync(response, cancellationToken);
    }

    private async Task WriteProjectClientConfigAsync(
        string projectId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataRoot.GetProjectDataRoot(projectId), "openviking");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "ovcli.conf");
        var content = JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["url"] = WorkspaceEndpoint,
                ["api_key"] = apiKey
            },
            new JsonSerializerOptions { WriteIndented = true });

        await AtomicWriteAsync(path, content + Environment.NewLine, cancellationToken);
        ProtectFile(path);
    }

    private async Task WriteServerConfigAsync(
        string rootKey,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DataDirectory);
        JsonObject document;
        if (File.Exists(ServerConfigFile))
        {
            var existing = await File.ReadAllTextAsync(ServerConfigFile, cancellationToken);
            document = JsonNode.Parse(existing) as JsonObject
                ?? throw new InvalidDataException("OpenViking ov.conf must contain a JSON object.");
        }
        else
        {
            document = new JsonObject();
        }

        var server = document["server"] as JsonObject ?? new JsonObject();
        server["host"] = "0.0.0.0";
        server["port"] = 1933;
        server["auth_mode"] = "api_key";
        server["root_api_key"] = rootKey;
        document["server"] = server;

        var storage = document["storage"] as JsonObject ?? new JsonObject();
        storage["workspace"] ??= "./data";
        document["storage"] = storage;

        var content = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await AtomicWriteAsync(ServerConfigFile, content + Environment.NewLine, cancellationToken);
        ProtectFile(ServerConfigFile);
    }

    private async Task WriteComposeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RuntimeDirectory);
        Directory.CreateDirectory(DataDirectory);

        var service = new Dictionary<string, object?>
        {
            ["image"] = image,
            ["container_name"] = "hstack-memory-openviking",
            ["restart"] = "unless-stopped",
            ["ports"] = new[] { "127.0.0.1:1933:1933" },
            ["volumes"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "bind",
                    ["source"] = DataDirectory,
                    ["target"] = "/app/.openviking"
                }
            },
            ["security_opt"] = new[] { "no-new-privileges:true" },
            ["cap_drop"] = new[] { "ALL" },
            ["pids_limit"] = 512,
            ["networks"] = new[] { "context" },
            ["labels"] = new Dictionary<string, string>
            {
                ["io.hstack.managed"] = "true",
                ["io.hstack.kind"] = "context",
                ["io.hstack.integration"] = "openviking",
                ["io.hstack.version"] = version
            }
        };

        var document = new Dictionary<string, object>
        {
            ["services"] = new Dictionary<string, object?> { ["openviking"] = service },
            ["networks"] = new Dictionary<string, object?>
            {
                ["context"] = new Dictionary<string, object>
                {
                    ["name"] = "hstack-context",
                    ["driver"] = "bridge"
                }
            }
        };

        await AtomicWriteAsync(
            ComposeFile,
            _yaml.Serialize(document),
            cancellationToken);
    }

    private async Task WaitForHealthAsync(CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        Exception? last = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync("/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                last = new HttpRequestException($"OpenViking health returned {(int)response.StatusCode}.");
            }
            catch (HttpRequestException exception)
            {
                last = exception;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new InvalidOperationException(
            $"OpenViking did not become healthy at {HostEndpoint}.",
            last);
    }

    private HttpClient CreateClient(string? apiKey = null)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(HostEndpoint),
            Timeout = TimeSpan.FromSeconds(30)
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }

        return client;
    }

    private async Task<string> GetOrCreateSecretAsync(
        SecretReference reference,
        CancellationToken cancellationToken)
    {
        var existing = await secretStore.GetAsync(reference, cancellationToken);
        if (existing is not null)
        {
            return existing.Value;
        }

        var value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await secretStore.SetAsync(reference, new SecretValue(value), cancellationToken);
        return value;
    }

    private static async Task<string> ReadUserKeyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (json.RootElement.TryGetProperty("result", out var result) &&
            result.TryGetProperty("user_key", out var userKey) &&
            !string.IsNullOrWhiteSpace(userKey.GetString()))
        {
            return userKey.GetString()!;
        }

        throw new InvalidDataException("OpenViking admin response did not contain result.user_key.");
    }

    internal static async Task ThrowApiErrorAsync(
        HttpResponseMessage response,
        string action,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"OpenViking could not {action}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
    }

    private static async Task AtomicWriteAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content, cancellationToken);
        File.Move(temp, path, true);
    }

    private static void ProtectFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static string UserId(string projectId)
    {
        var chars = projectId
            .Trim()
            .ToLowerInvariant()
            .Select(static character =>
                char.IsAsciiLetterOrDigit(character) || character == '-'
                    ? character
                    : '-')
            .ToArray();
        var normalized = new string(chars).Trim('-');
        return "project-" + (normalized.Length == 0 ? "workspace" : normalized);
    }

    private static void ValidateProjectId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }
    }
}
