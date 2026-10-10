using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace HermesStack.Web.Host;

// Explicit one-time URL-fragment bootstrap: unlike a query-string token,
// fragments never reach HTTP logs. API authority lives exclusively on host.
public sealed class UiSessionSecurity
{
    public const string CookieName = "__Host-hstack-ui";
    private readonly string _bootstrap = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly string _session = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly string _csrf = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private int _claimed;

    public string LaunchUrl(int port) => $"https://127.0.0.1:{port}/#{_bootstrap}";
    public string Csrf => _csrf;

    public bool TryUnlock(string? token)
    {
        if (!Equal(_bootstrap, token))
        {
            return false;
        }

        return Interlocked.CompareExchange(ref _claimed, 1, 0) == 0;
    }

    public bool IsAuthenticated(HttpContext context) =>
        context.Request.Cookies.TryGetValue(CookieName, out var value) && Equal(_session, value);

    public bool IsValidCsrf(string? value) => Equal(_csrf, value);

    public void SetSession(HttpContext context)
    {
        context.Response.Cookies.Append(CookieName, _session, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(8)
        });
    }

    private static bool Equal(string expected, string? actual)
    {
        if (actual is null || actual.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(actual));
    }

    public static string LoginHtml => """
        <!doctype html>
        <html lang="fr"><head><meta charset="utf-8"><meta name="referrer" content="no-referrer">
        <title>Déverrouillage HermesStack</title></head>
        <body><h1>HermesStack</h1><p id="status">Authentification locale...</p>
        <script>
        (async () => {
          const token = location.hash.slice(1);
          history.replaceState(null, "", "/");
          if (!token) {
            document.getElementById("status").textContent =
              "Session non ouverte. Relancez hstack ui pour obtenir votre lien privé.";
            return;
          }
          try {
            const res = await fetch("/unlock", {
              method: "POST", headers: {"Content-Type": "application/json"},
              body: JSON.stringify({token}), credentials: "same-origin"
            });
            if (!res.ok) throw new Error("Lien expiré ou incorrect");
            location.replace("/");
          } catch {
            document.getElementById("status").textContent =
              "Lien de démarrage invalide ou déjà utilisé. Relancez hstack ui.";
          }
        })();
        </script></body></html>
        """;
    public sealed record UnlockRequest(string? Token);
}
