using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryDiscussionPinDto
{
    public DiscussionPinStatus Status { get; set; }
    public Guid? PinnedByUserId { get; set; }
    public string? PinnedByName { get; set; }
    public DateTime? PinnedAt { get; set; }
    public Guid? AddressedByUserId { get; set; }
    public string? AddressedByName { get; set; }
    public DateTime? AddressedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
