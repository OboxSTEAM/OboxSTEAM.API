namespace OboxSteam.Domain.Enums;

/// <summary>
/// How a class re-delivery request was resolved (reporting). Null until the
/// student selects a cohort or accepts a remedial class.
/// </summary>
public enum RedeliveryResolutionType
{
    StudentSelectedCohort,
    RemedialClass
}
