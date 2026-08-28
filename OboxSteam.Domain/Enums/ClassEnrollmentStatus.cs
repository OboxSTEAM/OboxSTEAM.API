namespace OboxSteam.Domain.Enums;

public enum ClassEnrollmentStatus
{
    Active,
    Transferred,
    Withdrawn,
    Completed,

    /// <summary>Soft seat hold while program tuition payment is pending.</summary>
    Pending
}
