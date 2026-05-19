using System;

namespace OboxSteam.Application.DTOs.ParentDTO;

public class ParentStudentRelationDto
{
    public Guid LinkedUserId { get; set; }
    public string? Code { get; set; }
    public string Email { get; set; } = null!;
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
