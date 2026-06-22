namespace OboxSteam.Application.Commons;

/// <summary>
/// Output of <see cref="Interfaces.IClassService.ResolveOpenClassAutoStartScheduleAsync"/>.
/// Tells <c>OpenClassAutoStartService</c> whether to scan-and-start classes now
/// and how long to sleep before the next wake (adaptive delay instead of fixed polling).
/// </summary>
public sealed class OpenClassAutoStartSchedule
{
    /// <summary>
    /// When true, at least one Open class is full and its StartDate has passed (state D).
    /// The background service should call <see cref="Interfaces.IClassService.AutoStartEligibleOpenClassesAsync"/>.
    /// </summary>
    public bool ShouldRunAutoStart { get; init; }

    /// <summary>
    /// Adaptive sleep duration before the next DB inspection.
    /// Length depends on <see cref="Reason"/> (12h idle, min(remaining, 12h) waiting for StartDate, etc.).
    /// </summary>
    public TimeSpan NextDelay { get; init; }

    /// <summary>
    /// Log label for operations: Idle | ReadyToStart | WaitingForStartDate | WaitingForCapacity.
    /// </summary>
    public string Reason { get; init; } = null!;
}
