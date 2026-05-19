using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

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
                };

                await _unitOfWork.Users.AddRangeAsync(users);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed users");
            }
            else
            {
                _loggerService.LogInformation("Users already exist, skipping user seeding");
            }

            _loggerService.LogInformation("Starting seed experts");
            var existingExperts = await _unitOfWork.Experts.GetAllAsync();

            if (!existingExperts.Any())
            {
                var mentorUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");

                var experts = new List<Expert>
                {
                    new Expert
                    {
                        Id = Guid.NewGuid(),
                        Code = "EXP-001",
                        UserId = mentorUser?.Id,
                        FullName = "Dr. Linh Tran",
                        Title = "Senior Robotics Mentor",
                        Organization = "OboxSTEAM",
                        Bio = "Robotics mentor with a focus on hands-on learning and STEM outreach.",
                        AvatarUrl = null,
                        LinkedInUrl = null,
                        Achievements = "National STEM Educator Award",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Expert
                    {
                        Id = Guid.NewGuid(),
                        Code = "EXP-002",
                        UserId = null,
                        FullName = "Prof. Minh Hoang",
                        Title = "Visiting STEAM Advisor",
                        Organization = "STEAM Research Lab",
                        Bio = "Advisor for STEAM curriculum design and experiential learning.",
                        AvatarUrl = null,
                        LinkedInUrl = null,
                        Achievements = "Published 20+ STEAM research papers",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    }
                };

                await _unitOfWork.Experts.AddRangeAsync(experts);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed experts");
            }
            else
            {
                _loggerService.LogInformation("Experts already exist, skipping expert seeding");
            }

            _loggerService.LogInformation("Starting seed programs");
            var existingPrograms = await _unitOfWork.Programs.GetAllAsync();

            if (!existingPrograms.Any())
            {
                var programs = new List<Program>
                {
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-ROBOTICS",
                        Name = "Introduction to Robotics",
                        SeriesName = "Robotics Fundamentals",
                        Description = "A beginner-friendly program to introduce students to basic robotics principles and block-based programming.",
                        Level = DifficultyLevel.Beginner,
                        EstimatedDuration = "4 weeks at 2 hours a week",
                        SkillsGained = "Basic mechanics, block-based coding, logical thinking",
                        Rating = 0,
                        TotalReviews = 0,
                        Status = "Active",
                        Price = 49.99m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-WEBDEV",
                        Name = "Web Development Bootcamp",
                        SeriesName = "Software Engineering",
                        Description = "Learn to build your own websites using HTML, CSS, and basic JavaScript.",
                        Level = DifficultyLevel.Intermediate,
                        EstimatedDuration = "8 weeks at 4 hours a week",
                        SkillsGained = "HTML, CSS, JavaScript, Web Design",
                        Rating = 0,
                        TotalReviews = 0,
                        Status = "Active",
                        Price = 89.99m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-STEAM-01",
                        Name = "STEAM Explorer",
                        SeriesName = "Obox STEAM Discovery",
                        Description = "Hands-on projects covering Science, Technology, Engineering, Arts, and Mathematics.",
                        Level = DifficultyLevel.Beginner,
                        EstimatedDuration = "6 weeks at 3 hours a week",
                        SkillsGained = "Problem solving, creativity, scientific method",
                        Rating = 0,
                        TotalReviews = 0,
                        Status = "Active",
                        Price = 69.99m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    }
                };

                await _unitOfWork.Programs.AddRangeAsync(programs);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed programs");
            }
            else
            {
                _loggerService.LogInformation("Programs already exist, skipping program seeding");
            }

            _loggerService.LogInformation("Starting seed modules");
            var existingModules = await _unitOfWork.Modules.GetAllAsync();
            if (!existingModules.Any())
            {
                var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
                var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
                var programSteam = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-STEAM-01");

                var modules = new List<Module>();

                if (programRobotics != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-ROBOTICS-01",
                            ProgramId = programRobotics.Id,
                            Name = "Basics of Robotics",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 19.99m,
                            RetakeFee = 5.00m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-ROBOTICS-02",
                            ProgramId = programRobotics.Id,
                            Name = "Sensors and Movement",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            PrerequisiteModuleId = null,
                            IsMandatory = true,
                            Price = 24.99m,
                            RetakeFee = 6.00m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-ROBOTICS-03",
                            ProgramId = programRobotics.Id,
                            Name = "Build and Test Challenge",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = true,
                            Price = 29.99m,
                            RetakeFee = 7.50m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-ROBOTICS not found. Skipping robotics module seeding.");
                }

                if (programWebDev != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-WEBDEV-01",
                            ProgramId = programWebDev.Id,
                            Name = "HTML & CSS Foundations",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 29.99m,
                            RetakeFee = 7.00m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-WEBDEV-02",
                            ProgramId = programWebDev.Id,
                            Name = "JavaScript Basics",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 34.99m,
                            RetakeFee = 8.00m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-WEBDEV not found. Skipping web development module seeding.");
                }

                if (programSteam != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-STEAM-01",
                            ProgramId = programSteam.Id,
                            Name = "STEAM Lab Kickoff",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 21.99m,
                            RetakeFee = 5.50m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-STEAM-02",
                            ProgramId = programSteam.Id,
                            Name = "Creative Prototyping",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 24.99m,
                            RetakeFee = 6.00m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-STEAM-01 not found. Skipping STEAM module seeding.");
                }

                if (modules.Count > 0)
                {
                    await _unitOfWork.Modules.AddRangeAsync(modules);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed modules");
                }
                else
                {
                    _loggerService.LogWarning("No modules seeded because required programs were not found.");
                }
            }
            else
            {
                _loggerService.LogInformation("Modules already exist, skipping module seeding");
            }

            _loggerService.LogInformation("Starting seed courses");
            var existingCourses = await _unitOfWork.Courses.GetAllAsync();
            if (!existingCourses.Any())
            {
                var moduleRobotics1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
                var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");

                if (moduleRobotics1 != null && mentor != null)
                {
                    var courses = new List<Course>
                    {
                        new Course
                        {
                            Id = Guid.NewGuid(),
                            Code = "CRS-ROBOTICS-01",
                            ModuleId = moduleRobotics1.Id,
                            MentorId = mentor.Id,
                            Name = "Robotics 101 - Cohort A",
                            Description = "First cohort for the basics of robotics",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    };
                    await _unitOfWork.Courses.AddRangeAsync(courses);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed courses");
                }
                else
                {
                    _loggerService.LogWarning("Required module or mentor not found. Skipping course seeding.");
                }
            }
            else
            {
                _loggerService.LogInformation("Courses already exist, skipping course seeding");
            }

            _loggerService.LogInformation("Starting seed activities");
            var existingActivities = await _unitOfWork.Activities.GetAllAsync();
            if (!existingActivities.Any())
            {
                var courseRobotics1 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-ROBOTICS-01");

                if (courseRobotics1 != null)
                {
                    var activities = new List<Activity>
                    {
                        new Activity
                        {
                            Id = Guid.NewGuid(),
                            Code = "ACT-ROBOTICS-01",
                            CourseId = courseRobotics1.Id,
                            Name = "Introduction Lecture",
                            ActivityType = ActivityType.LiveOnline,
                            Description = "Live online introduction to robotics",
                            ActivityOrder = 1,
                            Location = "https://meet.google.com/abc-defg-hij",
                            StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9),
                            EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(11),
                            MaxCapacity = 30,
                            RequireQrCheckin = false,
                            RequireMediaEvidence = false,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Activity
                        {
                            Id = Guid.NewGuid(),
                            Code = "ACT-ROBOTICS-02",
                            CourseId = courseRobotics1.Id,
                            Name = "Building the Chassis",
                            ActivityType = ActivityType.Offline,
                            Description = "Hands-on lab for building robot chassis",
                            ActivityOrder = 2,
                            Location = "Lab Room 101",
                            StartTime = DateTime.UtcNow.AddDays(3).Date.AddHours(14),
                            EndTime = DateTime.UtcNow.AddDays(3).Date.AddHours(16),
                            MaxCapacity = 20,
                            RequireQrCheckin = true,
                            RequireMediaEvidence = true,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    };
                    await _unitOfWork.Activities.AddRangeAsync(activities);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed activities");
                }
                else
                {
                    _loggerService.LogWarning("Course CRS-ROBOTICS-01 not found. Skipping activity seeding.");
                }
            }
            else
            {
                _loggerService.LogInformation("Activities already exist, skipping activity seeding");
            }
        }

        public async Task ClearAllDataAsync()
        {
            _loggerService.LogInformation("Starting clear all data");
            await _unitOfWork.Activities.HardRemove(x => true);
            await _unitOfWork.Courses.HardRemove(x => true);
            await _unitOfWork.Modules.HardRemove(x => true);
            await _unitOfWork.Programs.HardRemove(x => true);
            await _unitOfWork.ParentStudents.HardRemove(x => true);
            await _unitOfWork.Users.HardRemove(x => true);
            await _unitOfWork.Experts.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();

            _loggerService.LogInformation("Finished clear all data");
        }
    }
}