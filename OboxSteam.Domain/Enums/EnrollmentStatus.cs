namespace OboxSteam.Domain.Enums;

public enum EnrollmentStatus
{
    PendingPayment,  // Awaiting payment before enrollment becomes active
    Active,
    Deferred,
    Completed,
    Failed,
    Dropped
}
