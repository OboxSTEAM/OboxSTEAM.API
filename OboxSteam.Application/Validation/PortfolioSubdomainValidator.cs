using System.Text.RegularExpressions;

namespace OboxSteam.Application.Validation;

public static partial class PortfolioSubdomainValidator
{
    private static readonly HashSet<string> ReservedLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "app", "mail", "static", "assets", "cdn", "oboxsteam",
        "dashboard", "login", "auth", "support", "help", "status", "docs", "blog",
        "ftp", "smtp", "pop", "imap", "ns1", "ns2", "dev", "staging", "test", "demo",
    };

    public static string? Normalize(string? subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            return null;
        }

        return subdomain.Trim().ToLowerInvariant();
    }

    public static bool TryValidateFormat(string normalizedSubdomain, out string? reason)
    {
        reason = null;

        if (normalizedSubdomain.Length is < 3 or > 63)
        {
            reason = "Subdomain must be between 3 and 63 characters.";
            return false;
        }

        if (!SubdomainPattern().IsMatch(normalizedSubdomain))
        {
            reason = "Subdomain may only contain lowercase letters, numbers, and hyphens, and cannot start or end with a hyphen.";
            return false;
        }

        if (normalizedSubdomain.Contains("--", StringComparison.Ordinal))
        {
            reason = "Subdomain cannot contain consecutive hyphens.";
            return false;
        }

        if (ReservedLabels.Contains(normalizedSubdomain))
        {
            reason = "This subdomain is reserved.";
            return false;
        }

        return true;
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SubdomainPattern();
}
