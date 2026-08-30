namespace OboxSteam.Domain.Enums;

/// <summary>
/// What rebuy credit copy will do for one module on a destination class.
/// </summary>
public enum RebuyModuleCreditHint
{
    /// <summary>Student did not complete this module on the source purchase.</summary>
    Ahead = 0,

    /// <summary>Student completed it and the destination class has already taught it.</summary>
    Copied = 1,

    /// <summary>Student completed it but the destination class has not fully taught it yet.</summary>
    RedoWithClass = 2,
}
