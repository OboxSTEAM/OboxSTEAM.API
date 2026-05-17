namespace OboxSteam.Application.DTOs.ParentDTO;

public class ParentStudentCreateDto
{
    public Guid ParentId { get; set; }
    public Guid StudentId { get; set; }
    public bool IsVerified { get; set; } = false;
}
