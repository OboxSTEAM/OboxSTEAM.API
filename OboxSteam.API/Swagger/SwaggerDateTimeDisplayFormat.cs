namespace OboxSteam.API.Swagger;

/// <summary>
/// Vietnam reader-friendly date/time strings for Swagger and API JSON serialization.
/// Values without a timezone are interpreted as GMT+7.
/// </summary>
public static class SwaggerDateTimeDisplayFormat
{
    /// <summary>Date and time pattern, e.g. 21/06/2026 16:00.</summary>
    public const string DateTimePattern = "dd/MM/yyyy HH:mm";

    /// <summary>Example shown in Swagger for date/time fields.</summary>
    public const string DateTimeExample = "21/06/2026 16:00";

    /// <summary>Date-only pattern for fields such as startDate and dueDate.</summary>
    public const string DatePattern = "dd/MM/yyyy";

    /// <summary>Example shown in Swagger for date-only fields.</summary>
    public const string DateExample = "01/06/2026";
}
