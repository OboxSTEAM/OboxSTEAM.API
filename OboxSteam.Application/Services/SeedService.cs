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
        private const string S3Bucket = "oboxsteam-bucket";

        private readonly ILogger _loggerService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBlobService _blobService;

        public SeedService(ILogger<SeedService> loggerService, IUnitOfWork unitOfWork, IBlobService blobService)
        {
            _loggerService = loggerService;
            _unitOfWork = unitOfWork;
            _blobService = blobService;
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
                    },
                    new Expert
                    {
                        Id = Guid.NewGuid(),
                        Code = "EXP-003",
                        UserId = null,
                        FullName = "Dr. Anh Pham",
                        Title = "Web Development Specialist",
                        Organization = "Tech Academy Vietnam",
                        Bio = "Full-stack developer teaching modern web technologies to young learners.",
                        AvatarUrl = null,
                        LinkedInUrl = null,
                        Achievements = "10+ years industry experience",
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
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-IOT",
                        Name = "Internet of Things Fundamentals",
                        SeriesName = "Connected Devices",
                        Description = "Build smart devices with sensors, microcontrollers, and cloud connectivity.",
                        Level = DifficultyLevel.Intermediate,
                        EstimatedDuration = "5 weeks at 3 hours a week",
                        SkillsGained = "Electronics, MQTT, sensor integration, prototyping",
                        Rating = 0,
                        TotalReviews = 0,
                        Status = "Active",
                        Price = 79.99m,
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
                var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");

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

                if (programIot != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-IOT-01",
                            ProgramId = programIot.Id,
                            Name = "Sensors and Microcontrollers",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 27.99m,
                            RetakeFee = 6.50m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-IOT-02",
                            ProgramId = programIot.Id,
                            Name = "Cloud Connectivity Lab",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 32.99m,
                            RetakeFee = 7.00m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-IOT not found. Skipping IoT module seeding.");
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
                var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
                var mentor2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-002");

                var moduleRobotics1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
                var moduleRobotics2 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-02");
                var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
                var moduleWebDev1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-01");
                var moduleWebDev2 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-02");
                var moduleSteam1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-STEAM-01");
                var moduleSteam2 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-STEAM-02");
                var moduleIot1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-IOT-01");
                var moduleIot2 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-IOT-02");

                if (mentor == null)
                {
                    _loggerService.LogWarning("Mentor MNT-001 not found. Skipping course seeding.");
                }
                else
                {
                    var courses = new List<Course>();
                    var seedTime = DateTime.UtcNow;

                    if (moduleRobotics1 != null)
                    {
                        courses.AddRange(new List<Course>
                        {
                            new Course
                            {
                                Id = Guid.NewGuid(),
                                Code = "CRS-ROBOTICS-01",
                                ModuleId = moduleRobotics1.Id,
                                MentorId = mentor.Id,
                                Name = "Robotics 101 - Cohort A",
                                Description = "First cohort for the basics of robotics and block-based programming.",
                                CreatedAt = seedTime,
                                CreatedBy = Guid.Empty,
                                IsDeleted = false
                            },
                            new Course
                            {
                                Id = Guid.NewGuid(),
                                Code = "CRS-ROBOTICS-02",
                                ModuleId = moduleRobotics1.Id,
                                MentorId = mentor.Id,
                                Name = "Robotics 101 - Cohort B",
                                Description = "Second cohort covering robotics fundamentals with hands-on exercises.",
                                CreatedAt = seedTime,
                                CreatedBy = Guid.Empty,
                                IsDeleted = false
                            }
                        });
                    }
                    else
                    {
                        _loggerService.LogWarning("Module MOD-ROBOTICS-01 not found. Skipping robotics basics course seeding.");
                    }

                    if (moduleRobotics2 != null)
                    {
                        courses.Add(new Course
                        {
                            Id = Guid.NewGuid(),
                            Code = "CRS-ROBOTICS-03",
                            ModuleId = moduleRobotics2.Id,
                            MentorId = mentor.Id,
                            Name = "Sensors and Movement - Spring 2026",
                            Description = "Experiential course on sensors, motors, and robot movement patterns.",
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }
                    else
                    {
                        _loggerService.LogWarning("Module MOD-ROBOTICS-02 not found. Skipping sensors course seeding.");
                    }

                    if (moduleRobotics3 != null)
                    {
                        courses.Add(new Course
                        {
                            Id = Guid.NewGuid(),
                            Code = "CRS-ROBOTICS-04",
                            ModuleId = moduleRobotics3.Id,
                            MentorId = mentor.Id,
                            Name = "Build and Test Challenge - Team Alpha",
                            Description = "Research module cohort focused on designing, building, and testing a robot prototype.",
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }
                    else
                    {
                        _loggerService.LogWarning("Module MOD-ROBOTICS-03 not found. Skipping build challenge course seeding.");
                    }

                    if (moduleWebDev1 != null)
                    {
                        courses.Add(new Course
                        {
                            Id = Guid.NewGuid(),
                            Code = "CRS-WEBDEV-01",
                            ModuleId = moduleWebDev1.Id,
                            MentorId = mentor.Id,
                            Name = "HTML & CSS - Evening Class",
                            Description = "Evening cohort for HTML structure, semantic markup, and responsive CSS layouts.",
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }
                    else
                    {
                        _loggerService.LogWarning("Module MOD-WEBDEV-01 not found. Skipping web foundations course seeding.");
                    }

                    if (moduleWebDev2 != null)
                    {
                        courses.Add(new Course
                        {
                            Id = Guid.NewGuid(),
                            Code = "CRS-WEBDEV-02",
                            ModuleId = moduleWebDev2.Id,
                            MentorId = mentor.Id,
                            Name = "JavaScript Basics - Weekend Bootcamp",
                            Description = "Weekend intensive on variables, DOM manipulation, and simple interactive pages.",
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }
                    else
                    {
                        _loggerService.LogWarning("Module MOD-WEBDEV-02 not found. Skipping JavaScript course seeding.");
                    }

                    if (moduleSteam1 != null)
                    {
                        courses.Add(new Course
                        {
                            Id = Guid.NewGuid(),
                            Code = "CRS-STEAM-01",
                            ModuleId = moduleSteam1.Id,
                            MentorId = mentor.Id,
                            Name = "STEAM Lab Kickoff - Cohort 1",
                            Description = "Introductory STEAM lab exploring interdisciplinary project-based learning.",
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }
                    else
                    {
                        _loggerService.LogWarning("Module MOD-STEAM-01 not found. Skipping STEAM kickoff course seeding.");
                    }

                    if (moduleSteam2 != null)
                    {
                        courses.Add(new Course
                        {
                            Id = Guid.NewGuid(),
                            Code = "CRS-STEAM-02",
                            ModuleId = moduleSteam2.Id,
                            MentorId = mentor.Id,
                            Name = "Creative Prototyping - Workshop A",
                            Description = "Hands-on workshop for rapid prototyping with recycled materials and simple circuits.",
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }
                    else
                    {
                        _loggerService.LogWarning("Module MOD-STEAM-02 not found. Skipping creative prototyping course seeding.");
                    }

                    if (moduleIot1 != null && mentor2 != null)
                    {
                        courses.Add(new Course
                        {
                            Id = Guid.NewGuid(),
                            Code = "CRS-IOT-01",
                            ModuleId = moduleIot1.Id,
                            MentorId = mentor2.Id,
                            Name = "Sensors 101 - Morning Class",
                            Description = "Introduction to sensors, Arduino basics, and reading environmental data.",
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }
                    else
                    {
                        _loggerService.LogWarning("Module MOD-IOT-01 or mentor MNT-002 not found. Skipping IoT sensors course seeding.");
                    }

                    if (moduleIot2 != null && mentor2 != null)
                    {
                        courses.Add(new Course
                        {
                            Id = Guid.NewGuid(),
                            Code = "CRS-IOT-02",
                            ModuleId = moduleIot2.Id,
                            MentorId = mentor2.Id,
                            Name = "Cloud Lab - Cohort Beta",
                            Description = "Connect devices to the cloud using MQTT and visualize live sensor data.",
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }
                    else
                    {
                        _loggerService.LogWarning("Module MOD-IOT-02 or mentor MNT-002 not found. Skipping IoT cloud course seeding.");
                    }

                    if (courses.Count > 0)
                    {
                        await _unitOfWork.Courses.AddRangeAsync(courses);
                        await _unitOfWork.SaveChangesAsync();
                        _loggerService.LogInformation("Finished seed courses — {Count} course(s) created.", courses.Count);
                    }
                    else
                    {
                        _loggerService.LogWarning("No courses seeded because required modules were not found.");
                    }
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
                var allCourses = await _unitOfWork.Courses.GetAllAsync();
                var courseByCode = allCourses.ToDictionary(c => c.Code, c => c);
                var seedTime = DateTime.UtcNow;
                var baseDate = seedTime.Date;

                var activities = CreateSeedActivities(courseByCode, baseDate, seedTime);

                if (activities.Count > 0)
                {
                    await _unitOfWork.Activities.AddRangeAsync(activities);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed activities — {Count} activity(ies) created.", activities.Count);
                }
                else
                {
                    _loggerService.LogWarning("No activities seeded because required courses were not found.");
                }
            }
            else
            {
                _loggerService.LogInformation("Activities already exist, skipping activity seeding");
            }

            _loggerService.LogInformation("Starting seed parent-student links");
            var existingParentStudents = await _unitOfWork.ParentStudents.GetAllAsync();
            if (!existingParentStudents.Any())
            {
                var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "PRT-001");
                var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
                var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
                var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");

                if (parent != null && student1 != null && student2 != null)
                {
                    var parentStudents = new List<ParentStudent>
                    {
                        new ParentStudent
                        {
                            Id = Guid.NewGuid(),
                            ParentId = parent.Id,
                            StudentId = student1.Id,
                            IsVerified = true,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        },
                        new ParentStudent
                        {
                            Id = Guid.NewGuid(),
                            ParentId = parent.Id,
                            StudentId = student2.Id,
                            IsVerified = true,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        }
                    };

                    if (student3 != null)
                    {
                        parentStudents.Add(new ParentStudent
                        {
                            Id = Guid.NewGuid(),
                            ParentId = parent.Id,
                            StudentId = student3.Id,
                            IsVerified = false,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }

                    await _unitOfWork.ParentStudents.AddRangeAsync(parentStudents);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed parent-student links");
                }
                else
                {
                    _loggerService.LogWarning("Parent or students not found. Skipping parent-student seeding.");
                }
            }
            else
            {
                _loggerService.LogInformation("Parent-student links already exist, skipping seeding");
            }

            _loggerService.LogInformation("Starting seed program enrollments");
            var existingProgramEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync();
            if (!existingProgramEnrollments.Any())
            {
                var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
                var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
                var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");
                var student4 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-004");
                var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
                var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
                var programSteam = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-STEAM-01");
                var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");
                var enrollTime = DateTime.UtcNow;

                var programEnrollments = new List<ProgramEnrollment>();

                if (student1 != null && programRobotics != null)
                {
                    programEnrollments.Add(new ProgramEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student1.Id,
                        ProgramId = programRobotics.Id,
                        Status = EnrollmentStatus.Active,
                        ProgressPercent = 25m,
                        EnrolledAt = enrollTime.AddDays(-14),
                        StartedAt = enrollTime.AddDays(-10),
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (student2 != null && programWebDev != null)
                {
                    programEnrollments.Add(new ProgramEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student2.Id,
                        ProgramId = programWebDev.Id,
                        Status = EnrollmentStatus.Active,
                        ProgressPercent = 10m,
                        EnrolledAt = enrollTime.AddDays(-7),
                        StartedAt = enrollTime.AddDays(-5),
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (student3 != null && programSteam != null)
                {
                    programEnrollments.Add(new ProgramEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student3.Id,
                        ProgramId = programSteam.Id,
                        Status = EnrollmentStatus.Active,
                        ProgressPercent = 50m,
                        EnrolledAt = enrollTime.AddDays(-21),
                        StartedAt = enrollTime.AddDays(-18),
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (student4 != null && programIot != null)
                {
                    programEnrollments.Add(new ProgramEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student4.Id,
                        ProgramId = programIot.Id,
                        Status = EnrollmentStatus.Active,
                        ProgressPercent = 0m,
                        EnrolledAt = enrollTime.AddDays(-2),
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (programEnrollments.Count > 0)
                {
                    await _unitOfWork.ProgramEnrollments.AddRangeAsync(programEnrollments);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed program enrollments — {Count} record(s).", programEnrollments.Count);
                }
                else
                {
                    _loggerService.LogWarning("No program enrollments seeded.");
                }
            }
            else
            {
                _loggerService.LogInformation("Program enrollments already exist, skipping seeding");
            }

            _loggerService.LogInformation("Starting seed module enrollments");
            var existingModuleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync();
            if (!existingModuleEnrollments.Any())
            {
                var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
                var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
                var moduleRobotics1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
                var moduleWebDev1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-01");
                var enrollTime = DateTime.UtcNow;

                var moduleEnrollments = new List<ModuleEnrollment>();

                if (student1 != null && moduleRobotics1 != null)
                {
                    moduleEnrollments.Add(new ModuleEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student1.Id,
                        ModuleId = moduleRobotics1.Id,
                        Status = EnrollmentStatus.Active,
                        ProgressPercent = 40m,
                        FinalGrade = null,
                        EnrolledAt = enrollTime.AddDays(-10),
                        StartedAt = enrollTime.AddDays(-8),
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (student2 != null && moduleWebDev1 != null)
                {
                    moduleEnrollments.Add(new ModuleEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student2.Id,
                        ModuleId = moduleWebDev1.Id,
                        Status = EnrollmentStatus.Active,
                        ProgressPercent = 15m,
                        EnrolledAt = enrollTime.AddDays(-5),
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (moduleEnrollments.Count > 0)
                {
                    await _unitOfWork.ModuleEnrollments.AddRangeAsync(moduleEnrollments);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed module enrollments");
                }
            }
            else
            {
                _loggerService.LogInformation("Module enrollments already exist, skipping seeding");
            }

            _loggerService.LogInformation("Starting seed course enrollments");
            var existingCourseEnrollments = await _unitOfWork.CourseEnrollments.GetAllAsync();
            if (!existingCourseEnrollments.Any())
            {
                var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
                var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
                var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");
                var courseRobotics1 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-ROBOTICS-01");
                var courseRobotics2 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-ROBOTICS-02");
                var courseWebDev1 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-WEBDEV-01");
                var courseSteam1 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-STEAM-01");
                var enrollTime = DateTime.UtcNow;

                var courseEnrollments = new List<CourseEnrollment>();

                if (student1 != null && courseRobotics1 != null)
                {
                    courseEnrollments.Add(new CourseEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student1.Id,
                        CourseId = courseRobotics1.Id,
                        Status = EnrollmentStatus.Active,
                        JoinedAt = enrollTime.AddDays(-7),
                        StartedAt = enrollTime.AddDays(-6),
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (student1 != null && courseRobotics2 != null)
                {
                    courseEnrollments.Add(new CourseEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student1.Id,
                        CourseId = courseRobotics2.Id,
                        Status = EnrollmentStatus.Active,
                        JoinedAt = enrollTime.AddDays(-3),
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (student2 != null && courseWebDev1 != null)
                {
                    courseEnrollments.Add(new CourseEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student2.Id,
                        CourseId = courseWebDev1.Id,
                        Status = EnrollmentStatus.Active,
                        JoinedAt = enrollTime.AddDays(-4),
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (student3 != null && courseSteam1 != null)
                {
                    courseEnrollments.Add(new CourseEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student3.Id,
                        CourseId = courseSteam1.Id,
                        Status = EnrollmentStatus.Active,
                        JoinedAt = enrollTime.AddDays(-12),
                        StartedAt = enrollTime.AddDays(-11),
                        CompletedAt = null,
                        CreatedAt = enrollTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (courseEnrollments.Count > 0)
                {
                    await _unitOfWork.CourseEnrollments.AddRangeAsync(courseEnrollments);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed course enrollments");
                }
            }
            else
            {
                _loggerService.LogInformation("Course enrollments already exist, skipping seeding");
            }

            _loggerService.LogInformation("Starting seed activity bookings");
            var existingBookings = await _unitOfWork.ActivityBookings.GetAllAsync();
            if (!existingBookings.Any())
            {
                var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
                var offlineActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-01-03");

                if (student1 != null && offlineActivity != null)
                {
                    var bookings = new List<ActivityBooking>
                    {
                        new ActivityBooking
                        {
                            Id = Guid.NewGuid(),
                            StudentId = student1.Id,
                            ActivityId = offlineActivity.Id,
                            Status = BookingStatus.Booked,
                            BookedAt = DateTime.UtcNow.AddDays(-2),
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        }
                    };

                    var liveActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-01-02");
                    var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
                    if (student2 != null && liveActivity != null)
                    {
                        bookings.Add(new ActivityBooking
                        {
                            Id = Guid.NewGuid(),
                            StudentId = student2.Id,
                            ActivityId = liveActivity.Id,
                            Status = BookingStatus.CheckedIn,
                            BookedAt = DateTime.UtcNow.AddDays(-5),
                            CheckedInAt = DateTime.UtcNow.AddDays(-4),
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }

                    await _unitOfWork.ActivityBookings.AddRangeAsync(bookings);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed activity bookings");
                }
                else
                {
                    _loggerService.LogWarning("Student or offline activity not found. Skipping activity booking seeding.");
                }
            }
            else
            {
                _loggerService.LogInformation("Activity bookings already exist, skipping seeding");
            }

            _loggerService.LogInformation("Starting seed materials");
            var existingMaterials = await _unitOfWork.Materials.GetAllAsync();
            if (!existingMaterials.Any())
            {
                var moduleRobotics1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
                var moduleWebDev1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-01");
                var activitySelfPaced = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-01-01");

                var materials = new List<Material>();

                if (moduleRobotics1 != null)
                {
                    materials.Add(new Material
                    {
                        Id = Guid.NewGuid(),
                        ModuleId = moduleRobotics1.Id,
                        ActivityId = null,
                        Title = "Robotics Starter Kit Guide",
                        MaterialType = OboxSteam.Domain.Enums.MaterialType.PDF,
                        FileUrl = "https://storage.oboxsteam.com/materials/robotics-starter-guide.pdf",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (moduleWebDev1 != null)
                {
                    materials.Add(new Material
                    {
                        Id = Guid.NewGuid(),
                        ModuleId = moduleWebDev1.Id,
                        ActivityId = null,
                        Title = "HTML Cheat Sheet",
                        MaterialType = OboxSteam.Domain.Enums.MaterialType.ExternalLink,
                        FileUrl = "https://developer.mozilla.org/en-US/docs/Web/HTML",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (moduleRobotics1 != null && activitySelfPaced != null)
                {
                    materials.Add(new Material
                    {
                        Id = Guid.NewGuid(),
                        ModuleId = moduleRobotics1.Id,
                        ActivityId = activitySelfPaced.Id,
                        Title = "Pre-class Reading: What is a Robot?",
                        MaterialType = OboxSteam.Domain.Enums.MaterialType.Video,
                        FileUrl = "https://storage.oboxsteam.com/videos/what-is-a-robot.mp4",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (materials.Count > 0)
                {
                    await _unitOfWork.Materials.AddRangeAsync(materials);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed materials");
                }
            }
            else
            {
                _loggerService.LogInformation("Materials already exist, skipping seeding");
            }

            _loggerService.LogInformation("Starting seed assignments");
            var existingAssignments = await _unitOfWork.Assignments.GetAllAsync();
            if (!existingAssignments.Any())
            {
                var moduleRobotics1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
                var moduleWebDev1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-01");
                var courseRobotics1 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-ROBOTICS-01");
                var seedTime = DateTime.UtcNow;

                if (moduleRobotics1 != null)
                {
                    var assignmentQuiz = new Assignment
                    {
                        Id = Guid.NewGuid(),
                        Code = "ASG-ROBOTICS-QUIZ-01",
                        ModuleId = moduleRobotics1.Id,
                        CourseId = courseRobotics1?.Id,
                        Title = "Robotics Fundamentals Quiz",
                        Description = "Multiple-choice quiz covering basic robotics concepts.",
                        AssignmentType = AssignmentType.Quiz,
                        MaxPoints = 100,
                        DueDate = seedTime.AddDays(14),
                        AllowShuffle = true,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    };

                    await _unitOfWork.Assignments.AddAsync(assignmentQuiz);
                    await _unitOfWork.SaveChangesAsync();

                    var question1 = new QuizQuestion
                    {
                        Id = Guid.NewGuid(),
                        AssignmentId = assignmentQuiz.Id,
                        QuestionText = "What is the primary purpose of a sensor on a robot?",
                        QuestionType = "SingleChoice",
                        Points = 50,
                        OrderIndex = 1,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    };

                    var question2 = new QuizQuestion
                    {
                        Id = Guid.NewGuid(),
                        AssignmentId = assignmentQuiz.Id,
                        QuestionText = "Which components are essential for robot movement? (Select all that apply)",
                        QuestionType = "MultipleChoice",
                        Points = 50,
                        OrderIndex = 2,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    };

                    await _unitOfWork.QuizQuestions.AddRangeAsync(new List<QuizQuestion> { question1, question2 });
                    await _unitOfWork.SaveChangesAsync();

                    var quizOptions = new List<QuizOption>
                    {
                        new QuizOption
                        {
                            Id = Guid.NewGuid(),
                            QuestionId = question1.Id,
                            OptionText = "To detect and respond to the environment",
                            IsCorrect = true,
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        },
                        new QuizOption
                        {
                            Id = Guid.NewGuid(),
                            QuestionId = question1.Id,
                            OptionText = "To decorate the robot",
                            IsCorrect = false,
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        },
                        new QuizOption
                        {
                            Id = Guid.NewGuid(),
                            QuestionId = question2.Id,
                            OptionText = "Motor",
                            IsCorrect = true,
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        },
                        new QuizOption
                        {
                            Id = Guid.NewGuid(),
                            QuestionId = question2.Id,
                            OptionText = "Wheel",
                            IsCorrect = true,
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        },
                        new QuizOption
                        {
                            Id = Guid.NewGuid(),
                            QuestionId = question2.Id,
                            OptionText = "Screen protector",
                            IsCorrect = false,
                            CreatedAt = seedTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        }
                    };

                    await _unitOfWork.QuizOptions.AddRangeAsync(quizOptions);
                    await _unitOfWork.SaveChangesAsync();
                }

                if (moduleWebDev1 != null)
                {
                    var assignmentUpload = new Assignment
                    {
                        Id = Guid.NewGuid(),
                        Code = "ASG-WEBDEV-UPLOAD-01",
                        ModuleId = moduleWebDev1.Id,
                        CourseId = null,
                        Title = "Build Your First Landing Page",
                        Description = "Submit a ZIP file containing your HTML and CSS landing page.",
                        AssignmentType = AssignmentType.FileUpload,
                        MaxPoints = 100,
                        DueDate = seedTime.AddDays(21),
                        AllowShuffle = false,
                        CreatedAt = seedTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    };

                    await _unitOfWork.Assignments.AddAsync(assignmentUpload);
                    await _unitOfWork.SaveChangesAsync();
                }

                _loggerService.LogInformation("Finished seed assignments");
            }
            else
            {
                _loggerService.LogInformation("Assignments already exist, skipping seeding");
            }

            _loggerService.LogInformation("Starting seed payments");
            var existingPayments = await _unitOfWork.Payments.GetAllAsync();
            if (!existingPayments.Any())
            {
                var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
                var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync();

                if (student1 != null && programEnrollment != null)
                {
                    var payments = new List<Payment>
                    {
                        new Payment
                        {
                            Id = Guid.NewGuid(),
                            Code = "INV-26001",
                            StudentId = student1.Id,
                            ProgramEnrollmentId = programEnrollment.Id,
                            ModuleEnrollmentId = null,
                            Amount = 49.99m,
                            Gateway = PaymentGateway.Momo,
                            TransactionId = "MOMO-TXN-001",
                            Status = PaymentStatus.Success,
                            PaidAt = DateTime.UtcNow.AddDays(-14),
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        }
                    };

                    await _unitOfWork.Payments.AddRangeAsync(payments);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed payments");
                }
            }
            else
            {
                _loggerService.LogInformation("Payments already exist, skipping seeding");
            }

            _loggerService.LogInformation("Finished seed all data");
        }

        public async Task ClearAllDataAsync()
        {
            _loggerService.LogInformation("Starting clear all data");

            // ── Step 1: Delete tracked S3 objects before wiping DB rows ────────
            await ClearS3ObjectsAsync();

            // ── Step 2: Hard-delete all DB rows ───────────────────────────────
            await _unitOfWork.MediaTags.HardRemove(x => true);
            await _unitOfWork.MediaAssets.HardRemove(x => true);
            await _unitOfWork.HighlightVideos.HardRemove(x => true);
            await _unitOfWork.FaceEmbeddings.HardRemove(x => true);
            await _unitOfWork.PortfolioCustomItems.HardRemove(x => true);
            await _unitOfWork.Portfolios.HardRemove(x => true);
            await _unitOfWork.StudentSkills.HardRemove(x => true);
            await _unitOfWork.StandardizedTests.HardRemove(x => true);
            await _unitOfWork.Certificates.HardRemove(x => true);
            await _unitOfWork.ProgramBoards.HardRemove(x => true);
            await _unitOfWork.OtpStorages.HardRemove(x => true);

            await _unitOfWork.QuizOptions.HardRemove(x => true);
            await _unitOfWork.QuizQuestions.HardRemove(x => true);
            await _unitOfWork.Submissions.HardRemove(x => true);
            await _unitOfWork.ActivityBookings.HardRemove(x => true);
            await _unitOfWork.Materials.HardRemove(x => true);
            await _unitOfWork.Activities.HardRemove(x => true);
            await _unitOfWork.Assignments.HardRemove(x => true);
            await _unitOfWork.CourseEnrollments.HardRemove(x => true);
            await _unitOfWork.ModuleEnrollments.HardRemove(x => true);
            await _unitOfWork.Payments.HardRemove(x => true);
            await _unitOfWork.ProgramEnrollments.HardRemove(x => true);
            await _unitOfWork.Courses.HardRemove(x => true);
            await _unitOfWork.Modules.HardRemove(x => true);
            await _unitOfWork.Programs.HardRemove(x => true);
            await _unitOfWork.ParentStudents.HardRemove(x => true);
            await _unitOfWork.Experts.HardRemove(x => true);
            await _unitOfWork.Users.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();

            _loggerService.LogInformation("Finished clear all data");
        }

        /// <summary>
        /// Xóa các S3 objects được track trong DB trước khi xóa DB rows.
        /// Chỉ xóa những keys thuộc bucket của project (media/, raw/, materials/, highlights/).
        /// Lỗi xóa từng object được log warning nhưng không làm dừng quá trình.
        /// </summary>
        private async Task ClearS3ObjectsAsync()
        {
            _loggerService.LogInformation("[ClearS3] Starting S3 cleanup...");
            var s3KeysToDelete = new List<string>();

            // ── 1. MediaAssets: FileUrl (media/) và RawVideoS3Key (raw/) ────────
            var mediaAssets = await _unitOfWork.MediaAssets.GetAllAsync();
            foreach (var asset in mediaAssets)
            {
                if (!string.IsNullOrWhiteSpace(asset.FileUrl))
                {
                    var key = ExtractS3Key(asset.FileUrl);
                    if (!string.IsNullOrEmpty(key))
                        s3KeysToDelete.Add(key);
                }

                if (!string.IsNullOrWhiteSpace(asset.RawVideoS3Key))
                    s3KeysToDelete.Add(asset.RawVideoS3Key);
            }

            // ── 2. Materials: FileUrl (materials/) — chỉ những URL thuộc S3 ───
            var materials = await _unitOfWork.Materials.GetAllAsync();
            foreach (var material in materials)
            {
                if (string.IsNullOrWhiteSpace(material.FileUrl))
                    continue;

                // Bỏ qua external links (không phải S3 URLs)
                if (!IsS3Url(material.FileUrl))
                    continue;

                var key = ExtractS3Key(material.FileUrl);
                if (!string.IsNullOrEmpty(key))
                    s3KeysToDelete.Add(key);
            }

            // ── 3. HighlightVideos: VideoUrl ────────────────────────────────────
            var highlightVideos = await _unitOfWork.HighlightVideos.GetAllAsync();
            foreach (var hv in highlightVideos)
            {
                if (!string.IsNullOrWhiteSpace(hv.VideoUrl))
                {
                    var key = ExtractS3Key(hv.VideoUrl);
                    if (!string.IsNullOrEmpty(key))
                        s3KeysToDelete.Add(key);
                }
            }

            // ── Deduplicate ────────────────────────────────────────────────────
            var distinctKeys = s3KeysToDelete
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _loggerService.LogInformation("[ClearS3] Found {Count} S3 object(s) to delete.", distinctKeys.Count);

            int deleted = 0, failed = 0;
            foreach (var key in distinctKeys)
            {
                try
                {
                    await _blobService.DeleteByKeyAsync(key);
                    deleted++;
                    _loggerService.LogDebug("[ClearS3] Deleted: {Key}", key);
                }
                catch (Exception ex)
                {
                    failed++;
                    _loggerService.LogWarning(ex, "[ClearS3] Failed to delete S3 object: {Key}", key);
                }
            }

            _loggerService.LogInformation(
                "[ClearS3] S3 cleanup done. Deleted={Deleted}, Failed={Failed}",
                deleted, failed);
        }

        /// <summary>
        /// Trích xuất S3 key từ URL (path-style hoặc virtual-hosted style).
        /// Trả về null nếu URL không hợp lệ hoặc không thuộc bucket của project.
        /// </summary>
        private static string? ExtractS3Key(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            var path = uri.AbsolutePath.TrimStart('/');

            // Path-style: /{bucket}/{key}
            var bucketPrefix = $"{S3Bucket}/";
            if (path.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
                path = path[bucketPrefix.Length..];

            return string.IsNullOrWhiteSpace(path) ? null : path;
        }

        /// <summary>
        /// Kiểm tra URL có thuộc S3 bucket của project hay không.
        /// Chỉ xóa những URL chứa hostname amazonaws.com hoặc bucket name của project.
        /// </summary>
        private static bool IsS3Url(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            var host = uri.Host;
            return host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase)
                || host.Contains(S3Bucket, StringComparison.OrdinalIgnoreCase);
        }

        private static List<Activity> CreateSeedActivities(
            Dictionary<string, Course> courseByCode,
            DateTime baseDate,
            DateTime seedTime)
        {
            var activities = new List<Activity>();

            void AddActivities(string courseCode, IEnumerable<Activity> courseActivities)
            {
                if (!courseByCode.TryGetValue(courseCode, out var course))
                {
                    return;
                }

                foreach (var activity in courseActivities)
                {
                    activity.CourseId = course.Id;
                    activity.CreatedAt = seedTime;
                    activity.CreatedBy = Guid.Empty;
                    activity.IsDeleted = false;
                    activities.Add(activity);
                }
            }

            AddActivities("CRS-ROBOTICS-01", new[]
            {
                NewActivity("ACT-ROBOTICS-01-01", "Pre-class Reading: Robot Basics", ActivityType.SelfPaced, 1,
                    "Self-paced reading before the first live session.", null, null, null, null, false, false),
                NewActivity("ACT-ROBOTICS-01-02", "Introduction Lecture", ActivityType.LiveOnline, 2,
                    "Live online introduction to robotics.",
                    "https://meet.google.com/robotics-intro",
                    baseDate.AddDays(1).AddHours(9), baseDate.AddDays(1).AddHours(11), 30, false, false),
                NewActivity("ACT-ROBOTICS-01-03", "Building the Chassis", ActivityType.Offline, 3,
                    "Hands-on lab for building robot chassis.",
                    "Lab Room 101",
                    baseDate.AddDays(3).AddHours(14), baseDate.AddDays(3).AddHours(16), 20, true, true),
                NewActivity("ACT-ROBOTICS-01-04", "Reflection Journal", ActivityType.SelfPaced, 4,
                    "Submit a short reflection on what you learned this week.", null, null, null, null, false, false),
            });

            AddActivities("CRS-ROBOTICS-02", new[]
            {
                NewActivity("ACT-ROBOTICS-02-01", "Cohort B Kickoff", ActivityType.LiveOnline, 1,
                    "Welcome session for cohort B.",
                    "https://meet.google.com/robotics-cohort-b",
                    baseDate.AddDays(2).AddHours(10), baseDate.AddDays(2).AddHours(11), 25, false, false),
                NewActivity("ACT-ROBOTICS-02-02", "Block Programming Lab", ActivityType.Offline, 2,
                    "Practice block-based programming with physical kits.",
                    "Lab Room 102",
                    baseDate.AddDays(5).AddHours(14), baseDate.AddDays(5).AddHours(16), 20, true, true),
                NewActivity("ACT-ROBOTICS-02-03", "Weekly Quiz Review", ActivityType.SelfPaced, 3,
                    "Review quiz answers and supplementary videos.", null, null, null, null, false, false),
            });

            AddActivities("CRS-ROBOTICS-03", new[]
            {
                NewActivity("ACT-ROBOTICS-03-01", "Sensor Theory", ActivityType.SelfPaced, 1,
                    "Learn how ultrasonic and infrared sensors work.", null, null, null, null, false, false),
                NewActivity("ACT-ROBOTICS-03-02", "Movement Patterns Workshop", ActivityType.LiveOnline, 2,
                    "Live workshop on programming movement patterns.",
                    "https://meet.google.com/robotics-movement",
                    baseDate.AddDays(7).AddHours(9), baseDate.AddDays(7).AddHours(11), 30, false, false),
                NewActivity("ACT-ROBOTICS-03-03", "Sensor Calibration Lab", ActivityType.Offline, 3,
                    "Calibrate sensors and test obstacle avoidance.",
                    "Lab Room 103",
                    baseDate.AddDays(10).AddHours(14), baseDate.AddDays(10).AddHours(17), 15, true, true),
            });

            AddActivities("CRS-ROBOTICS-04", new[]
            {
                NewActivity("ACT-ROBOTICS-04-01", "Design Brief", ActivityType.SelfPaced, 1,
                    "Read the build-and-test challenge design brief.", null, null, null, null, false, false),
                NewActivity("ACT-ROBOTICS-04-02", "Team Prototype Build", ActivityType.Offline, 2,
                    "Full-day team session to assemble and test prototypes.",
                    "Maker Space A",
                    baseDate.AddDays(14).AddHours(9), baseDate.AddDays(14).AddHours(17), 12, true, true),
                NewActivity("ACT-ROBOTICS-04-03", "Final Presentation", ActivityType.LiveOnline, 3,
                    "Teams present their robot prototypes to mentors.",
                    "https://meet.google.com/robotics-finals",
                    baseDate.AddDays(21).AddHours(14), baseDate.AddDays(21).AddHours(16), 50, false, true),
            });

            AddActivities("CRS-WEBDEV-01", new[]
            {
                NewActivity("ACT-WEBDEV-01-01", "HTML Structure Overview", ActivityType.SelfPaced, 1,
                    "Video lessons on semantic HTML and document structure.", null, null, null, null, false, false),
                NewActivity("ACT-WEBDEV-01-02", "Live CSS Layout Session", ActivityType.LiveOnline, 2,
                    "Live session on flexbox and grid layouts.",
                    "https://meet.google.com/webdev-css",
                    baseDate.AddDays(4).AddHours(18), baseDate.AddDays(4).AddHours(20), 35, false, false),
                NewActivity("ACT-WEBDEV-01-03", "Responsive Design Clinic", ActivityType.Offline, 3,
                    "In-person clinic for responsive page layouts.",
                    "Computer Lab 201",
                    baseDate.AddDays(8).AddHours(18), baseDate.AddDays(8).AddHours(20), 18, true, false),
            });

            AddActivities("CRS-WEBDEV-02", new[]
            {
                NewActivity("ACT-WEBDEV-02-01", "JavaScript Variables & Types", ActivityType.SelfPaced, 1,
                    "Self-paced module on JS fundamentals.", null, null, null, null, false, false),
                NewActivity("ACT-WEBDEV-02-02", "DOM Manipulation Live Lab", ActivityType.LiveOnline, 2,
                    "Interactive live coding on DOM APIs.",
                    "https://meet.google.com/webdev-js-dom",
                    baseDate.AddDays(6).AddHours(10), baseDate.AddDays(6).AddHours(12), 30, false, false),
                NewActivity("ACT-WEBDEV-02-03", "Weekend Hackathon", ActivityType.Offline, 3,
                    "Build a simple interactive page in teams.",
                    "Computer Lab 202",
                    baseDate.AddDays(12).AddHours(9), baseDate.AddDays(12).AddHours(15), 24, true, true),
                NewActivity("ACT-WEBDEV-02-04", "Code Review Session", ActivityType.LiveOnline, 4,
                    "Mentor-led code review of student projects.",
                    "https://meet.google.com/webdev-review",
                    baseDate.AddDays(15).AddHours(10), baseDate.AddDays(15).AddHours(12), 30, false, false),
            });

            AddActivities("CRS-STEAM-01", new[]
            {
                NewActivity("ACT-STEAM-01-01", "STEAM Lab Orientation", ActivityType.LiveOnline, 1,
                    "Orientation to interdisciplinary STEAM projects.",
                    "https://meet.google.com/steam-kickoff",
                    baseDate.AddDays(3).AddHours(9), baseDate.AddDays(3).AddHours(10), 40, false, false),
                NewActivity("ACT-STEAM-01-02", "Science Experiment Kit", ActivityType.SelfPaced, 2,
                    "Complete the at-home science experiment kit.", null, null, null, null, false, true),
                NewActivity("ACT-STEAM-01-03", "Group Art & Engineering Session", ActivityType.Offline, 3,
                    "Combine art and engineering in a collaborative build.",
                    "STEAM Studio 1",
                    baseDate.AddDays(9).AddHours(13), baseDate.AddDays(9).AddHours(16), 16, true, true),
            });

            AddActivities("CRS-STEAM-02", new[]
            {
                NewActivity("ACT-STEAM-02-01", "Prototyping Principles", ActivityType.SelfPaced, 1,
                    "Introduction to rapid prototyping methods.", null, null, null, null, false, false),
                NewActivity("ACT-STEAM-02-02", "Material Exploration Lab", ActivityType.Offline, 2,
                    "Explore recycled materials and simple circuits.",
                    "STEAM Studio 2",
                    baseDate.AddDays(11).AddHours(10), baseDate.AddDays(11).AddHours(13), 14, true, true),
                NewActivity("ACT-STEAM-02-03", "Design Critique", ActivityType.LiveOnline, 3,
                    "Peer and mentor critique of prototype designs.",
                    "https://meet.google.com/steam-critique",
                    baseDate.AddDays(16).AddHours(15), baseDate.AddDays(16).AddHours(16), 30, false, false),
                NewActivity("ACT-STEAM-02-04", "Portfolio Documentation", ActivityType.SelfPaced, 4,
                    "Document your prototype with photos and a short write-up.", null, null, null, null, false, true),
            });

            AddActivities("CRS-IOT-01", new[]
            {
                NewActivity("ACT-IOT-01-01", "Microcontroller Basics", ActivityType.SelfPaced, 1,
                    "Self-paced intro to Arduino and GPIO pins.", null, null, null, null, false, false),
                NewActivity("ACT-IOT-01-02", "Sensor Wiring Workshop", ActivityType.Offline, 2,
                    "Wire temperature and humidity sensors to a board.",
                    "Electronics Lab 301",
                    baseDate.AddDays(5).AddHours(9), baseDate.AddDays(5).AddHours(12), 16, true, true),
                NewActivity("ACT-IOT-01-03", "Live Q&A: Sensor Data", ActivityType.LiveOnline, 3,
                    "Live Q&A on reading and interpreting sensor data.",
                    "https://meet.google.com/iot-sensors",
                    baseDate.AddDays(8).AddHours(14), baseDate.AddDays(8).AddHours(15), 25, false, false),
            });

            AddActivities("CRS-IOT-02", new[]
            {
                NewActivity("ACT-IOT-02-01", "MQTT Concepts", ActivityType.SelfPaced, 1,
                    "Learn MQTT publish/subscribe patterns.", null, null, null, null, false, false),
                NewActivity("ACT-IOT-02-02", "Cloud Dashboard Setup", ActivityType.LiveOnline, 2,
                    "Set up a cloud dashboard for live sensor feeds.",
                    "https://meet.google.com/iot-cloud",
                    baseDate.AddDays(10).AddHours(10), baseDate.AddDays(10).AddHours(12), 20, false, false),
                NewActivity("ACT-IOT-02-03", "Device Deployment Lab", ActivityType.Offline, 3,
                    "Deploy a device and verify cloud connectivity.",
                    "Electronics Lab 302",
                    baseDate.AddDays(13).AddHours(9), baseDate.AddDays(13).AddHours(13), 12, true, true),
            });

            return activities;
        }

        private static Activity NewActivity(
            string code,
            string name,
            ActivityType activityType,
            int activityOrder,
            string? description,
            string? location,
            DateTime? startTime,
            DateTime? endTime,
            int? maxCapacity,
            bool requireQrCheckin,
            bool requireMediaEvidence) => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            ActivityType = activityType,
            Description = description,
            ActivityOrder = activityOrder,
            Location = location,
            StartTime = startTime,
            EndTime = endTime,
            MaxCapacity = maxCapacity,
            RequireQrCheckin = requireQrCheckin,
            RequireMediaEvidence = requireMediaEvidence,
        };
    }
}