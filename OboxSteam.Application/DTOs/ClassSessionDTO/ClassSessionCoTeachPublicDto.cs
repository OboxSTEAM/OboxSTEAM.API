namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Public co-teach card for an Accepted expert. Must not include mentor feedback.
/// </summary>
public sealed class ClassSessionCoTeachPublicDto
{
    public Guid InvitationId { get; set; }
    public Guid ExpertId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Title { get; set; }
    public string? AvatarUrl { get; set; }
    public string[] Specialization { get; set; } = [];
    public List<ClassSessionCoTeachDegreeDto> Degrees { get; set; } = [];
}
