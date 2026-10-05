using HermesStack.Domain.Network;

namespace HermesStack.Application.Network;

public static class ProxyConfigurationPolicy
{
    public static ProxyConfiguration ValidateAndNormalize(ProxyConfiguration configuration)
    {
        if (!configuration.Enabled)
        {
            return new ProxyConfiguration();
        }

        if (string.IsNullOrWhiteSpace(configuration.Http) &&
            string.IsNullOrWhiteSpace(configuration.Https))
        {
            throw new InvalidDataException(
                "HS4001: An enabled proxy requires at least one HTTP or HTTPS endpoint.");
        }

        return configuration with
        {
            Http = NormalizeEndpoint(configuration.Http, "HTTP"),
            Https = NormalizeEndpoint(configuration.Https, "HTTPS"),
            NoProxy = configuration.EffectiveNoProxy
        };
    }

    private static string? NormalizeEndpoint(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidDataException(
                $"HS4002: {label} proxy endpoint must be an absolute http(s) URI.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidDataException(
                $"HS4003: {label} proxy credentials must not be embedded in hstack.yaml.");
        }

        return uri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
            UriFormat.UriEscaped).TrimEnd('/');
    }
}
