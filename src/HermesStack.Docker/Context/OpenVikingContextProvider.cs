using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HermesStack.Application.Abstractions;
using HermesStack.Application.Context;
using HermesStack.Domain.Context;

namespace HermesStack.Docker.Context;

public sealed class OpenVikingContextProvider(
    OpenVikingServiceManager manager,
    IContextScopeMapper scopeMapper) : IContextProvider, IContextScopeMapper
{
    public string Id => "openviking";
    public string DisplayName => "OpenViking";

    public Task<ContextProviderAvailability> DetectAsync(
        CancellationToken cancellationToken = default) =>
        manager.DetectAsync(cancellationToken);

    public async Task<ContextProviderProjectInfo> EnsureProjectAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var connection = await manager.EnsureProjectAsync(projectId, cancellationToken);
        return new ContextProviderProjectInfo(
            Id,
            projectId,
            connection.AccountId,
            connection.UserId,
            connection.WorkspaceEndpoint,
            true);
    }

    public async Task<ContextSession> OpenSessionAsync(
        ContextSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var connection = await manager.EnsureProjectAsync(request.ProjectId, cancellationToken);
        using var client = CreateClient(connection.HostEndpoint, connection.ApiKey);
        var body = request.SessionId is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?> { ["session_id"] = request.SessionId };

        using var response = await client.PostAsJsonAsync(
            "/api/v1/sessions",
            body,
            cancellationToken);
        await EnsureSuccessAsync(response, "open a context session", cancellationToken);

        using var json = await ParseAsync(response, cancellationToken);
        var id = FindString(json.RootElement, "session_id")
            ?? request.SessionId
            ?? throw new InvalidDataException("OpenViking did not return a session id.");
        return new ContextSession(id, request.ProjectId, request.AgentId);
    }

    public async Task<IReadOnlyList<ContextItem>> RetrieveAsync(
        ContextQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = await manager.EnsureProjectAsync(query.ProjectId, cancellationToken);
        using var client = CreateClient(connection.HostEndpoint, connection.ApiKey);
        var targetUri = scopeMapper.GetSearchRoot(query);
        using var response = await client.PostAsJsonAsync(
            "/api/v1/search/find",
            new
            {
                query = query.Query,
                target_uri = targetUri,
                limit = Math.Max(1, query.MaxItems ?? 20)
            },
            cancellationToken);

        await EnsureSuccessAsync(response, "search context", cancellationToken);
        using var json = await ParseAsync(response, cancellationToken);
        var items = new List<ContextItem>();
        CollectItems(json.RootElement, query.Scope, items);

        return items
            .GroupBy(static item => item.Uri, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(item => item.Score ?? double.MinValue).First())
            .OrderByDescending(static item => item.Score ?? double.MinValue)
            .Take(Math.Max(1, query.MaxItems ?? 20))
            .ToArray();
    }

    public async Task StoreAsync(
        ContextWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var connection = await manager.EnsureProjectAsync(request.ProjectId, cancellationToken);
        using var client = CreateClient(connection.HostEndpoint, connection.ApiKey);
        var uri = scopeMapper.GetWriteUri(request);
        if (request.Scope == ContextScope.Shared)
        {
            await EnsureRestrictedSharedNamespaceAsync(uri, cancellationToken);
        }

        using var response = await client.PostAsJsonAsync(
            "/api/v1/content/write",
            new
            {
                uri,
                content = request.Content,
                mode = "replace",
                wait = false,
                tags = request.Tags
            },
            cancellationToken);
        await EnsureSuccessAsync(response, "write context", cancellationToken);
    }

    public async Task ShareAsync(
        ContextShareRequest request,
        CancellationToken cancellationToken = default)
    {
        var owner = await manager.EnsureProjectAsync(request.ProjectId, cancellationToken);
        var targetProjects = new HashSet<string>(
            request.ProjectIds.Append(request.ProjectId),
            StringComparer.OrdinalIgnoreCase);

        var users = new List<(string UserId, bool Owner)>();
        foreach (var projectId in targetProjects)
        {
            var connection = await manager.EnsureProjectAsync(projectId, cancellationToken);
            users.Add((connection.UserId, string.Equals(projectId, request.ProjectId, StringComparison.OrdinalIgnoreCase)));
        }

        var admin = await manager.GetAdminConnectionAsync(cancellationToken);
        using var client = CreateClient(admin.HostEndpoint, admin.ApiKey);
        var uri = scopeMapper.GetSharedNamespaceUri(request.Namespace);
        var entries = users.Select(user => new
        {
            principal = $"user:{user.UserId}",
            level = user.Owner || request.AllowWrite ? "write" : "read"
        }).ToArray();

        using var mkdirResponse = await client.PostAsJsonAsync(
            "/api/v1/fs/mkdir",
            new
            {
                uri,
                description = $"HermesStack shared context: {request.Namespace}",
                acl = new
                {
                    acl_mode = "restricted",
                    entries
                }
            },
            cancellationToken);

        if (!mkdirResponse.IsSuccessStatusCode &&
            mkdirResponse.StatusCode != HttpStatusCode.Conflict)
        {
            await OpenVikingServiceManager.ThrowApiErrorAsync(
                mkdirResponse,
                "create shared context namespace",
                cancellationToken);
        }

        using var aclRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/acl")
        {
            Content = JsonContent.Create(new
            {
                uri,
                acl_mode = "restricted",
                entries
            })
        };
        using var aclResponse = await client.SendAsync(aclRequest, cancellationToken);
        await EnsureSuccessAsync(aclResponse, "apply shared context ACL", cancellationToken);
        _ = owner;
    }

    public async Task ExportAsync(
        string projectId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var connection = await manager.EnsureProjectAsync(projectId, cancellationToken);
        using var client = CreateClient(connection.HostEndpoint, connection.ApiKey);
        using var response = await client.PostAsJsonAsync(
            "/api/v1/pack/export",
            new
            {
                uri = $"viking://user/{connection.UserId}/",
                include_vectors = false
            },
            cancellationToken);
        await EnsureSuccessAsync(response, "export project context", cancellationToken);

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(fullPath);
        await input.CopyToAsync(output, cancellationToken);
    }

    public async Task ImportAsync(
        string projectId,
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("OpenViking .ovpack file was not found.", fullPath);
        }

        var connection = await manager.EnsureProjectAsync(projectId, cancellationToken);
        using var client = CreateClient(connection.HostEndpoint, connection.ApiKey);

        await using var file = File.OpenRead(fullPath);
        using var upload = new MultipartFormDataContent();
        using var fileContent = new StreamContent(file);
        upload.Add(fileContent, "file", Path.GetFileName(fullPath));
        using var uploadResponse = await client.PostAsync(
            "/api/v1/resources/temp_upload",
            upload,
            cancellationToken);
        await EnsureSuccessAsync(uploadResponse, "upload context archive", cancellationToken);

        using var uploadJson = await ParseAsync(uploadResponse, cancellationToken);
        var tempFileId = FindString(uploadJson.RootElement, "temp_file_id")
            ?? throw new InvalidDataException("OpenViking did not return a temp_file_id.");

        using var importResponse = await client.PostAsJsonAsync(
            "/api/v1/pack/import",
            new
            {
                temp_file_id = tempFileId,
                parent = $"viking://user/{connection.UserId}/",
                on_conflict = "fail",
                vector_mode = "auto"
            },
            cancellationToken);
        await EnsureSuccessAsync(importResponse, "import project context", cancellationToken);
    }

    private async Task EnsureRestrictedSharedNamespaceAsync(
        string itemUri,
        CancellationToken cancellationToken)
    {
        const string prefix = "viking://resources/hstack-shared/";
        if (!itemUri.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Shared context target is outside the HermesStack shared root.");
        }

        var relative = itemUri[prefix.Length..].Trim('/');
        var separator = relative.IndexOf('/');
        if (separator <= 0)
        {
            throw new InvalidOperationException(
                "Shared context writes require an existing explicit namespace.");
        }

        var namespaceUri = prefix + relative[..separator] + "/";
        var admin = await manager.GetAdminConnectionAsync(cancellationToken);
        using var client = CreateClient(admin.HostEndpoint, admin.ApiKey);
        using var response = await client.GetAsync(
            $"/api/v1/acl?uri={Uri.EscapeDataString(namespaceUri)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Shared context namespace '{namespaceUri}' does not exist. Create it with 'hstack memory share' first.");
        }

        await EnsureSuccessAsync(response, "inspect shared context ACL", cancellationToken);
        using var json = await ParseAsync(response, cancellationToken);
        var mode = FindString(json.RootElement, "acl_mode");
        if (!string.Equals(mode, "restricted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Shared context namespace '{namespaceUri}' is not restricted; refusing to write.");
        }
    }

    public string GetSearchRoot(ContextQuery query) => scopeMapper.GetSearchRoot(query);
    public string GetWriteUri(ContextWriteRequest request) => scopeMapper.GetWriteUri(request);
    public string GetSharedNamespaceUri(string @namespace) => scopeMapper.GetSharedNamespaceUri(@namespace);

    private static HttpClient CreateClient(string endpoint, string apiKey)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(endpoint),
            Timeout = TimeSpan.FromMinutes(2)
        };
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        return client;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string action,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await OpenVikingServiceManager.ThrowApiErrorAsync(
                response,
                action,
                cancellationToken);
        }
    }

    private static async Task<JsonDocument> ParseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static void CollectItems(
        JsonElement element,
        ContextScope scope,
        ICollection<ContextItem> items)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("uri", out var uriValue) &&
                uriValue.ValueKind == JsonValueKind.String &&
                uriValue.GetString() is string uri)
            {
                var content =
                    GetString(element, "abstract") ??
                    GetString(element, "overview") ??
                    GetString(element, "content") ??
                    GetString(element, "text") ??
                    uri;
                var score = GetDouble(element, "score");
                items.Add(new ContextItem(
                    uri,
                    scope,
                    content,
                    ContextBudgetPolicy.EstimateTokens(content),
                    score,
                    AlreadySummarized: element.TryGetProperty("abstract", out _) ||
                                       element.TryGetProperty("overview", out _)));
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectItems(property.Value, scope, items);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                CollectItems(child, scope, items);
            }
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number)
            ? number
            : null;

    private static string? FindString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            foreach (var property in element.EnumerateObject())
            {
                var found = FindString(property.Value, name);
                if (found is not null)
                {
                    return found;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var found = FindString(child, name);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
