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
                    Code = "ADM-001",
                    Email = "admin@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Admin@123")!,
                    FullName = "Admin",
                    Phone = "0123456789",
                    Role = RoleType.Admin,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-005",
                    Email = "student5@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Emma Tran",
                    Phone = "0123456776",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-006",
                    Email = "student6@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Ryan Pham",
                    Phone = "0123456775",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-007",
                    Email = "student7@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Sophia Hoang",
                    Phone = "0123456774",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-008",
                    Email = "student8@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Liam Vo",
                    Phone = "0123456773",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-009",
                    Email = "student9@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Olivia Bui",
                    Phone = "0123456772",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-010",
                    Email = "student10@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Noah Dang",
                    Phone = "0123456771",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-011",
                    Email = "student11@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Mia Nguyen",
                    Phone = "0123456770",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-012",
                    Email = "student12@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Ethan Le",
                    Phone = "0123456769",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-013",
                    Email = "student13@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Ava Truong",
                    Phone = "0123456768",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-014",
                    Email = "student14@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Lucas Phan",
                    Phone = "0123456767",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-015",
                    Email = "student15@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 15",
                    Phone = "0123456715",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-016",
                    Email = "student16@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 16",
                    Phone = "0123456716",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-017",
                    Email = "student17@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 17",
                    Phone = "0123456717",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-018",
                    Email = "student18@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 18",
                    Phone = "0123456718",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-019",
                    Email = "student19@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 19",
                    Phone = "0123456719",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-020",
                    Email = "student20@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 20",
                    Phone = "0123456720",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-021",
                    Email = "student21@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 21",
                    Phone = "0123456721",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-022",
                    Email = "student22@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 22",
                    Phone = "0123456722",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-023",
                    Email = "student23@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 23",
                    Phone = "0123456723",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-024",
                    Email = "student24@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 24",
                    Phone = "0123456724",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Code = "STD-025",
                    Email = "student25@oboxsteam.com",
                    PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                    FullName = "Open Class Student 25",
                    Phone = "0123456725",
                    Role = RoleType.Student,
                    Status = AccountStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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

        await EnsureAdditionalStudentUsersAsync();
    }

    private async Task EnsureAdditionalStudentUsersAsync()
    {
        var additionalStudents = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-005",
                Email = "student5@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Emma Tran",
                Phone = "0123456776",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-006",
                Email = "student6@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Ryan Pham",
                Phone = "0123456775",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-007",
                Email = "student7@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Sophia Hoang",
                Phone = "0123456774",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-008",
                Email = "student8@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Liam Vo",
                Phone = "0123456773",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-009",
                Email = "student9@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Olivia Bui",
                Phone = "0123456772",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-010",
                Email = "student10@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Noah Dang",
                Phone = "0123456771",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-011",
                Email = "student11@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Mia Nguyen",
                Phone = "0123456770",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-012",
                Email = "student12@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Ethan Le",
                Phone = "0123456769",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-013",
                Email = "student13@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Ava Truong",
                Phone = "0123456768",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-014",
                Email = "student14@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Lucas Phan",
                Phone = "0123456767",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-015",
                Email = "student15@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 15",
                Phone = "0123456715",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-016",
                Email = "student16@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 16",
                Phone = "0123456716",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-017",
                Email = "student17@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 17",
                Phone = "0123456717",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-018",
                Email = "student18@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 18",
                Phone = "0123456718",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-019",
                Email = "student19@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 19",
                Phone = "0123456719",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-020",
                Email = "student20@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 20",
                Phone = "0123456720",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-021",
                Email = "student21@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 21",
                Phone = "0123456721",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-022",
                Email = "student22@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 22",
                Phone = "0123456722",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-023",
                Email = "student23@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 23",
                Phone = "0123456723",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-024",
                Email = "student24@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 24",
                Phone = "0123456724",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "STD-025",
                Email = "student25@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = "Open Class Student 25",
                Phone = "0123456725",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            }
        };

        var studentsToAdd = new List<User>();
        foreach (var student in additionalStudents)
        {
            var exists = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == student.Code);
            if (exists == null)
            {
                studentsToAdd.Add(student);
            }
        }

        if (studentsToAdd.Count == 0)
        {
            return;
        }

        await _unitOfWork.Users.AddRangeAsync(studentsToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Backfilled {Count} additional student user(s).",
            studentsToAdd.Count);
    }
}

