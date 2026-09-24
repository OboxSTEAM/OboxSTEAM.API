using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryThreadActionRequest
{
    public AdvisoryThreadAction Action { get; set; }

    public string? Message { get; set; }

    public Guid? ConcurrencyVersion { get; set; }

    public string? ClientOperationId { get; set; }
}
