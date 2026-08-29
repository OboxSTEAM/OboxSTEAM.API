namespace OboxSteam.Application.Notifications;

/// <summary>Canonical placeholder names for <see cref="NotificationTemplateRenderer"/>.</summary>
public static class NotificationTokenKeys
{
    public const string StudentName = "studentName";
    public const string ActorName = "actorName";
    public const string ClassName = "className";
    public const string ProgramName = "programName";
    public const string ModuleName = "moduleName";
    public const string ActivityName = "activityName";
    public const string AssignmentTitle = "assignmentTitle";
    public const string ExtraAttempts = "extraAttempts";
    public const string CheckedInAt = "checkedInAt";

    public static Dictionary<string, string> Create(
        string? studentName = null,
        string? actorName = null,
        string? className = null,
        string? programName = null,
        string? moduleName = null,
        string? activityName = null,
        string? assignmentTitle = null,
        string? extraAttempts = null,
        string? checkedInAt = null)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(tokens, StudentName, studentName);
        Add(tokens, ActorName, actorName);
        Add(tokens, ClassName, className);
        Add(tokens, ProgramName, programName);
        Add(tokens, ModuleName, moduleName);
        Add(tokens, ActivityName, activityName);
        Add(tokens, AssignmentTitle, assignmentTitle);
        Add(tokens, ExtraAttempts, extraAttempts);
        Add(tokens, CheckedInAt, checkedInAt);
        return tokens;
    }

    public static void CopyToPayload(NotificationPayload payload, IReadOnlyDictionary<string, string> tokens)
    {
        payload.StudentName ??= Read(tokens, StudentName);
        payload.ActorName ??= Read(tokens, ActorName);
        payload.ClassName ??= Read(tokens, ClassName);
        payload.ProgramName ??= Read(tokens, ProgramName);
    }

    private static void Add(Dictionary<string, string> tokens, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tokens[key] = value;
        }
    }

    private static string? Read(IReadOnlyDictionary<string, string> tokens, string key)
        => tokens.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
