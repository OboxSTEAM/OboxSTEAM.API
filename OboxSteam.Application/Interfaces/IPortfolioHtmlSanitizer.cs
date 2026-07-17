namespace OboxSteam.Application.Interfaces;

public interface IPortfolioHtmlSanitizer
{
    /// <summary>Sanitize user HTML; returns null when the result is empty.</summary>
    string? Sanitize(string? html);
}
