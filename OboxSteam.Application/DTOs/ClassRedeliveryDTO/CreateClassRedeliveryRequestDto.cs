namespace OboxSteam.Application.DTOs.ClassRedeliveryDTO;

public class CreateClassRedeliveryRequestDto
{
    public Guid ModuleEnrollmentId { get; set; }
    public string? RequestMessage { get; set; }
}
