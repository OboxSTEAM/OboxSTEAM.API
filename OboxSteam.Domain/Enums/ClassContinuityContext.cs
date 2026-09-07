namespace OboxSteam.Domain.Enums;

/// <summary>
/// Which continuity picker entry point produced a class catalog.
/// </summary>
public enum ClassContinuityContext
{
    /// <summary>Failed/Dropped/Completed rebuy or first-purchase class list.</summary>
    Rebuy = 0,

    /// <summary>Active program enrollment module redelivery / voluntary retake.</summary>
    ActiveRedelivery = 1,
}
