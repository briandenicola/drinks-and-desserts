namespace WhiskeyAndSmokes.Api.Services;

public static class OidcPublicOrigin
{
    public static string Normalize(string origin)
    {
        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            (uri.AbsolutePath != "/" && !string.IsNullOrEmpty(uri.AbsolutePath)) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && IsLocalhost(uri.Host))))
        {
            throw new FormatException("OIDC public origin must be an absolute origin with HTTPS except localhost HTTP, and no path, query, or fragment.");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    public static bool IsLocalhost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
}
