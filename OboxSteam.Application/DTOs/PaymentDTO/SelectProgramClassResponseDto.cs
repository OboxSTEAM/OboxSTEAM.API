namespace OboxSteam.Application.DTOs.PaymentDTO;

public sealed class SelectProgramClassResponseDto
{
    public Guid ProgramEnrollmentId { get; set; }
    public Guid ClassId { get; set; }
    public DateTimeOffset HoldExpiresAt { get; set; }
}
