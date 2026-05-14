using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class User : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!; // e.g., STD-26001

    [MaxLength(255)]
    public string Email { get; set; } = null!;

    public string? PasswordHash { get; set; }

    [MaxLength(255)]
    public string? FullName { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; } // S3 Link

    public RoleType Role { get; set; }

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    // JWT Token
    [MaxLength(128)]
    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiryTime { get; set; }

    public bool IsEmailVerified { get; set; }

    // Navigation properties
    public ICollection<ParentStudent> ParentRelations { get; set; } = new List<ParentStudent>();
    public ICollection<ParentStudent> StudentRelations { get; set; } = new List<ParentStudent>();
    public StudentProfile? StudentProfile { get; set; }
    public ICollection<StudentSkill> StudentSkills { get; set; } = new List<StudentSkill>();
    public ICollection<StandardizedTest> StandardizedTests { get; set; } = new List<StandardizedTest>();
    public Expert? Expert { get; set; }
    public FaceEmbedding? FaceEmbedding { get; set; }
}
