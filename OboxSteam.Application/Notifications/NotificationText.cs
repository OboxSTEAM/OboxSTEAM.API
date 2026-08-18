namespace OboxSteam.Application.Notifications;

/// <summary>Title and body copy for one notification variant.</summary>
public sealed class NotificationText
{
    public string Title { get; }

    public string? Body { get; }

    public NotificationText(string title, string? body = null)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Body = body;
    }
}
