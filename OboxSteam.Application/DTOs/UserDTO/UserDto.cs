using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.UserDTO;

public class UserDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string? FullName { get; set; }
    public string Email { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public RoleType Role { get; set; }
    public AccountStatus Status { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
