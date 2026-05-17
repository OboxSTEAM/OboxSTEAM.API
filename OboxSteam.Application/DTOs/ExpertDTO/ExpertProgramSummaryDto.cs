namespace OboxSteam.Application.DTOs.ExpertDTO;

public class ExpertProgramSummaryDto
{
    public Guid ProgramId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? RoleInBoard { get; set; }
}
