using System.Diagnostics;
using System.Net;
using HermesStack.Web.Contracts;
using HermesStack.Web.Host;

var port = 5158;
var portIndex = Array.IndexOf(args, "--port");
if (portIndex >= 0)
{
    if (portIndex + 1 >= args.Length ||
        !int.TryParse(args[portIndex + 1], out port) ||
        port is < 1024 or > 65535)
    {
        throw new ArgumentException("--port requires an integer between 1024 and 65535.");
    }
}

using var certificate = UiCertificate.Create();
var builder = WebApplication.CreateBuilder(Array.Empty<string>());
builder.WebHost.ConfigureKestrel(options =>
{
    // A browser on the same PC is the only supported client. Never use
    // localhost wildcard hostnames or an ASPNETCORE_URLS LAN override.
    options.Listen(IPAddress.Loopback, port, listen => listen.UseHttps(certificate));
    options.Limits.MaxRequestBodySize = 64 * 1024;
});
builder.Services.AddSingleton<UiSessionSecurity>();
builder.Services.AddSingleton<HStackWebRuntime>();
var app = builder.Build();
var security = app.Services.GetRequiredService<UiSessionSecurity>();
var host = app.Services.GetRequiredService<HStackWebRuntime>();
var expectedPort = port;

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-store";
    ctx.Response.Headers["Pragma"] = "no-cache";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; base-uri 'none'; object-src 'none'; " +
        "frame-ancestors 'none'; form-action 'self'; connect-src 'self'; " +
        "img-src 'self' data:; font-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'";

    if (!UiHostPolicy.ValidHost(ctx, expectedPort) ||
        !UiHostPolicy.ValidOrigin(ctx, expectedPort))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    if (ctx.Request.Path == "/unlock" && HttpMethods.IsPost(ctx.Request.Method))
    {
        // A one-time capability is supplied in a URL fragment, NOT query
        // strings or browser storage, so it does not enter HTTP access logs.
        UiSessionSecurity.UnlockRequest? request;
        try
        {
            request = await ctx.Request.ReadFromJsonAsync<UiSessionSecurity.UnlockRequest>();
        }
        catch (System.Text.Json.JsonException)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (request is null || !security.TryUnlock(request.Token))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        security.SetSession(ctx);
        ctx.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    if (!security.IsAuthenticated(ctx))
    {
        if (HttpMethods.IsGet(ctx.Request.Method) && ctx.Request.Path == "/")
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync(UiSessionSecurity.LoginHtml);
            return;
        }

        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    if (!HttpMethods.IsGet(ctx.Request.Method) &&
        !HttpMethods.IsHead(ctx.Request.Method) &&
        !UiHostPolicy.AuthorizedMutation(ctx, security, expectedPort))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    await next();
});

app.UseExceptionHandler(error =>
{
    error.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "HStack UI action failed. Run hstack doctor for diagnostics."
        });
    });
});

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapGet("/api/session", () => new SessionInfo(security.Csrf));
app.MapGet("/api/projects",
    async (CancellationToken ct) => Results.Ok(await host.ListAsync(ct)));
app.MapGet("/api/projects/{id}",
    async (string id, CancellationToken ct) => Results.Ok(await host.GetAsync(id, ct)));
app.MapGet("/api/diagnostics",
    async (CancellationToken ct) => Results.Ok(await host.DiagnosticsAsync(ct)));
app.MapGet("/api/projects/{id}/agents",
    async (string id, CancellationToken ct) => Results.Ok(await host.AgentsAsync(id, ct)));
app.MapGet("/api/projects/{id}/memory",
    async (string id, CancellationToken ct) => Results.Ok(await host.MemoryAsync(id, ct)));
app.MapGet("/api/projects/{id}/logs",
    async (string id, int? tail, CancellationToken ct) =>
        Results.Ok(await host.LogsAsync(id, tail ?? 80, ct)));
app.MapGet("/api/help/{topic}", async (string topic, CancellationToken ct) =>
{
    var file = topic switch
    {
        "start" => "INDEX.md",
        "develop" => "DEVELOPPER-AVEC-HSTACK.md",
        _ => null
    };
    if (file is null)
    {
        return Results.NotFound();
    }

    // Published Web.Host keeps manuals beside its executable. During
    // dotnet run, find only the checked-out HermesStack solution root.
    var path = Path.Combine(AppContext.BaseDirectory, "manual", file);
    if (!File.Exists(path))
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "HermesStack.slnx")))
            {
                continue;
            }

            path = Path.Combine(directory.FullName,
                topic == "start" ? "docs/INDEX.md" : "docs/guides/DEVELOPPER-AVEC-HSTACK.md");
            break;
        }
    }

    return File.Exists(path)
        ? Results.Text(await File.ReadAllTextAsync(path, ct), "text/plain; charset=utf-8")
        : Results.NotFound();
});

app.MapPost("/api/projects", async (CreateProjectRequest value, CancellationToken ct) =>
    Results.Created("/api/projects/" + value.Id, await host.CreateAsync(value, ct)));
app.MapPut("/api/projects/{id}", async (string id, EditProjectRequest value, CancellationToken ct) =>
    Results.Ok(await host.EditAsync(id, value, ct)));
app.MapDelete("/api/projects/{id}", async (string id, HttpContext ctx, CancellationToken ct) =>
{
    if (ctx.Request.Headers["X-HStack-Confirm"] != $"delete:{id}")
    {
        return Results.BadRequest(new { error = "Explicit deletion confirmation required." });
    }

    await host.DeleteAsync(id, ct);
    return Results.NoContent();
});
app.MapPost("/api/projects/{id}/lifecycle", async (
    string id, LifecycleRequest value, HttpContext ctx, CancellationToken ct) =>
{
    if (value.Action is not ("up" or "down" or "restart") ||
        ctx.Request.Headers["X-HStack-Confirm"] != $"{id}:{value.Action}")
    {
        return Results.BadRequest(new { error = "Explicit lifecycle confirmation required." });
    }

    return Results.Ok(await host.LifecycleAsync(id, value.Action, ct));
});
app.MapPost("/api/projects/{id}/ports", async (
    string id, PortRequest value, CancellationToken ct) =>
        Results.Ok(await host.AddPortAsync(id, value, ct)));
app.MapDelete("/api/projects/{id}/ports/{port:int}", async (
    string id, int port, HttpContext ctx, CancellationToken ct) =>
{
    if (ctx.Request.Headers["X-HStack-Confirm"] != $"{id}:port:{port}")
    {
        return Results.BadRequest(new { error = "Explicit port removal confirmation required." });
    }

    return Results.Ok(await host.RemovePortAsync(id, port, ct));
});
app.MapPost("/api/projects/{id}/backup", async (
    string id, HttpContext ctx, CancellationToken ct) =>
{
    if (ctx.Request.Headers["X-HStack-Confirm"] != $"{id}:backup")
    {
        return Results.BadRequest(new { error = "Explicit backup confirmation required." });
    }

    return Results.Ok(new { path = await host.BackupAsync(id, ct) });
});

// The WASM project's static files remain served by the SDK.
// The authenticated SPA fallback must be a physical file of the Web Host,
// not a duplicate content declaration for Client/wwwroot/index.html.
app.MapFallback((HttpContext context) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api") ||
        path.StartsWithSegments("/_framework") ||
        path.StartsWithSegments("/_content") ||
        Path.HasExtension(path.Value))
    {
        return Results.NotFound();
    }

    return Results.File(
        Path.Combine(AppContext.BaseDirectory, "spa", "index.html"),
        "text/html; charset=utf-8");
});

// We do NOT write the capability into ASP.NET access logs or persistent files.
// The browser is opened with a fragment that never reaches the server.
var launchUrl = security.LaunchUrl(expectedPort);
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("HermesStack local UI (HTTPS, loopback only).");
    Console.WriteLine("Open this one-time URL: " + launchUrl);
    Console.WriteLine("A locally generated certificate may require browser confirmation.");
    if (!args.Contains("--no-browser", StringComparer.Ordinal))
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo(launchUrl) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Headless sessions can paste the one-time URL instead.
        }
    }
});
await app.RunAsync();

public partial class Program;
