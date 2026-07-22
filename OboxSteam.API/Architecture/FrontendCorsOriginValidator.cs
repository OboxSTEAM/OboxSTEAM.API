namespace OboxSteam.API.Architecture;

/// <summary>
/// Production CORS origin checks for the main FE apex and one-label portfolio subdomains.
/// </summary>
public static class FrontendCorsOriginValidator
{
    public const string ApexOrigin = "https://oboxsteam.website";
    public const string PortfolioHostSuffix = ".oboxsteam.website";

    /// <summary>
    /// Returns true when <paramref name="origin"/> is in <paramref name="allowedOrigins"/>,
    /// or is an HTTPS one-label portfolio host such as <c>https://ch1mpleo.oboxsteam.website</c>.
    /// </summary>
    public static bool IsAllowed(string? origin, ISet<string> allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (allowedOrigins.Contains(origin))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Portfolio subdomains are HTTPS-only in production.
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reject non-default ports so https://evil.oboxsteam.website:8443 is not treated as the real host.
        if (!uri.IsDefaultPort)
        {
            return false;
        }

        var host = uri.IdnHost;
        if (!host.EndsWith(PortfolioHostSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // One label only: ch1mpleo.oboxsteam.website — not a.b.oboxsteam.website.
        var label = host[..^PortfolioHostSuffix.Length];
        return label.Length > 0 && !label.Contains('.');
    }
}
