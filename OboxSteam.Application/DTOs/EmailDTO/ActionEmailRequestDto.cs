namespace OboxSteam.Application.DTOs.EmailDTO;

public class ActionEmailRequestDto
{
    public string To { get; set; } = null!;
    public string? UserName { get; set; }
    public string Link { get; set; } = null!; 
}
