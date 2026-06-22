using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedUsersAsync()
    {
        _loggerService.LogInformation("Starting seed users");
        var existingUsers = await _unitOfWork.Users.GetAllAsync();
        var users = new List<User>();

        if (!existingUsers.Any())
        {
            users = new List<User>
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "SAD-001",
                    Email = "superadmin@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Admin@123")!,
                    FullName = "Super Admin",
                    Phone = "0123456789",
                    Role = RoleType.SuperAdmin,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "MNG-001",
                    Email = "manager@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Manager@123")!,
                    FullName = "System Manager",
                    Phone = "0123456788",
                    Role = RoleType.Manager,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "MNT-001",
                    Email = "mentor@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                    FullName = "John Mentor",
                    Phone = "0123456787",
                    Role = RoleType.Mentor,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "PRT-001",
                    Email = "parent@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Parent@123")!,
                    FullName = "Jane Parent",
                    Phone = "0123456786",
                    Role = RoleType.Parent,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-001",
                    Email = "student1@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Bob Student",
                    Phone = "0123456785",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-002",
                    Email = "student2@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "John Student",
                    Phone = "0123456784",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-003",
                    Email = "student3@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Alice Nguyen",
                    Phone = "0123456783",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-004",
                    Email = "student4@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "David Le",
                    Phone = "0123456782",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "MNT-002",
                    Email = "mentor2@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                    FullName = "Sarah Mentor",
                    Phone = "0123456781",
                    Role = RoleType.Mentor,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "MNT-003",
                    Email = "mentor3@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                    FullName = "Michael Mentor",
                    Phone = "0123456780",
                    Role = RoleType.Mentor,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "MNT-004",
                    Email = "mentor4@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                    FullName = "Emily Mentor",
                    Phone = "0123456779",
                    Role = RoleType.Mentor,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "MNT-005",
                    Email = "mentor5@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                    FullName = "Chris Mentor",
                    Phone = "0123456778",
                    Role = RoleType.Mentor,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "MNT-006",
                    Email = "mentor6@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                    FullName = "Lisa Mentor",
                    Phone = "0123456777",
                    Role = RoleType.Mentor,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
            };

            await _unitOfWork.Users.AddRangeAsync(users);
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation("Finished seed users");
        }
        else
        {
            _loggerService.LogInformation("Users already exist, skipping user seeding");
        }
    }
}

