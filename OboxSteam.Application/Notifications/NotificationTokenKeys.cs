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

    public static Dictionary<string, string> Create(
        string? studentName = null,
        string? actorName = null,
        string? className = null,
        string? programName = null,
        string? moduleName = null,
        string? activityName = null,
        string? assignmentTitle = null,
        string? extraAttempts = null)
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
        return tokens;
    }

    private static void Add(Dictionary<string, string> tokens, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tokens[key] = value;
        }
    }
}
