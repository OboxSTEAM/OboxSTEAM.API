namespace OboxSteam.Application.DTOs.EmailDTO;

public sealed class StaffAccountCredentialsEmailDto
{
    public string To { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    /// <summary>Human-readable role label in Vietnamese (e.g. Mentor, Chuyên gia).</summary>
    public string RoleLabel { get; set; } = null!;

    public string? AccountCode { get; set; }
}
