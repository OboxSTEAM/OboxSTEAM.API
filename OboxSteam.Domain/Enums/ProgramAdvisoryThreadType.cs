namespace OboxSteam.Domain.Enums;

public enum ProgramAdvisoryThreadType
{
    Suggestion,
    RequiredChange,

    /// <summary>One program-level discussion thread. No status workflow.</summary>
    General,
}
