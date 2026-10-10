using System.Net;
using HermesStack.Web.Contracts;
using Microsoft.AspNetCore.Http;

namespace HermesStack.Web.Host;

public static class UiHostPolicy
{
    public static bool ValidHost(HttpContext context, int port)
    {
        var ip = context.Connection.LocalIpAddress;
        return ip is not null &&
               IPAddress.IsLoopback(ip) &&
               string.Equals(context.Request.Host.Host, "127.0.0.1", StringComparison.Ordinal) &&
               context.Request.Host.Port == port &&
               context.Request.IsHttps;
    }

    public static bool ValidOrigin(HttpContext context, int port)
    {
        if (!context.Request.Headers.TryGetValue("Origin", out var origin))
        {
            return true; // ordinary GET navigation; mutations check origin explicitly
        }

        return origin.Count == 1 &&
               string.Equals(origin.ToString(), $"https://127.0.0.1:{port}", StringComparison.Ordinal);
    }

    public static bool AuthorizedMutation(HttpContext context, UiSessionSecurity security, int port)
    {
        return context.Request.Headers.TryGetValue("Origin", out var origin) &&
               origin.Count == 1 &&
               string.Equals(origin.ToString(), $"https://127.0.0.1:{port}", StringComparison.Ordinal) &&
               context.Request.Headers.TryGetValue("X-HStack-CSRF", out var value) &&
               value.Count == 1 && security.IsValidCsrf(value.ToString());
    }
}
