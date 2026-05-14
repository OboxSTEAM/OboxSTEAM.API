using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Student booking for Live/Offline activities.
/// Unique constraint: one student can book a slot only once.
/// </summary>
public class ActivityBooking : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public BookingStatus Status { get; set; } = BookingStatus.Booked;

    public DateTime? BookedAt { get; set; }

    /// <summary>Timestamp when the mentor scanned the QR code for attendance.</summary>
    public DateTime? CheckedInAt { get; set; }
}
