using System.Text.RegularExpressions;
using OboxSteam.Application.DTOs.PortfolioDTO;
using OboxSteam.Application.Utils;

namespace OboxSteam.Application.Validation;

public static partial class PortfolioThemeValidator
{
    public static void ValidateTheme(ThemeConfigDto theme)
    {
        ValidateHexColor(theme.PrimaryColor, nameof(theme.PrimaryColor));
        ValidateHexColor(theme.SecondaryColor, nameof(theme.SecondaryColor));
        ValidateHexColor(theme.AccentColor, nameof(theme.AccentColor));
        ValidateOptionalUrl(theme.BackgroundImageUrl, nameof(theme.BackgroundImageUrl), 500);

        if (theme.HeadingFontFamily is { Length: > 100 })
        {
            throw ErrorHelper.BadRequest("HeadingFontFamily must be at most 100 characters.");
        }

        if (theme.FontFamily is { Length: > 100 })
        {
            throw ErrorHelper.BadRequest("FontFamily must be at most 100 characters.");
        }
    }

    public static void ValidateHexColor(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (!HexColorPattern().IsMatch(trimmed))
        {
            throw ErrorHelper.BadRequest($"{fieldName} must be a valid hex color (e.g. #1A2B3C).");
        }
    }

    public static void ValidateOptionalUrl(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw ErrorHelper.BadRequest($"{fieldName} must be at most {maxLength} characters.");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw ErrorHelper.BadRequest($"{fieldName} must be a valid http or https URL.");
        }
    }

    [GeneratedRegex(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorPattern();
}
