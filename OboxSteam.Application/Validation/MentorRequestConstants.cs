namespace OboxSteam.Application.Validation;

/// <summary>Shared defaults for mentor class-assignment limits.</summary>
public static class MentorRequestConstants
{
    /// <summary>
    /// Used when <c>User.MaxConcurrentClasses</c> is null.
    /// Counts active assigned classes + Pending requests.
    /// </summary>
    public const int DefaultMaxConcurrentClasses = 3;
}
