using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;
using OboxSteam.Application.Utils;

namespace OboxSteam.Application.Services
{
    public class SeedService : ISeedService
    {
        private readonly ILogger _loggerService;
        private readonly IUnitOfWork _unitOfWork;

        public SeedService(ILogger<SeedService> loggerService, IUnitOfWork unitOfWork)
        {
            _loggerService = loggerService;
            _unitOfWork = unitOfWork;
        }
        public async Task SeedAllDataAsync()
        {
            _loggerService.LogInformation("Starting seed all data");

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
                        Email = "student@oboxsteam.com",
                        PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                        FullName = "Bob Student",
                        Phone = "0123456785",
                        Role = RoleType.Student,
                        Status = AccountStatus.Active,
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    }
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

        public async Task ClearAllDataAsync()
        {
            _loggerService.LogInformation("Starting clear all data");
            await _unitOfWork.Users.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
           
            _loggerService.LogInformation("Finished clear all data");
        }
    }
}
