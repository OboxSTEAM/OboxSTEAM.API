using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class UpdateAdvisoryThreadStatusRequest
{
    public ProgramAdvisoryThreadStatus Status { get; set; }

    public string? Message { get; set; }
}
