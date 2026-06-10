namespace OboxSteam.Application.DTOs.EmailDTO;

/// <summary>Data needed to send the enrollment confirmation email to the student.</summary>
public class EnrollmentConfirmationEmailDto
{
    public string To { get; set; } = null!;
    public string StudentName { get; set; } = null!;
    public string ProgramName { get; set; } = null!;
}
