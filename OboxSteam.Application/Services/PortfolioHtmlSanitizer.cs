using AngleSharp.Dom;
using Ganss.Xss;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Whitelist-based HTML sanitizer for portfolio-authored content.
/// Allowed: p, br, strong, em, u, s, ul, ol, li, a[href], h3, span[style=color].
/// </summary>
public sealed class PortfolioHtmlSanitizer : IPortfolioHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public PortfolioHtmlSanitizer()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.UnionWith(
        [
            "p", "br", "strong", "em", "u", "s", "ul", "ol", "li", "a", "h3", "span",
        ]);

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.UnionWith(["href", "style"]);

        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowedCssProperties.Add("color");

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);

        _sanitizer.AllowDataAttributes = false;

        _sanitizer.FilterUrl += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.SanitizedUrl))
            {
                e.SanitizedUrl = null;
            }
        };

        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement element)
            {
                return;
            }

            if (!string.Equals(element.TagName, "A", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            element.SetAttribute("rel", "noopener noreferrer");
            if (!element.HasAttribute("target"))
            {
                element.SetAttribute("target", "_blank");
            }
        };
    }

    public string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var sanitized = _sanitizer.Sanitize(html.Trim());
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }
}
