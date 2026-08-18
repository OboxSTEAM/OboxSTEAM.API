namespace OboxSteam.Application.Notifications;

/// <summary>Replaces <c>{token}</c> placeholders in notification copy.</summary>
public static class NotificationTemplateRenderer
{
    public static string Interpolate(string template, IReadOnlyDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(tokens);

        if (tokens.Count == 0 || template.IndexOf('{') < 0)
        {
            return template;
        }

        var result = template;
        foreach (var (key, value) in tokens)
        {
            result = result.Replace("{" + key + "}", value ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }

    public static NotificationText Interpolate(NotificationText text, IReadOnlyDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new NotificationText(
            Interpolate(text.Title, tokens),
            text.Body is null ? null : Interpolate(text.Body, tokens));
    }
}
