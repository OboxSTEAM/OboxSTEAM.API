namespace OboxSteam.Domain.Enums;

/// <summary>
/// How a class continuity request was resolved (reporting). Null until the
/// student selects a Standard cohort. <see cref="RemedialClass"/> is legacy —
/// intensive Remedial offers are no longer created.
/// </summary>
public enum RedeliveryResolutionType
{
    StudentSelectedCohort,

    /// <summary>Legacy Remedial intensive path — no longer written.</summary>
    RemedialClass
}
