using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MentorDTO;

public class MentorProfileDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string? FullName { get; set; }
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public RoleType Role { get; set; }
    public AccountStatus Status { get; set; }
    public int? MaxConcurrentClasses { get; set; }
    public int EffectiveMaxConcurrentClasses { get; set; }
    public int AssignedClassCount { get; set; }
    public int PendingRequestCount { get; set; }
    public int ConcurrentUsage { get; set; }
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Bio { get; set; }
    public string? Achievements { get; set; }
    public string? LinkedInUrl { get; set; }
    public List<MentorSkillDto> Skills { get; set; } = new();
}
