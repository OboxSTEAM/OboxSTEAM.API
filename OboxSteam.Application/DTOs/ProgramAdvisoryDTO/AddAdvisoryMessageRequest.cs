namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AddAdvisoryMessageRequest
{
    public string Message { get; set; } = null!;

    public Guid? ConcurrencyVersion { get; set; }
}
