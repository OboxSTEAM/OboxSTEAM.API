using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryMentionCountDto
{
    public ProgramAdvisoryTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public int MessageCount { get; set; }
    public int OpenPinCount { get; set; }
}
