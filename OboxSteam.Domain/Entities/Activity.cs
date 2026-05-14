using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Activities represent individual slots within a course.
/// Can be SelfPaced (no scheduling), LiveOnline, or Offline.
/// </summary>
public class Activity : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public ActivityType ActivityType { get; set; }

    public string? Description { get; set; }

    public int ActivityOrder { get; set; }

    // Slot scheduling — only applies to LiveOnline / Offline
    [MaxLength(500)]
    public string? Location { get; set; } // Google Meet link or physical address

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public int? MaxCapacity { get; set; }

    public bool RequireQrCheckin { get; set; } // True only for Offline
    public bool RequireMediaEvidence { get; set; }

    // Navigation
    public ICollection<ActivityBooking> ActivityBookings { get; set; } = new List<ActivityBooking>();
    public ICollection<Material> Materials { get; set; } = new List<Material>();
    public ICollection<MediaAsset> MediaAssets { get; set; } = new List<MediaAsset>();
}
