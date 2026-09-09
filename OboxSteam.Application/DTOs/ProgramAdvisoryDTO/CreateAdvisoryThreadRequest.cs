using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class CreateAdvisoryThreadRequest
{
    public ProgramAdvisoryTargetType TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public ProgramAdvisoryThreadType Type { get; set; }

    public string Message { get; set; } = null!;

    public Guid? SubmissionId { get; set; }
}
