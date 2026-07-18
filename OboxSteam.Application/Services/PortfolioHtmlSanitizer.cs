using AngleSharp.Dom;
using Ganss.Xss;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Whitelist-based HTML sanitizer for portfolio-authored content.
/// Allowed: p, br, strong, em, u, s, ul, ol, li, a[href], h2, h3, span[style=color],
/// and alignment classes pf-align-left|center|right|justify on block tags.
/// </summary>
public sealed class PortfolioHtmlSanitizer : IPortfolioHtmlSanitizer
{
    private static readonly HashSet<string> AllowedAlignmentClasses = new(StringComparer.Ordinal)
    {
        "pf-align-left",
        "pf-align-center",
        "pf-align-right",
        "pf-align-justify",
    };

    private readonly HtmlSanitizer _sanitizer;

    public PortfolioHtmlSanitizer()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.UnionWith(
        [
            "p", "br", "strong", "em", "u", "s", "ul", "ol", "li", "a", "h2", "h3", "span",
        ]);

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.UnionWith(["href", "style", "class"]);

        _sanitizer.AllowedClasses.Clear();
        _sanitizer.AllowedClasses.UnionWith(AllowedAlignmentClasses);

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

            if (string.Equals(element.TagName, "A", StringComparison.OrdinalIgnoreCase))
            {
                element.SetAttribute("rel", "noopener noreferrer");
                if (!element.HasAttribute("target"))
                {
                    element.SetAttribute("target", "_blank");
                }
            }

            // Keep only the FE alignment tokens; drop any other class that slipped through.
            if (element.HasAttribute("class"))
            {
                var kept = element.ClassList
                    .Where(c => AllowedAlignmentClasses.Contains(c))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (kept.Length == 0)
                {
                    element.RemoveAttribute("class");
                }
                else
                {
                    element.SetAttribute("class", string.Join(' ', kept));
                }
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
