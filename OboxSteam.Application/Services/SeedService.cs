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

            _loggerService.LogInformation("Starting seed programs");
            var existingPrograms = await _unitOfWork.Programs.GetAllAsync();

            if (!existingPrograms.Any())
            {
                var roboticsProgramId = Guid.NewGuid();
                var codingProgramId = Guid.NewGuid();

                var programs = new List<Program>
                {
                    new Program
                    {
                        Id = roboticsProgramId,
                        Code = "PRG-ROBOTICS",
                        Name = "Robotics Fundamentals",
                        SeriesName = "Obox Master Track",
                        Description = "Hands-on robotics foundations with sensors, motion, and control.",
                        Level = DifficultyLevel.Beginner,
                        EstimatedDuration = "3 months at 10 hours a week",
                        SkillsGained = "Robotics basics, sensors, motors, control logic",
                        Rating = 4.8m,
                        TotalReviews = 120,
                        ThumbnailUrl = "https://cdn.oboxsteam.com/programs/robotics.png",
                        Status = "Active",
                        Price = 299.00m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = codingProgramId,
                        Code = "PRG-CODING",
                        Name = "Coding for Young Innovators",
                        SeriesName = "Obox Master Track",
                        Description = "Block-based and text-based coding for creative problem solving.",
                        Level = DifficultyLevel.Beginner,
                        EstimatedDuration = "4 months at 8 hours a week",
                        SkillsGained = "Algorithmic thinking, debugging, creativity",
                        Rating = 4.6m,
                        TotalReviews = 95,
                        ThumbnailUrl = "https://cdn.oboxsteam.com/programs/coding.png",
                        Status = "Active",
                        Price = 279.00m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    }
                };

                var modules = new List<Module>
                {
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-ROB-001",
                        ProgramId = roboticsProgramId,
                        Name = "Robotics Essentials",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 120.00m,
                        RetakeFee = 25.00m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-ROB-002",
                        ProgramId = roboticsProgramId,
                        Name = "Sensors and Actuators Lab",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 150.00m,
                        RetakeFee = 30.00m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-COD-001",
                        ProgramId = codingProgramId,
                        Name = "Creative Coding Basics",
                        ModuleType = ModuleType.Theory,
                        ModuleOrder = 1,
                        IsMandatory = true,
                        Price = 110.00m,
                        RetakeFee = 20.00m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Code = "MOD-COD-002",
                        ProgramId = codingProgramId,
                        Name = "Game Logic Workshop",
                        ModuleType = ModuleType.Experiential,
                        ModuleOrder = 2,
                        IsMandatory = true,
                        Price = 140.00m,
                        RetakeFee = 25.00m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    }
                };

                await _unitOfWork.Programs.AddRangeAsync(programs);
                await _unitOfWork.Modules.AddRangeAsync(modules);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed programs");
            }
            else
            {
                _loggerService.LogInformation("Programs already exist, skipping program seeding");
            }
        }

        public async Task ClearAllDataAsync()
        {
            _loggerService.LogInformation("Starting clear all data");
            await _unitOfWork.Modules.HardRemove(x => true);
            await _unitOfWork.Programs.HardRemove(x => true);
            await _unitOfWork.Users.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
           
            _loggerService.LogInformation("Finished clear all data");
        }
    }
}
