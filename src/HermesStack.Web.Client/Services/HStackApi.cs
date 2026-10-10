using System.Net.Http.Json;
using System.Text.Json;
using HermesStack.Web.Contracts;

namespace HermesStack.Web.Client.Services;

// All requests are same-origin. Browser only holds an ephemeral anti-CSRF
// value; the authenticated session cookie is HttpOnly and server-owned.
public sealed class HStackApi(HttpClient client)
{
    private string? _csrf;

    private async Task EnsureSessionAsync()
    {
        if (_csrf is null)
        {
            var session = await client.GetFromJsonAsync<SessionInfo>("api/session");
            _csrf = session?.Csrf ??
                throw new InvalidOperationException("Session HStack indisponible.");
        }
    }

    public async Task<List<ProjectInfo>> ProjectsAsync() =>
        await client.GetFromJsonAsync<List<ProjectInfo>>("api/projects") ?? [];

    public async Task<DiagnosticsInfo> DiagnosticsAsync() =>
        await client.GetFromJsonAsync<DiagnosticsInfo>("api/diagnostics") ??
            throw new InvalidOperationException("Diagnostic indisponible.");

    public async Task<List<AgentInfo>> AgentsAsync(string id) =>
        await client.GetFromJsonAsync<List<AgentInfo>>($"api/projects/{Uri.EscapeDataString(id)}/agents") ?? [];

    public async Task<MemoryInfo?> MemoryAsync(string id) =>
        await client.GetFromJsonAsync<MemoryInfo>(
            $"api/projects/{Uri.EscapeDataString(id)}/memory");

    public async Task<LogsInfo?> LogsAsync(string id) =>
        await client.GetFromJsonAsync<LogsInfo>(
            $"api/projects/{Uri.EscapeDataString(id)}/logs?tail=80");

    public Task<string> ManualAsync(string topic) =>
        client.GetStringAsync($"api/help/{Uri.EscapeDataString(topic)}");

    public Task CreateAsync(string id, string hostPath, string name) =>
        ChangeAsync(HttpMethod.Post, "api/projects", new CreateProjectRequest(id, hostPath, name));

    public Task EditAsync(string id, string? hostPath, string name, string orchestrator) =>
        ChangeAsync(HttpMethod.Put, $"api/projects/{Uri.EscapeDataString(id)}",
            new EditProjectRequest(hostPath, name, orchestrator));

    public Task LifecycleAsync(string id, string action) =>
        ChangeAsync(HttpMethod.Post, $"api/projects/{Uri.EscapeDataString(id)}/lifecycle",
            new LifecycleRequest(action), $"{id}:{action}");

    public Task DeleteAsync(string id) =>
        ChangeAsync(HttpMethod.Delete, $"api/projects/{Uri.EscapeDataString(id)}",
            null, $"delete:{id}");

    public Task AddPortAsync(string id, int container, int? host) =>
        ChangeAsync(HttpMethod.Post, $"api/projects/{Uri.EscapeDataString(id)}/ports",
            new PortRequest(container, host));

    public Task RemovePortAsync(string id, int port) =>
        ChangeAsync(HttpMethod.Delete,
            $"api/projects/{Uri.EscapeDataString(id)}/ports/{port}",
            null, $"{id}:port:{port}");

    public Task BackupAsync(string id) =>
        ChangeAsync(HttpMethod.Post, $"api/projects/{Uri.EscapeDataString(id)}/backup",
            null, $"{id}:backup");

    private async Task ChangeAsync(
        HttpMethod method, string path, object? body, string? confirmation = null)
    {
        await EnsureSessionAsync();
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-HStack-CSRF", _csrf);
        if (confirmation is not null)
        {
            request.Headers.Add("X-HStack-Confirm", confirmation);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Opération refusée ({(int)response.StatusCode}). Vérifie l'état du projet et les diagnostics.");
        }
    }
}
