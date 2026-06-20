using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
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
                        AvatarUrl = "https://images.unsplash.com/photo-1758685848006-1bc450061624?q=80&w=1332&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        LinkedInUrl = "https://www.linkedin.com/company/anthropicresearch",
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
                        AvatarUrl = "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?q=80&w=687&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        LinkedInUrl = "https://www.linkedin.com/company/anthropicresearch",
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
                        AvatarUrl = "https://images.unsplash.com/photo-1555436169-20e93ea9a7ff?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        LinkedInUrl = "https://www.linkedin.com/company/anthropicresearch",
                        Achievements = "10+ years industry experience",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Expert
                    {
                        Id = Guid.NewGuid(),
                        Code = "EXP-004",
                        UserId = null,
                        FullName = "Dr. Mai Nguyen",
                        Title = "AI & Machine Learning Specialist",
                        Organization = "Vietnam AI Institute",
                        Bio = "Researcher and educator specializing in introductory AI and data science for students.",
                        AvatarUrl = "https://placeholder.local/avatars/exp-004",
                        LinkedInUrl = "https://www.linkedin.com/company/anthropicresearch",
                        Achievements = "Led 5 national AI education initiatives",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Expert
                    {
                        Id = Guid.NewGuid(),
                        Code = "EXP-005",
                        UserId = null,
                        FullName = "Prof. Hoa Le",
                        Title = "Mathematics Educator",
                        Organization = "National University of Education",
                        Bio = "Passionate about making mathematics engaging through puzzles and real-world applications.",
                        AvatarUrl = "https://images.unsplash.com/photo-1561346745-5db62ae43861?q=80&w=1283&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        LinkedInUrl = "https://www.linkedin.com/company/anthropicresearch",
                        Achievements = "Author of 3 popular math textbooks",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Expert
                    {
                        Id = Guid.NewGuid(),
                        Code = "EXP-006",
                        UserId = null,
                        FullName = "Ms. Thao Vu",
                        Title = "Digital Arts Director",
                        Organization = "Creative Minds Studio",
                        Bio = "Professional illustrator mentoring students in digital art, design, and creative expression.",
                        AvatarUrl = "https://plus.unsplash.com/premium_photo-1658506656752-4f1b1c1d5916?q=80&w=687&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        LinkedInUrl = "https://www.linkedin.com/company/anthropicresearch",
                        Achievements = "Award-winning digital artist",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Expert
                    {
                        Id = Guid.NewGuid(),
                        Code = "EXP-007",
                        UserId = null,
                        FullName = "Dr. Khoa Bui",
                        Title = "Environmental Scientist",
                        Organization = "Green Earth Foundation",
                        Bio = "Environmental researcher focused on sustainability education and climate science outreach.",
                        AvatarUrl = "https://images.unsplash.com/photo-1581368129682-e2d66324045b?q=80&w=687&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        LinkedInUrl = "https://www.linkedin.com/company/anthropicresearch",
                        Achievements = "UN Youth Climate Ambassador 2023",
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
                        Category = ProgramCategory.Technology,
                        EstimatedDuration = "4 weeks at 2 hours a week",
                        SkillsGained = "Basic mechanics, block-based coding, logical thinking",
                        Rating = 4.7m,
                        TotalReviews = 128,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1518314916381-77a37c2a49ae?q=80&w=1171&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 1_200_000m,
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
                        Category = ProgramCategory.Technology,
                        EstimatedDuration = "8 weeks at 4 hours a week",
                        SkillsGained = "HTML, CSS, JavaScript, Web Design",
                        Rating = 4.5m,
                        TotalReviews = 214,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1498050108023-c5249f4df085?q=80&w=1172&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 2_200_000m,
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
                        Category = ProgramCategory.Engineering,
                        EstimatedDuration = "5 weeks at 3 hours a week",
                        SkillsGained = "Electronics, MQTT, sensor integration, prototyping",
                        Rating = 4.3m,
                        TotalReviews = 76,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1596658591534-591d75e2f2f7?q=80&w=1171&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 1_950_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    // ── 10 additional programs ──
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-PYBASIC",
                        Name = "Python for Beginners",
                        SeriesName = "Coding Foundations",
                        Description = "Learn the fundamentals of Python programming through interactive exercises and real-world mini-projects.",
                        Level = DifficultyLevel.Beginner,
                        Category = ProgramCategory.Technology,
                        EstimatedDuration = "6 weeks at 3 hours a week",
                        SkillsGained = "Python syntax, loops, functions, basic data structures",
                        Rating = 4.9m,
                        TotalReviews = 312,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1649180556628-9ba704115795?q=80&w=1162&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 1_450_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-MATHFUN",
                        Name = "Fun with Mathematics",
                        SeriesName = "Math Explorers",
                        Description = "Explore algebra, geometry, and number theory through puzzles, games, and visual explanations.",
                        Level = DifficultyLevel.Beginner,
                        Category = ProgramCategory.Mathematic,
                        EstimatedDuration = "5 weeks at 2 hours a week",
                        SkillsGained = "Algebra, geometry, logical reasoning, mental math",
                        Rating = 4.6m,
                        TotalReviews = 189,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1635372722656-389f87a941b7?q=80&w=1331&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 1_100_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-DIGART",
                        Name = "Digital Art & Illustration",
                        SeriesName = "Creative Studio",
                        Description = "Create stunning digital artwork using industry-standard tools and techniques for character design and illustration.",
                        Level = DifficultyLevel.Beginner,
                        Category = ProgramCategory.Art,
                        EstimatedDuration = "8 weeks at 3 hours a week",
                        SkillsGained = "Digital drawing, color theory, composition, character design",
                        Rating = 4.7m,
                        TotalReviews = 143,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1588876315093-ce09afb34028?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 1_800_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-BIOTECH",
                        Name = "Introduction to Biotechnology",
                        SeriesName = "Life Sciences",
                        Description = "Discover the science of biotechnology through lab simulations, genetics experiments, and real-world case studies.",
                        Level = DifficultyLevel.Intermediate,
                        Category = ProgramCategory.Science,
                        EstimatedDuration = "7 weeks at 3 hours a week",
                        SkillsGained = "Cell biology, genetics, lab techniques, scientific analysis",
                        Rating = 4.4m,
                        TotalReviews = 88,
                        ThumbnailUrl = "https://plus.unsplash.com/premium_photo-1661380732508-93beb2601f24?q=80&w=1169&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 2_050_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-3DDESIGN",
                        Name = "3D Modeling & Design",
                        SeriesName = "Engineering Design Track",
                        Description = "Learn to design 3D models for printing and engineering applications using industry-standard CAD tools.",
                        Level = DifficultyLevel.Intermediate,
                        Category = ProgramCategory.Engineering,
                        EstimatedDuration = "6 weeks at 4 hours a week",
                        SkillsGained = "CAD modeling, 3D printing, prototyping, design thinking",
                        Rating = 4.5m,
                        TotalReviews = 102,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1547194936-28214bd75193?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 2_300_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-AIBASIC",
                        Name = "AI & Machine Learning for Kids",
                        SeriesName = "Future Tech",
                        Description = "Understand how AI works through hands-on experiments with image recognition, chatbots, and data training.",
                        Level = DifficultyLevel.Intermediate,
                        Category = ProgramCategory.Technology,
                        EstimatedDuration = "8 weeks at 3 hours a week",
                        SkillsGained = "AI concepts, machine learning basics, data thinking, Python ML libraries",
                        Rating = 4.8m,
                        TotalReviews = 256,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1526378722484-bd91ca387e72?q=80&w=1334&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 2_450_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-ENVSCI",
                        Name = "Environmental Science & Sustainability",
                        SeriesName = "Green Future",
                        Description = "Study environmental systems, climate change, and sustainable solutions through field projects and research.",
                        Level = DifficultyLevel.Beginner,
                        Category = ProgramCategory.Science,
                        EstimatedDuration = "5 weeks at 2 hours a week",
                        SkillsGained = "Ecology, sustainability, data collection, environmental analysis",
                        Rating = 4.6m,
                        TotalReviews = 134,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1542601906990-b4d3fb778b09?q=80&w=1313&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 1_350_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-GAMEDEV",
                        Name = "Game Design & Development",
                        SeriesName = "Game Creators Studio",
                        Description = "Build your own 2D games from scratch by learning game logic, level design, and basic programming concepts.",
                        Level = DifficultyLevel.Intermediate,
                        Category = ProgramCategory.Technology,
                        EstimatedDuration = "10 weeks at 4 hours a week",
                        SkillsGained = "Game logic, level design, sprite animation, basic programming",
                        Rating = 4.9m,
                        TotalReviews = 378,
                        ThumbnailUrl = "https://plus.unsplash.com/premium_photo-1721080251127-76315300cc5c?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 2_700_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-MUSICTECH",
                        Name = "Music Production & Technology",
                        SeriesName = "Creative Studio",
                        Description = "Create original music tracks using digital audio workstations, sound design, and music theory fundamentals.",
                        Level = DifficultyLevel.Beginner,
                        Category = ProgramCategory.Art,
                        EstimatedDuration = "6 weeks at 2 hours a week",
                        SkillsGained = "DAW skills, music theory, sound design, audio mixing",
                        Rating = 4.5m,
                        TotalReviews = 91,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1598488035139-bdbb2231ce04?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 1_600_000m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    },
                    new Program
                    {
                        Id = Guid.NewGuid(),
                        Code = "PRG-DATAMATH",
                        Name = "Statistics & Data Analysis",
                        SeriesName = "Math Explorers",
                        Description = "Learn to collect, visualize, and interpret data using statistics and probability — the math behind everyday decisions.",
                        Level = DifficultyLevel.Advanced,
                        Category = ProgramCategory.Mathematic,
                        EstimatedDuration = "7 weeks at 3 hours a week",
                        SkillsGained = "Statistics, probability, data visualization, spreadsheet analysis",
                        Rating = 4.4m,
                        TotalReviews = 67,
                        ThumbnailUrl = "https://images.unsplash.com/photo-1666875753105-c63a6f3bdc86?q=80&w=1173&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
                        Status = "Active",
                        Price = 1_950_000m,
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

            _loggerService.LogInformation("Starting seed program boards");
            var existingProgramBoards = await _unitOfWork.ProgramBoards.GetAllAsync();

            if (!existingProgramBoards.Any())
            {
                var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001");
                var expert002 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-002");
                var expert003 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-003");
                var expert004 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-004");
                var expert005 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-005");
                var expert006 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-006");
                var expert007 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-007");

                var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
                var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
                var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");
                var programPyBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-PYBASIC");
                var programMathFun = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-MATHFUN");
                var programDigArt = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-DIGART");
                var programBiotech = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-BIOTECH");
                var programAiBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-AIBASIC");
                var programEnvSci = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ENVSCI");
                var programGameDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-GAMEDEV");
                var programMusicTech = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-MUSICTECH");
                var programDataMath = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-DATAMATH");
                var program3DDesign = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-3DDESIGN");

                var programBoards = new List<ProgramBoard>();
                var seedUtc = DateTime.UtcNow;

                void AddBoard(Expert? expert, Program? program, string role)
                {
                    if (expert == null || program == null)
                        return;

                    programBoards.Add(new ProgramBoard
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = program.Id,
                        ExpertId = expert.Id,
                        RoleInBoard = role,
                        CreatedAt = seedUtc,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                AddBoard(expert001, programRobotics, "Lead Robotics Advisor");
                AddBoard(expert001, programIot, "IoT Hardware Mentor");
                AddBoard(expert002, programRobotics, "STEAM Curriculum Advisor");
                AddBoard(expert002, programBiotech, "Life Sciences Board Member");
                AddBoard(expert003, programWebDev, "Lead Web Development Advisor");
                AddBoard(expert003, programGameDev, "Game Development Mentor");
                AddBoard(expert004, programAiBasic, "AI Program Director");
                AddBoard(expert004, programPyBasic, "Programming Advisor");
                AddBoard(expert004, programDataMath, "Data Science Board Member");
                AddBoard(expert005, programMathFun, "Lead Mathematics Advisor");
                AddBoard(expert005, programDataMath, "Statistics Board Member");
                AddBoard(expert006, programDigArt, "Lead Digital Arts Advisor");
                AddBoard(expert006, program3DDesign, "3D Design Mentor");
                AddBoard(expert006, programMusicTech, "Creative Arts Board Member");
                AddBoard(expert007, programEnvSci, "Environmental Science Director");
                AddBoard(expert007, programBiotech, "Sustainability Advisor");
                AddBoard(expert002, programEnvSci, "STEAM Outreach Advisor");

                if (programBoards.Any())
                {
                    await _unitOfWork.ProgramBoards.AddRangeAsync(programBoards);
                    await _unitOfWork.SaveChangesAsync();
                }

                _loggerService.LogInformation("Finished seed program boards");
            }
            else
            {
                _loggerService.LogInformation("Program boards already exist, skipping program board seeding");
            }

            _loggerService.LogInformation("Starting seed modules");
            var existingModules = await _unitOfWork.Modules.GetAllAsync();
            if (!existingModules.Any())
            {
                var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
                var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
                var programSteam = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-STEAM-01");
                var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");
                var programPyBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-PYBASIC");
                var programMathFun = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-MATHFUN");
                var programDigArt = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-DIGART");
                var programBiotech = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-BIOTECH");
                var program3DDesign = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-3DDESIGN");
                var programAiBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-AIBASIC");
                var programEnvSci = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ENVSCI");
                var programGameDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-GAMEDEV");
                var programMusicTech = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-MUSICTECH");
                var programDataMath = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-DATAMATH");

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
                            Price = 450_000m,
                            RetakeFee = 100_000m,
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
                            Price = 500_000m,
                            RetakeFee = 120_000m,
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
                            Price = 550_000m,
                            RetakeFee = 150_000m,
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
                            Price = 700_000m,
                            RetakeFee = 150_000m,
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
                            Price = 800_000m,
                            RetakeFee = 180_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-WEBDEV-03",
                            ProgramId = programWebDev.Id,
                            Name = "Responsive Design & Deployment",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 700_000m,
                            RetakeFee = 150_000m,
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
                            Price = 500_000m,
                            RetakeFee = 110_000m,
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
                            Price = 550_000m,
                            RetakeFee = 120_000m,
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
                            Price = 650_000m,
                            RetakeFee = 130_000m,
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
                            Price = 700_000m,
                            RetakeFee = 150_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-IOT-03",
                            ProgramId = programIot.Id,
                            Name = "IoT Project Showcase",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 600_000m,
                            RetakeFee = 120_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-IOT not found. Skipping IoT module seeding.");
                }

                // ── PRG-PYBASIC ──────────────────────────────────────────────────
                if (programPyBasic != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-PYBASIC-01",
                            ProgramId = programPyBasic.Id,
                            Name = "Python Syntax & Data Types",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 480_000m,
                            RetakeFee = 100_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-PYBASIC-02",
                            ProgramId = programPyBasic.Id,
                            Name = "Control Flow & Functions",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 500_000m,
                            RetakeFee = 110_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-PYBASIC-03",
                            ProgramId = programPyBasic.Id,
                            Name = "Mini-Project: Python Game",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 470_000m,
                            RetakeFee = 100_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-PYBASIC not found. Skipping Python module seeding.");
                }

                // ── PRG-MATHFUN ──────────────────────────────────────────────────
                if (programMathFun != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-MATHFUN-01",
                            ProgramId = programMathFun.Id,
                            Name = "Algebra & Number Patterns",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 380_000m,
                            RetakeFee = 80_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-MATHFUN-02",
                            ProgramId = programMathFun.Id,
                            Name = "Geometry & Spatial Thinking",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 380_000m,
                            RetakeFee = 80_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-MATHFUN-03",
                            ProgramId = programMathFun.Id,
                            Name = "Math Puzzle Challenge",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 340_000m,
                            RetakeFee = 70_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-MATHFUN not found. Skipping Math module seeding.");
                }

                // ── PRG-DIGART ──────────────────────────────────────────────────
                if (programDigArt != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-DIGART-01",
                            ProgramId = programDigArt.Id,
                            Name = "Color Theory & Digital Tools",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 600_000m,
                            RetakeFee = 130_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-DIGART-02",
                            ProgramId = programDigArt.Id,
                            Name = "Character Design Workshop",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 650_000m,
                            RetakeFee = 140_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-DIGART-03",
                            ProgramId = programDigArt.Id,
                            Name = "Portfolio Illustration Project",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 550_000m,
                            RetakeFee = 110_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-DIGART not found. Skipping Digital Art module seeding.");
                }

                // ── PRG-BIOTECH ──────────────────────────────────────────────────
                if (programBiotech != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-BIOTECH-01",
                            ProgramId = programBiotech.Id,
                            Name = "Cell Biology Fundamentals",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 700_000m,
                            RetakeFee = 150_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-BIOTECH-02",
                            ProgramId = programBiotech.Id,
                            Name = "Genetics & Lab Simulation",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 750_000m,
                            RetakeFee = 160_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-BIOTECH-03",
                            ProgramId = programBiotech.Id,
                            Name = "Biotechnology Case Study",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 600_000m,
                            RetakeFee = 120_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-BIOTECH not found. Skipping Biotech module seeding.");
                }

                // ── PRG-3DDESIGN ─────────────────────────────────────────────────
                if (program3DDesign != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-3DDESIGN-01",
                            ProgramId = program3DDesign.Id,
                            Name = "CAD Basics & Interface",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 800_000m,
                            RetakeFee = 170_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-3DDESIGN-02",
                            ProgramId = program3DDesign.Id,
                            Name = "3D Modeling Practice",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 850_000m,
                            RetakeFee = 180_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-3DDESIGN-03",
                            ProgramId = program3DDesign.Id,
                            Name = "Print & Prototype Challenge",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 650_000m,
                            RetakeFee = 130_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-3DDESIGN not found. Skipping 3D Design module seeding.");
                }

                // ── PRG-AIBASIC ──────────────────────────────────────────────────
                if (programAiBasic != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-AIBASIC-01",
                            ProgramId = programAiBasic.Id,
                            Name = "What is AI? Concepts & History",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 800_000m,
                            RetakeFee = 170_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-AIBASIC-02",
                            ProgramId = programAiBasic.Id,
                            Name = "Image Recognition Hands-On",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 850_000m,
                            RetakeFee = 180_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-AIBASIC-03",
                            ProgramId = programAiBasic.Id,
                            Name = "Build Your Own Chatbot",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 800_000m,
                            RetakeFee = 160_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-AIBASIC not found. Skipping AI module seeding.");
                }

                // ── PRG-ENVSCI ───────────────────────────────────────────────────
                if (programEnvSci != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-ENVSCI-01",
                            ProgramId = programEnvSci.Id,
                            Name = "Ecology & Ecosystems",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 450_000m,
                            RetakeFee = 90_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-ENVSCI-02",
                            ProgramId = programEnvSci.Id,
                            Name = "Climate Change Field Research",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 500_000m,
                            RetakeFee = 100_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-ENVSCI-03",
                            ProgramId = programEnvSci.Id,
                            Name = "Sustainability Action Project",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 400_000m,
                            RetakeFee = 80_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-ENVSCI not found. Skipping Environmental Science module seeding.");
                }

                // ── PRG-GAMEDEV ──────────────────────────────────────────────────
                if (programGameDev != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-GAMEDEV-01",
                            ProgramId = programGameDev.Id,
                            Name = "Game Design Fundamentals",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 900_000m,
                            RetakeFee = 190_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-GAMEDEV-02",
                            ProgramId = programGameDev.Id,
                            Name = "2D Sprite & Level Design",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 950_000m,
                            RetakeFee = 200_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-GAMEDEV-03",
                            ProgramId = programGameDev.Id,
                            Name = "Game Logic & Scripting",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 3,
                            IsMandatory = true,
                            Price = 950_000m,
                            RetakeFee = 200_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-GAMEDEV-04",
                            ProgramId = programGameDev.Id,
                            Name = "Publish Your Game",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 4,
                            IsMandatory = false,
                            Price = 900_000m,
                            RetakeFee = 180_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-GAMEDEV not found. Skipping Game Dev module seeding.");
                }

                // ── PRG-MUSICTECH ────────────────────────────────────────────────
                if (programMusicTech != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-MUSICTECH-01",
                            ProgramId = programMusicTech.Id,
                            Name = "Music Theory Essentials",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 550_000m,
                            RetakeFee = 110_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-MUSICTECH-02",
                            ProgramId = programMusicTech.Id,
                            Name = "DAW Production Workshop",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 600_000m,
                            RetakeFee = 120_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-MUSICTECH-03",
                            ProgramId = programMusicTech.Id,
                            Name = "Original Track Showcase",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 450_000m,
                            RetakeFee = 90_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-MUSICTECH not found. Skipping Music Tech module seeding.");
                }

                // ── PRG-DATAMATH ─────────────────────────────────────────────────
                if (programDataMath != null)
                {
                    modules.AddRange(new List<Module>
                    {
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-DATAMATH-01",
                            ProgramId = programDataMath.Id,
                            Name = "Statistics & Probability",
                            ModuleType = ModuleType.Theory,
                            ModuleOrder = 1,
                            IsMandatory = true,
                            Price = 650_000m,
                            RetakeFee = 130_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-DATAMATH-02",
                            ProgramId = programDataMath.Id,
                            Name = "Data Visualization Lab",
                            ModuleType = ModuleType.Experiential,
                            ModuleOrder = 2,
                            IsMandatory = true,
                            Price = 700_000m,
                            RetakeFee = 140_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        },
                        new Module
                        {
                            Id = Guid.NewGuid(),
                            Code = "MOD-DATAMATH-03",
                            ProgramId = programDataMath.Id,
                            Name = "Real-World Data Analysis Project",
                            ModuleType = ModuleType.Research,
                            ModuleOrder = 3,
                            IsMandatory = false,
                            Price = 600_000m,
                            RetakeFee = 120_000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = Guid.Empty
                        }
                    });
                }
                else
                {
                    _loggerService.LogWarning("Program PRG-DATAMATH not found. Skipping Data Math module seeding.");
                }

                if (modules.Count > 0)
                {
                    foreach (var module in modules)
                    {
                        if (module.LearningOutcomes.Length > 0)
                        {
                            continue;
                        }

                        module.LearningOutcomes = module.Code switch
                        {
                            "MOD-ROBOTICS-01" => new[]
                            {
                                "Understand core robotics concepts and components",
                                "Identify basic mechanical structures and actuators",
                                "Follow safety practices in a robotics lab"
                            },
                            "MOD-ROBOTICS-02" => new[]
                            {
                                "Work with common sensors and read sensor data",
                                "Implement movement control for simple robots",
                                "Debug and calibrate sensor-based behaviors"
                            },
                            "MOD-ROBOTICS-03" => new[]
                            {
                                "Plan and build a small robot for a challenge",
                                "Test, iterate, and improve performance",
                                "Present results and reflect on engineering trade-offs"
                            },
                            "MOD-WEBDEV-01" => new[]
                            {
                                "Build semantic HTML structures",
                                "Style pages with modern CSS",
                                "Create clean layouts using Flexbox/Grid"
                            },
                            "MOD-WEBDEV-02" => new[]
                            {
                                "Write JavaScript with variables, functions, and control flow",
                                "Manipulate the DOM to create interactivity",
                                "Use debugging tools to fix common issues"
                            },
                            "MOD-WEBDEV-03" => new[]
                            {
                                "Design responsive pages for multiple screen sizes",
                                "Understand deployment basics and hosting",
                                "Ship a small web project end-to-end"
                            },
                            "MOD-STEAM-01" => new[]
                            {
                                "Understand STEAM learning workflow and lab rules",
                                "Explore core tools used in the program",
                                "Practice collaboration and documentation"
                            },
                            "MOD-STEAM-02" => new[]
                            {
                                "Prototype ideas using simple materials and tools",
                                "Iterate quickly based on feedback",
                                "Communicate design choices clearly"
                            },
                            "MOD-IOT-01" => new[]
                            {
                                "Understand microcontrollers and basic electronics",
                                "Read data from sensors in embedded projects",
                                "Build simple circuits safely"
                            },
                            "MOD-IOT-02" => new[]
                            {
                                "Send device data to the cloud",
                                "Understand connectivity basics (Wi‑Fi/API)",
                                "Monitor and troubleshoot IoT data flow"
                            },
                            "MOD-IOT-03" => new[]
                            {
                                "Build a small IoT prototype as a project",
                                "Demonstrate end-to-end device-to-cloud flow",
                                "Present outcomes and lessons learned"
                            },
                            "MOD-PYBASIC-01" => new[]
                            {
                                "Write Python syntax confidently",
                                "Work with core data types and operators",
                                "Practice clean coding and formatting"
                            },
                            "MOD-PYBASIC-02" => new[]
                            {
                                "Use conditions and loops effectively",
                                "Write and reuse functions",
                                "Solve small problems with structured thinking"
                            },
                            "MOD-PYBASIC-03" => new[]
                            {
                                "Apply Python fundamentals in a mini-project",
                                "Plan simple game logic and implement it",
                                "Test and refine features"
                            },
                            "MOD-MATHFUN-01" => new[]
                            {
                                "Build number sense through games",
                                "Practice basic arithmetic strategies",
                                "Develop confidence with math challenges"
                            },
                            "MOD-MATHFUN-02" => new[]
                            {
                                "Recognize patterns and sequences",
                                "Solve puzzles using logical reasoning",
                                "Explain solutions clearly"
                            },
                            "MOD-DIGART-01" => new[]
                            {
                                "Use basic digital drawing tools",
                                "Apply color and composition principles",
                                "Create simple artwork with layers"
                            },
                            "MOD-DIGART-02" => new[]
                            {
                                "Design characters or scenes digitally",
                                "Improve line, shading, and texture",
                                "Export artwork for sharing"
                            },
                            "MOD-BIOTECH-01" => new[]
                            {
                                "Understand basic biology and lab safety",
                                "Observe and document simple experiments",
                                "Learn scientific method basics"
                            },
                            "MOD-3DDESIGN-01" => new[]
                            {
                                "Model simple 3D shapes and objects",
                                "Understand dimensions and constraints",
                                "Prepare models for printing or presentation"
                            },
                            "MOD-AIBASIC-01" => new[]
                            {
                                "Understand what AI is and common applications",
                                "Learn basic ML concepts (data, training, prediction)",
                                "Discuss AI ethics at a beginner level"
                            },
                            _ => new[]
                            {
                                "Understand key concepts of this module",
                                "Practice skills through hands-on activities",
                                "Apply learning in a small project or assessment"
                            }
                        };
                    }

                    await _unitOfWork.Modules.AddRangeAsync(modules);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed modules — {Count} module(s) created.", modules.Count);
                }
                else
                {
                    _loggerService.LogWarning("No modules seeded because required programs were not found.");
                }
            }
            else
            {
                _loggerService.LogInformation("Modules already exist, skipping module seeding");

                var modulesToBackfill = await _unitOfWork.Modules.GetAllAsync(
                    m => !m.IsDeleted && (m.LearningOutcomes == null || m.LearningOutcomes.Length == 0));

                if (modulesToBackfill.Any())
                {
                    foreach (var module in modulesToBackfill)
                    {
                        module.LearningOutcomes = module.Code switch
                        {
                            "MOD-ROBOTICS-01" => new[]
                            {
                                "Understand core robotics concepts and components",
                                "Identify basic mechanical structures and actuators",
                                "Follow safety practices in a robotics lab"
                            },
                            "MOD-ROBOTICS-02" => new[]
                            {
                                "Work with common sensors and read sensor data",
                                "Implement movement control for simple robots",
                                "Debug and calibrate sensor-based behaviors"
                            },
                            "MOD-ROBOTICS-03" => new[]
                            {
                                "Plan and build a small robot for a challenge",
                                "Test, iterate, and improve performance",
                                "Present results and reflect on engineering trade-offs"
                            },
                            "MOD-WEBDEV-01" => new[]
                            {
                                "Build semantic HTML structures",
                                "Style pages with modern CSS",
                                "Create clean layouts using Flexbox/Grid"
                            },
                            "MOD-WEBDEV-02" => new[]
                            {
                                "Write JavaScript with variables, functions, and control flow",
                                "Manipulate the DOM to create interactivity",
                                "Use debugging tools to fix common issues"
                            },
                            "MOD-WEBDEV-03" => new[]
                            {
                                "Design responsive pages for multiple screen sizes",
                                "Understand deployment basics and hosting",
                                "Ship a small web project end-to-end"
                            },
                            "MOD-STEAM-01" => new[]
                            {
                                "Understand STEAM learning workflow and lab rules",
                                "Explore core tools used in the program",
                                "Practice collaboration and documentation"
                            },
                            "MOD-STEAM-02" => new[]
                            {
                                "Prototype ideas using simple materials and tools",
                                "Iterate quickly based on feedback",
                                "Communicate design choices clearly"
                            },
                            "MOD-IOT-01" => new[]
                            {
                                "Understand microcontrollers and basic electronics",
                                "Read data from sensors in embedded projects",
                                "Build simple circuits safely"
                            },
                            "MOD-IOT-02" => new[]
                            {
                                "Send device data to the cloud",
                                "Understand connectivity basics (Wi‑Fi/API)",
                                "Monitor and troubleshoot IoT data flow"
                            },
                            "MOD-IOT-03" => new[]
                            {
                                "Build a small IoT prototype as a project",
                                "Demonstrate end-to-end device-to-cloud flow",
                                "Present outcomes and lessons learned"
                            },
                            "MOD-PYBASIC-01" => new[]
                            {
                                "Write Python syntax confidently",
                                "Work with core data types and operators",
                                "Practice clean coding and formatting"
                            },
                            "MOD-PYBASIC-02" => new[]
                            {
                                "Use conditions and loops effectively",
                                "Write and reuse functions",
                                "Solve small problems with structured thinking"
                            },
                            "MOD-PYBASIC-03" => new[]
                            {
                                "Apply Python fundamentals in a mini-project",
                                "Plan simple game logic and implement it",
                                "Test and refine features"
                            },
                            "MOD-MATHFUN-01" => new[]
                            {
                                "Build number sense through games",
                                "Practice basic arithmetic strategies",
                                "Develop confidence with math challenges"
                            },
                            "MOD-MATHFUN-02" => new[]
                            {
                                "Recognize patterns and sequences",
                                "Solve puzzles using logical reasoning",
                                "Explain solutions clearly"
                            },
                            "MOD-DIGART-01" => new[]
                            {
                                "Use basic digital drawing tools",
                                "Apply color and composition principles",
                                "Create simple artwork with layers"
                            },
                            "MOD-DIGART-02" => new[]
                            {
                                "Design characters or scenes digitally",
                                "Improve line, shading, and texture",
                                "Export artwork for sharing"
                            },
                            "MOD-BIOTECH-01" => new[]
                            {
                                "Understand basic biology and lab safety",
                                "Observe and document simple experiments",
                                "Learn scientific method basics"
                            },
                            "MOD-3DDESIGN-01" => new[]
                            {
                                "Model simple 3D shapes and objects",
                                "Understand dimensions and constraints",
                                "Prepare models for printing or presentation"
                            },
                            "MOD-AIBASIC-01" => new[]
                            {
                                "Understand what AI is and common applications",
                                "Learn basic ML concepts (data, training, prediction)",
                                "Discuss AI ethics at a beginner level"
                            },
                            _ => new[]
                            {
                                "Understand key concepts of this module",
                                "Practice skills through hands-on activities",
                                "Apply learning in a small project or assessment"
                            }
                        };
                    }

                    await _unitOfWork.Modules.UpdateRange(modulesToBackfill);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation(
                        "Backfilled LearningOutcomes for {Count} existing module(s).",
                        modulesToBackfill.Count);
                }
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
                        ProgressPercent = 0m,
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
                        ProgressPercent = 0m,
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
                var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
                var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
                var programEnrollmentStudent1 = student1 != null && programRobotics != null
                    ? await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                        pe => pe.StudentId == student1.Id && pe.ProgramId == programRobotics.Id && !pe.IsDeleted)
                    : null;
                var programEnrollmentStudent2 = student2 != null && programWebDev != null
                    ? await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                        pe => pe.StudentId == student2.Id && pe.ProgramId == programWebDev.Id && !pe.IsDeleted)
                    : null;
                var enrollTime = DateTime.UtcNow;

                var moduleEnrollments = new List<ModuleEnrollment>();

                if (student1 != null && moduleRobotics1 != null)
                {
                    moduleEnrollments.Add(new ModuleEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student1.Id,
                        ModuleId = moduleRobotics1.Id,
                        ProgramEnrollmentId = programEnrollmentStudent1?.Id,
                        Status = EnrollmentStatus.Active,
                        ProgressPercent = 0m,
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
                        ProgramEnrollmentId = programEnrollmentStudent2?.Id,
                        Status = EnrollmentStatus.Active,
                        ProgressPercent = 0m,
                        EnrolledAt = enrollTime.AddDays(-5),
                        StartedAt = enrollTime.AddDays(-4),
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
                var offlineActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-03-03");

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
                            PaidById = student1.Id,
                            ProgramEnrollmentId = programEnrollment.Id,
                            ModuleEnrollmentId = null,
                            Amount = 1_200_000m,
                            Gateway = PaymentGateway.Stripe,
                            TransactionId = "STRIPE-TXN-001",
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

            _loggerService.LogInformation("Starting seed program reviews");
            var existingProgramReviews = await _unitOfWork.ProgramReviews.GetAllAsync();
            if (!existingProgramReviews.Any())
            {
                var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
                var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
                var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");
                var student4 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-004");
                var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
                var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
                var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");
                var programPyBasic = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-PYBASIC");
                var programGameDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-GAMEDEV");
                var reviewTime = DateTime.UtcNow;

                var programReviews = new List<ProgramReview>();

                if (student1 != null && programRobotics != null)
                {
                    programReviews.Add(new ProgramReview
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programRobotics.Id,
                        StudentId = student1.Id,
                        StarRating = 5,
                        Comment = "Chương trình thực sự thú vị! Tôi đã học được rất nhiều về robotics từ căn bản đến nâng cao. Các hoạt động thực hành rất bổ ích.",
                        CreatedAt = reviewTime.AddDays(-10),
                        CreatedBy = student1.Id,
                        IsDeleted = false
                    });
                }

                if (student2 != null && programRobotics != null)
                {
                    programReviews.Add(new ProgramReview
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programRobotics.Id,
                        StudentId = student2.Id,
                        StarRating = 4,
                        Comment = "Nội dung phong phú, mentor nhiệt tình. Chỉ tiếc thời lượng hơi ngắn so với khối lượng kiến thức.",
                        CreatedAt = reviewTime.AddDays(-8),
                        CreatedBy = student2.Id,
                        IsDeleted = false
                    });
                }

                if (student1 != null && programWebDev != null)
                {
                    programReviews.Add(new ProgramReview
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programWebDev.Id,
                        StudentId = student1.Id,
                        StarRating = 5,
                        Comment = "Bootcamp web dev cực hay! Sau khoá học tôi đã tự xây dựng được trang web cá nhân. Rất đáng tiền.",
                        CreatedAt = reviewTime.AddDays(-6),
                        CreatedBy = student1.Id,
                        IsDeleted = false
                    });
                }

                if (student3 != null && programWebDev != null)
                {
                    programReviews.Add(new ProgramReview
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programWebDev.Id,
                        StudentId = student3.Id,
                        StarRating = 4,
                        Comment = "Giảng viên giải thích rõ ràng, bài tập thực hành đa dạng. Tôi đã cải thiện kỹ năng CSS rất nhiều.",
                        CreatedAt = reviewTime.AddDays(-5),
                        CreatedBy = student3.Id,
                        IsDeleted = false
                    });
                }

                if (student2 != null && programIot != null)
                {
                    programReviews.Add(new ProgramReview
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programIot.Id,
                        StudentId = student2.Id,
                        StarRating = 4,
                        Comment = "IoT Fundamentals rất bổ ích cho ai muốn tìm hiểu về thiết bị thông minh. Phần cloud connectivity là điểm nhấn.",
                        CreatedAt = reviewTime.AddDays(-12),
                        CreatedBy = student2.Id,
                        IsDeleted = false
                    });
                }

                if (student4 != null && programIot != null)
                {
                    programReviews.Add(new ProgramReview
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programIot.Id,
                        StudentId = student4.Id,
                        StarRating = 3,
                        Comment = "Nội dung ổn nhưng cần thêm tài liệu tham khảo bằng tiếng Việt. Phần thực hành cần nhiều kit hơn.",
                        CreatedAt = reviewTime.AddDays(-3),
                        CreatedBy = student4.Id,
                        IsDeleted = false
                    });
                }

                if (student1 != null && programPyBasic != null)
                {
                    programReviews.Add(new ProgramReview
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programPyBasic.Id,
                        StudentId = student1.Id,
                        StarRating = 5,
                        Comment = "Khoá Python cho người mới bắt đầu này cực kỳ dễ hiểu! Tôi chưa có kinh nghiệm lập trình nhưng sau 6 tuần đã viết được game nhỏ.",
                        CreatedAt = reviewTime.AddDays(-15),
                        CreatedBy = student1.Id,
                        IsDeleted = false
                    });
                }

                if (student3 != null && programGameDev != null)
                {
                    programReviews.Add(new ProgramReview
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programGameDev.Id,
                        StudentId = student3.Id,
                        StarRating = 5,
                        Comment = "Game Design & Development là khoá học yêu thích nhất của tôi! Tôi đã publish được game 2D đầu tiên sau khi hoàn thành.",
                        CreatedAt = reviewTime.AddDays(-7),
                        CreatedBy = student3.Id,
                        IsDeleted = false
                    });
                }

                if (student4 != null && programGameDev != null)
                {
                    programReviews.Add(new ProgramReview
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = programGameDev.Id,
                        StudentId = student4.Id,
                        StarRating = 4,
                        Comment = "Nội dung phong phú, hướng dẫn chi tiết từng bước. Phần sprite animation rất thú vị và sáng tạo.",
                        CreatedAt = reviewTime.AddDays(-2),
                        CreatedBy = student4.Id,
                        IsDeleted = false
                    });
                }

                if (programReviews.Count > 0)
                {
                    await _unitOfWork.ProgramReviews.AddRangeAsync(programReviews);
                    await _unitOfWork.SaveChangesAsync();
                    _loggerService.LogInformation("Finished seed program reviews — {Count} review(s) created.", programReviews.Count);
                }
                else
                {
                    _loggerService.LogWarning("No program reviews seeded.");
                }
            }
            else
            {
                _loggerService.LogInformation("Program reviews already exist, skipping seeding");
            }

            await SeedClassFlowAsync();
            await SeedResearchMilestoneDataAsync();
            await SeedResearchModuleEnrollmentsAsync();
            await SeedResearchActivityProgressAsync();
            await SeedEnrollmentActivityProgressAsync();
            await BackfillActivityProgressStatusAsync();
            await SeedResearchSubmissionsAsync();
            await SeedExtendedResearchDataAsync();
            await SeedMaterialsAsync();

            _loggerService.LogInformation("Finished seed all data");
        }

        private async Task SeedClassFlowAsync()
        {
            _loggerService.LogInformation("Starting seed class flow");
            var existingClass = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == "CLS-ROBOTICS-2026A");
            if (existingClass != null)
            {
                _loggerService.LogInformation("Class flow already seeded, skipping");
                return;
            }

            var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
            var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            var moduleRobotics1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
            var activityLive = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-01-02");
            var activityUpcoming = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-01-03");
            var assignmentQuiz = await _unitOfWork.Assignments.FirstOrDefaultAsync(a => a.Code == "ASG-ROBOTICS-QUIZ-01");

            if (programRobotics == null || mentor == null || student1 == null || student2 == null
                || moduleRobotics1 == null || activityLive == null || activityUpcoming == null)
            {
                _loggerService.LogWarning("Required entities for class flow not found. Skipping.");
                return;
            }

            var seedTime = DateTime.UtcNow;
            var classStart = seedTime.AddDays(-14);
            var classEnd = seedTime.AddDays(90);

            var activeClass = new Class
            {
                Id = Guid.NewGuid(),
                Code = "CLS-ROBOTICS-2026A",
                Name = "Robotics Spring 2026 - Cohort A",
                ProgramId = programRobotics.Id,
                MentorId = mentor.Id,
                StartDate = classStart,
                EndDate = classEnd,
                MaxCapacity = 25,
                Status = ClassStatus.InProgress,
                MinHoursBeforeAssignmentJoin = 48,
                ScheduleSummary = "Every Saturday 9:00-12:00",
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            var openClass = new Class
            {
                Id = Guid.NewGuid(),
                Code = "CLS-ROBOTICS-2026B",
                Name = "Robotics Summer 2026 - Cohort B",
                ProgramId = programRobotics.Id,
                MentorId = mentor.Id,
                StartDate = seedTime.AddDays(21),
                EndDate = seedTime.AddDays(120),
                MaxCapacity = 20,
                Status = ClassStatus.Open,
                MinHoursBeforeAssignmentJoin = 48,
                ScheduleSummary = "Every Sunday 14:00-17:00",
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            await _unitOfWork.Classes.AddRangeAsync(new List<Class> { activeClass, openClass });
            await _unitOfWork.SaveChangesAsync();

            var student1ProgramEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student1.Id
                      && pe.ProgramId == programRobotics.Id
                      && !pe.IsDeleted);

            if (student1ProgramEnrollment == null)
            {
                student1ProgramEnrollment = new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student1.Id,
                    ProgramId = programRobotics.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 25m,
                    EnrolledAt = seedTime.AddDays(-14),
                    StartedAt = seedTime.AddDays(-10),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };
                await _unitOfWork.ProgramEnrollments.AddAsync(student1ProgramEnrollment);
                await _unitOfWork.SaveChangesAsync();
            }

            var student2ProgramEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student2.Id
                      && pe.ProgramId == programRobotics.Id
                      && !pe.IsDeleted);

            if (student2ProgramEnrollment == null)
            {
                student2ProgramEnrollment = new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student2.Id,
                    ProgramId = programRobotics.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 5m,
                    EnrolledAt = seedTime.AddDays(-5),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };
                await _unitOfWork.ProgramEnrollments.AddAsync(student2ProgramEnrollment);
                await _unitOfWork.SaveChangesAsync();
            }

            var student1ModuleEnrollment = await EnsureModuleEnrollmentForClassAsync(
                student1.Id,
                moduleRobotics1.Id,
                student1ProgramEnrollment.Id,
                seedTime,
                progressPercent: 40m);

            var student2ModuleEnrollment = await EnsureModuleEnrollmentForClassAsync(
                student2.Id,
                moduleRobotics1.Id,
                student2ProgramEnrollment.Id,
                seedTime,
                progressPercent: 10m);

            var classEnrollments = new List<ClassEnrollment>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassId = activeClass.Id,
                    StudentId = student1.Id,
                    ProgramEnrollmentId = student1ProgramEnrollment.Id,
                    Status = ClassEnrollmentStatus.Active,
                    EnrolledAt = seedTime.AddDays(-10),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassId = activeClass.Id,
                    StudentId = student2.Id,
                    ProgramEnrollmentId = student2ProgramEnrollment.Id,
                    Status = ClassEnrollmentStatus.Active,
                    EnrolledAt = seedTime.AddDays(-8),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                }
            };

            await _unitOfWork.ClassEnrollments.AddRangeAsync(classEnrollments);
            await _unitOfWork.SaveChangesAsync();

            var sessionPast = new ClassSession
            {
                Id = Guid.NewGuid(),
                ClassId = activeClass.Id,
                ModuleId = moduleRobotics1.Id,
                ActivityId = activityLive.Id,
                SessionKind = SessionKind.LiveOnline,
                Title = "Introduction Lecture",
                Description = "Live online introduction to robotics for cohort A.",
                StartTime = seedTime.AddDays(-7).Date.AddHours(9),
                EndTime = seedTime.AddDays(-7).Date.AddHours(11),
                Location = "https://meet.google.com/robotics-cohort-a-intro",
                RequiresAttendance = true,
                Status = ClassSessionStatus.Completed,
                CreatedAt = seedTime,
                CreatedBy = mentor.Id,
                IsDeleted = false
            };

            var sessionUpcoming = new ClassSession
            {
                Id = Guid.NewGuid(),
                ClassId = activeClass.Id,
                ModuleId = moduleRobotics1.Id,
                ActivityId = activityUpcoming.Id,
                SessionKind = SessionKind.LiveOnline,
                Title = "Chassis Design Workshop",
                Description = "Live online workshop for robot chassis design and planning.",
                StartTime = seedTime.AddDays(7).Date.AddHours(14),
                EndTime = seedTime.AddDays(7).Date.AddHours(16),
                Location = "https://meet.google.com/robotics-chassis-workshop",
                RequiresAttendance = true,
                Status = ClassSessionStatus.Scheduled,
                CreatedAt = seedTime,
                CreatedBy = mentor.Id,
                IsDeleted = false
            };

            var sessions = new List<ClassSession> { sessionPast, sessionUpcoming };

            if (assignmentQuiz != null)
            {
                sessions.Add(new ClassSession
                {
                    Id = Guid.NewGuid(),
                    ClassId = activeClass.Id,
                    ModuleId = moduleRobotics1.Id,
                    AssignmentId = assignmentQuiz.Id,
                    SessionKind = SessionKind.AssignmentWindow,
                    Title = "Robotics Fundamentals Quiz Window",
                    Description = "Assignment window for the module quiz.",
                    StartTime = seedTime.AddDays(14).Date.AddHours(9),
                    EndTime = seedTime.AddDays(14).Date.AddHours(12),
                    RequiresAttendance = false,
                    Status = ClassSessionStatus.Scheduled,
                    CreatedAt = seedTime,
                    CreatedBy = mentor.Id,
                    IsDeleted = false
                });
            }

            await _unitOfWork.ClassSessions.AddRangeAsync(sessions);
            await _unitOfWork.SaveChangesAsync();

            var attendances = new List<SessionAttendance>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = sessionPast.Id,
                    StudentId = student1.Id,
                    ModuleEnrollmentId = student1ModuleEnrollment.Id,
                    Status = AttendanceStatus.Present,
                    CheckedInAt = sessionPast.StartTime.AddMinutes(5),
                    RecordedBy = mentor.Id,
                    CreatedAt = seedTime,
                    CreatedBy = mentor.Id,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = sessionPast.Id,
                    StudentId = student2.Id,
                    ModuleEnrollmentId = student2ModuleEnrollment.Id,
                    Status = AttendanceStatus.Late,
                    CheckedInAt = sessionPast.StartTime.AddMinutes(25),
                    RecordedBy = mentor.Id,
                    CreatedAt = seedTime,
                    CreatedBy = mentor.Id,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = sessionUpcoming.Id,
                    StudentId = student1.Id,
                    ModuleEnrollmentId = student1ModuleEnrollment.Id,
                    Status = AttendanceStatus.Expected,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = sessionUpcoming.Id,
                    StudentId = student2.Id,
                    ModuleEnrollmentId = student2ModuleEnrollment.Id,
                    Status = AttendanceStatus.Expected,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                }
            };

            await _unitOfWork.SessionAttendances.AddRangeAsync(attendances);
            await _unitOfWork.SaveChangesAsync();

            _loggerService.LogInformation(
                "Finished seed class flow — 2 class(es), 2 enrollment(s), {SessionCount} session(s), {AttendanceCount} attendance(s).",
                sessions.Count,
                attendances.Count);
        }

        private async Task<ModuleEnrollment> EnsureModuleEnrollmentForClassAsync(
            Guid studentId,
            Guid moduleId,
            Guid programEnrollmentId,
            DateTime seedTime,
            decimal progressPercent)
        {
            var enrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == studentId
                      && me.ModuleId == moduleId
                      && !me.IsDeleted);

            if (enrollment == null)
            {
                enrollment = new ModuleEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    ModuleId = moduleId,
                    ProgramEnrollmentId = programEnrollmentId,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = progressPercent,
                    EnrolledAt = seedTime.AddDays(-10),
                    StartedAt = seedTime.AddDays(-8),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };
                await _unitOfWork.ModuleEnrollments.AddAsync(enrollment);
                await _unitOfWork.SaveChangesAsync();
                return enrollment;
            }

            if (!enrollment.ProgramEnrollmentId.HasValue)
            {
                enrollment.ProgramEnrollmentId = programEnrollmentId;
                await _unitOfWork.ModuleEnrollments.Update(enrollment);
                await _unitOfWork.SaveChangesAsync();
            }

            return enrollment;
        }

        private async Task SeedResearchMilestoneDataAsync()
        {
            _loggerService.LogInformation("Starting seed research milestones");
            var existingRoboticsMilestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
                rm => rm.Code == "RML-ROBOTICS-03-01" && !rm.IsDeleted);
            if (existingRoboticsMilestone != null)
            {
                _loggerService.LogInformation("Robotics research milestones already exist, skipping robotics seeding");
                return;
            }

            var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
            if (moduleRobotics3 == null)
            {
                _loggerService.LogWarning("Module MOD-ROBOTICS-03 not found. Skipping research milestone seeding.");
                return;
            }

            var designBriefActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-04-01");
            var prototypeBuildActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-04-02");
            var finalPresentationActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-04-03");

            if (designBriefActivity == null || prototypeBuildActivity == null || finalPresentationActivity == null)
            {
                _loggerService.LogWarning(
                    "Research module activities not found. Skipping research milestone seeding.");
                return;
            }

            var seedTime = DateTime.UtcNow;
            var availabilityFrom = seedTime.AddDays(-30);
            var availabilityUntil = seedTime.AddDays(90);

            var assignmentDesign = new Assignment
            {
                Id = Guid.NewGuid(),
                Code = "ASG-ROBOTICS-03-01",
                ModuleId = moduleRobotics3.Id,
                Title = "Design Brief Submission",
                Description = "Submit your robot design document and component list.",
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 60m,
                IsRequiredForModulePass = true,
                DueDate = seedTime.AddDays(14),
                AvailableFrom = availabilityFrom,
                AvailableUntil = availabilityUntil,
                MaxAttempts = 3,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            var assignmentPrototype = new Assignment
            {
                Id = Guid.NewGuid(),
                Code = "ASG-ROBOTICS-03-02",
                ModuleId = moduleRobotics3.Id,
                Title = "Prototype Build Report",
                Description = "Upload photos, build notes, and a short test summary.",
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 60m,
                IsRequiredForModulePass = true,
                DueDate = seedTime.AddDays(28),
                AvailableFrom = availabilityFrom,
                AvailableUntil = availabilityUntil,
                MaxAttempts = 3,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            var assignmentCapstone = new Assignment
            {
                Id = Guid.NewGuid(),
                Code = "ASG-ROBOTICS-03-03",
                ModuleId = moduleRobotics3.Id,
                Title = "Capstone Presentation Deliverable",
                Description = "Submit your final presentation deck and demo video link.",
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 70m,
                IsRequiredForModulePass = true,
                DueDate = seedTime.AddDays(42),
                AvailableFrom = availabilityFrom,
                AvailableUntil = availabilityUntil,
                MaxAttempts = 2,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            var milestoneDesign = new ResearchMilestone
            {
                Id = Guid.NewGuid(),
                Code = "RML-ROBOTICS-03-01",
                ModuleId = moduleRobotics3.Id,
                Title = "Design & Planning",
                Description = "Plan the robot challenge approach and document design choices.",
                MilestoneOrder = 1,
                IsCapstone = false,
                AssignmentId = assignmentDesign.Id,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            var milestonePrototype = new ResearchMilestone
            {
                Id = Guid.NewGuid(),
                Code = "RML-ROBOTICS-03-02",
                ModuleId = moduleRobotics3.Id,
                Title = "Prototype Assembly",
                Description = "Build and test the first working prototype.",
                MilestoneOrder = 2,
                IsCapstone = false,
                AssignmentId = assignmentPrototype.Id,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            var milestoneCapstone = new ResearchMilestone
            {
                Id = Guid.NewGuid(),
                Code = "RML-ROBOTICS-03-03",
                ModuleId = moduleRobotics3.Id,
                Title = "Capstone Presentation",
                Description = "Present the final robot and reflect on engineering trade-offs.",
                MilestoneOrder = 3,
                IsCapstone = true,
                AssignmentId = assignmentCapstone.Id,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            await _unitOfWork.Assignments.AddRangeAsync(
                new List<Assignment> { assignmentDesign, assignmentPrototype, assignmentCapstone });
            await _unitOfWork.ResearchMilestones.AddRangeAsync(
                new List<ResearchMilestone> { milestoneDesign, milestonePrototype, milestoneCapstone });
            await _unitOfWork.SaveChangesAsync();

            var milestoneActivities = new List<ResearchMilestoneActivity>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ResearchMilestoneId = milestoneDesign.Id,
                    ActivityId = designBriefActivity.Id,
                    IsRequiredForSubmission = true,
                    DisplayOrder = 1,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ResearchMilestoneId = milestonePrototype.Id,
                    ActivityId = prototypeBuildActivity.Id,
                    IsRequiredForSubmission = true,
                    DisplayOrder = 1,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ResearchMilestoneId = milestoneCapstone.Id,
                    ActivityId = finalPresentationActivity.Id,
                    IsRequiredForSubmission = true,
                    DisplayOrder = 1,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                }
            };

            await _unitOfWork.ResearchMilestoneActivities.AddRangeAsync(milestoneActivities);
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Finished seed research milestones — 3 milestone(s) and 3 activity link(s) created.");
        }

        private async Task SeedResearchModuleEnrollmentsAsync()
        {
            _loggerService.LogInformation("Starting seed research module enrollments");
            var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
            if (moduleRobotics3 == null)
            {
                _loggerService.LogWarning("Module MOD-ROBOTICS-03 not found. Skipping research module enrollment seeding.");
                return;
            }

            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");
            ProgramEnrollment? programEnrollmentStudent1 = null;
            if (student1 != null)
            {
                programEnrollmentStudent1 = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student1.Id && !pe.IsDeleted);
            }
            var enrollTime = DateTime.UtcNow;
            var moduleEnrollments = new List<ModuleEnrollment>();

            async Task TryAddEnrollmentAsync(User? student, Guid? programEnrollmentId, decimal progressPercent)
            {
                if (student == null)
                {
                    return;
                }

                var exists = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    me => me.StudentId == student.Id
                          && me.ModuleId == moduleRobotics3.Id
                          && !me.IsDeleted);

                if (exists != null)
                {
                    return;
                }

                moduleEnrollments.Add(new ModuleEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    ModuleId = moduleRobotics3.Id,
                    ProgramEnrollmentId = programEnrollmentId,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = progressPercent,
                    EnrolledAt = enrollTime.AddDays(-7),
                    StartedAt = enrollTime.AddDays(-5),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            await TryAddEnrollmentAsync(student1, programEnrollmentStudent1?.Id, 55m);
            await TryAddEnrollmentAsync(student2, null, 20m);
            await TryAddEnrollmentAsync(student3, null, 10m);

            if (moduleEnrollments.Count == 0)
            {
                _loggerService.LogInformation("Research module enrollments already exist, skipping seeding");
                return;
            }

            await _unitOfWork.ModuleEnrollments.AddRangeAsync(moduleEnrollments);
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Finished seed research module enrollments — {Count} enrollment(s) created.",
                moduleEnrollments.Count);
        }

        private async Task SeedResearchActivityProgressAsync()
        {
            _loggerService.LogInformation("Starting seed research activity progress");
            var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
            if (moduleRobotics3 == null)
            {
                _loggerService.LogWarning("Module MOD-ROBOTICS-03 not found. Skipping research activity progress seeding.");
                return;
            }

            var designBriefActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-04-01");
            var prototypeBuildActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-ROBOTICS-04-02");
            if (designBriefActivity == null || prototypeBuildActivity == null)
            {
                _loggerService.LogWarning("Research activities not found. Skipping research activity progress seeding.");
                return;
            }

            var studentCodes = new[] { "STD-001", "STD-002", "STD-003" };
            var progressTime = DateTime.UtcNow;
            var activityProgresses = new List<ActivityProgress>();

            foreach (var studentCode in studentCodes)
            {
                var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode);
                if (student == null)
                {
                    continue;
                }

                var enrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    me => me.StudentId == student.Id
                          && me.ModuleId == moduleRobotics3.Id
                          && !me.IsDeleted);

                if (enrollment == null)
                {
                    continue;
                }

                var existingProgress = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
                    ap => ap.ModuleEnrollmentId == enrollment.Id
                          && ap.ActivityId == designBriefActivity.Id
                          && !ap.IsDeleted);

                if (existingProgress == null)
                {
                    activityProgresses.Add(new ActivityProgress
                    {
                        Id = Guid.NewGuid(),
                        StudentId = student.Id,
                        ActivityId = designBriefActivity.Id,
                        ModuleEnrollmentId = enrollment.Id,
                        ActivityStatus = ActivityStatus.Done,
                        IsCompleted = true,
                        CompletedAt = progressTime.AddDays(-4),
                        CreatedAt = progressTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false
                    });
                }

                if (studentCode == "STD-001")
                {
                    var existingPrototypeProgress = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
                        ap => ap.ModuleEnrollmentId == enrollment.Id
                              && ap.ActivityId == prototypeBuildActivity.Id
                              && !ap.IsDeleted);

                    if (existingPrototypeProgress == null)
                    {
                        activityProgresses.Add(new ActivityProgress
                        {
                            Id = Guid.NewGuid(),
                            StudentId = student.Id,
                            ActivityId = prototypeBuildActivity.Id,
                            ModuleEnrollmentId = enrollment.Id,
                            ActivityStatus = ActivityStatus.Done,
                            IsCompleted = true,
                            CompletedAt = progressTime.AddDays(-2),
                            CreatedAt = progressTime,
                            CreatedBy = Guid.Empty,
                            IsDeleted = false
                        });
                    }
                }
            }

            if (activityProgresses.Count == 0)
            {
                _loggerService.LogInformation("Research activity progress already exists, skipping seeding");
                return;
            }

            await _unitOfWork.ActivityProgresses.AddRangeAsync(activityProgresses);
            await _unitOfWork.SaveChangesAsync();

            foreach (var studentCode in studentCodes)
            {
                var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode);
                if (student == null)
                {
                    continue;
                }

                var enrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    me => me.StudentId == student.Id
                          && me.ModuleId == moduleRobotics3.Id
                          && !me.IsDeleted);

                if (enrollment == null)
                {
                    continue;
                }

                var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
                    _unitOfWork,
                    enrollment);

                if (moduleProgressPercent >= 100m && enrollment.ProgramEnrollmentId.HasValue)
                {
                    await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                        _unitOfWork,
                        enrollment.ProgramEnrollmentId.Value,
                        enrollment);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Finished seed research activity progress — {Count} record(s) created.",
                activityProgresses.Count);
        }

        private async Task SeedEnrollmentActivityProgressAsync()
        {
            _loggerService.LogInformation("Starting seed enrollment activity progress");
            var seedTime = DateTime.UtcNow;

            await TrySeedModuleActivityProgressAsync(
                "STD-001",
                "MOD-ROBOTICS-01",
                "PRG-ROBOTICS",
                [
                    ("ACT-ROBOTICS-01-01", ActivityStatus.Done, seedTime.AddDays(-6)),
                    ("ACT-ROBOTICS-01-02", ActivityStatus.Done, seedTime.AddDays(-4)),
                    ("ACT-ROBOTICS-01-03", ActivityStatus.InProgress, null),
                ]);

            await TrySeedModuleActivityProgressAsync(
                "STD-002",
                "MOD-WEBDEV-01",
                "PRG-WEBDEV",
                [
                    ("ACT-WEBDEV-01-01", ActivityStatus.Done, seedTime.AddDays(-3)),
                    ("ACT-WEBDEV-01-02", ActivityStatus.InProgress, null),
                ]);

            _loggerService.LogInformation("Finished seed enrollment activity progress");
        }

        private async Task TrySeedModuleActivityProgressAsync(
            string studentCode,
            string moduleCode,
            string programCode,
            (string ActivityCode, ActivityStatus Status, DateTime? CompletedAt)[] activitySeeds)
        {
            var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == studentCode);
            var module = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == moduleCode);
            if (student == null || module == null)
            {
                _loggerService.LogWarning(
                    "Student {StudentCode} or module {ModuleCode} not found. Skipping activity progress seeding.",
                    studentCode,
                    moduleCode);
                return;
            }

            var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == programCode);
            ProgramEnrollment? programEnrollment = null;
            if (program != null)
            {
                programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted);
            }

            var moduleEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student.Id
                      && me.ModuleId == module.Id
                      && me.Status == EnrollmentStatus.Active
                      && !me.IsDeleted);

            if (moduleEnrollment == null)
            {
                _loggerService.LogWarning(
                    "Active module enrollment not found for {StudentCode} / {ModuleCode}. Skipping activity progress seeding.",
                    studentCode,
                    moduleCode);
                return;
            }

            var enrollmentUpdated = false;
            if (programEnrollment != null && moduleEnrollment.ProgramEnrollmentId != programEnrollment.Id)
            {
                moduleEnrollment.ProgramEnrollmentId = programEnrollment.Id;
                enrollmentUpdated = true;
            }

            if (!moduleEnrollment.StartedAt.HasValue)
            {
                moduleEnrollment.StartedAt = DateTime.UtcNow.AddDays(-7);
                enrollmentUpdated = true;
            }

            if (enrollmentUpdated)
            {
                await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
            }

            var seedTime = DateTime.UtcNow;
            var progressChanged = enrollmentUpdated;

            foreach (var (activityCode, status, completedAt) in activitySeeds)
            {
                var activity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == activityCode);
                if (activity == null)
                {
                    _loggerService.LogWarning("Activity {ActivityCode} not found. Skipping.", activityCode);
                    continue;
                }

                var existingProgress = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
                    ap => ap.ModuleEnrollmentId == moduleEnrollment.Id
                          && ap.ActivityId == activity.Id
                          && !ap.IsDeleted);

                if (existingProgress != null)
                {
                    if (existingProgress.ActivityStatus == status
                        && existingProgress.IsCompleted == (status == ActivityStatus.Done))
                    {
                        continue;
                    }

                    existingProgress.ActivityStatus = status;
                    existingProgress.IsCompleted = status == ActivityStatus.Done;
                    existingProgress.CompletedAt = status == ActivityStatus.Done
                        ? completedAt ?? seedTime
                        : null;
                    await _unitOfWork.ActivityProgresses.Update(existingProgress);
                    progressChanged = true;
                    continue;
                }

                await _unitOfWork.ActivityProgresses.AddAsync(new ActivityProgress
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    ActivityId = activity.Id,
                    ModuleEnrollmentId = moduleEnrollment.Id,
                    ActivityStatus = status,
                    IsCompleted = status == ActivityStatus.Done,
                    CompletedAt = status == ActivityStatus.Done ? completedAt ?? seedTime : null,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
                progressChanged = true;
            }

            if (!progressChanged)
            {
                return;
            }

            await _unitOfWork.SaveChangesAsync();

            var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
                _unitOfWork,
                moduleEnrollment);

            if (moduleProgressPercent >= 100m && moduleEnrollment.ProgramEnrollmentId.HasValue)
            {
                await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                    _unitOfWork,
                    moduleEnrollment.ProgramEnrollmentId.Value,
                    moduleEnrollment);
            }

            await _unitOfWork.SaveChangesAsync();

            _loggerService.LogInformation(
                "Seeded activity progress for {StudentCode} / {ModuleCode} — module progress {ProgressPercent}%.",
                studentCode,
                moduleCode,
                moduleProgressPercent);
        }

        private async Task BackfillActivityProgressStatusAsync()
        {
            _loggerService.LogInformation("Starting backfill activity progress status");
            var progresses = await _unitOfWork.ActivityProgresses.GetAllAsync(ap => !ap.IsDeleted);
            var moduleEnrollmentIds = new HashSet<Guid>();
            var changed = false;

            foreach (var progress in progresses)
            {
                if (progress.IsCompleted && progress.ActivityStatus != ActivityStatus.Done)
                {
                    progress.ActivityStatus = ActivityStatus.Done;
                    progress.CompletedAt ??= DateTime.UtcNow;
                    await _unitOfWork.ActivityProgresses.Update(progress);
                    moduleEnrollmentIds.Add(progress.ModuleEnrollmentId);
                    changed = true;
                }
            }

            if (!changed)
            {
                _loggerService.LogInformation("No activity progress status backfill required");
                return;
            }

            await _unitOfWork.SaveChangesAsync();

            foreach (var moduleEnrollmentId in moduleEnrollmentIds)
            {
                var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId);
                if (moduleEnrollment == null || moduleEnrollment.IsDeleted)
                {
                    continue;
                }

                var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
                    _unitOfWork,
                    moduleEnrollment);

                if (moduleProgressPercent >= 100m && moduleEnrollment.ProgramEnrollmentId.HasValue)
                {
                    await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                        _unitOfWork,
                        moduleEnrollment.ProgramEnrollmentId.Value,
                        moduleEnrollment);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Finished backfill activity progress status — {Count} module enrollment(s) recalculated.",
                moduleEnrollmentIds.Count);
        }

        private async Task SeedResearchSubmissionsAsync()
        {
            _loggerService.LogInformation("Starting seed research submissions");

            var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
            var milestoneDesign = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
                rm => rm.Code == "RML-ROBOTICS-03-01" && !rm.IsDeleted);
            var milestonePrototype = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
                rm => rm.Code == "RML-ROBOTICS-03-02" && !rm.IsDeleted);

            if (mentor == null
                || student1 == null
                || student2 == null
                || moduleRobotics3 == null
                || milestoneDesign == null
                || milestonePrototype == null)
            {
                _loggerService.LogWarning("Required research submission seed data not found. Skipping.");
                return;
            }

            var assignmentDesign = await _unitOfWork.Assignments.GetByIdAsync(milestoneDesign.AssignmentId);
            var assignmentPrototype = await _unitOfWork.Assignments.GetByIdAsync(milestonePrototype.AssignmentId);
            if (assignmentDesign == null || assignmentPrototype == null)
            {
                _loggerService.LogWarning("Research milestone assignments not found. Skipping research submission seeding.");
                return;
            }

            var enrollmentStudent1 = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student1.Id
                      && me.ModuleId == moduleRobotics3.Id
                      && !me.IsDeleted);
            var enrollmentStudent2 = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student2.Id
                      && me.ModuleId == moduleRobotics3.Id
                      && !me.IsDeleted);

            if (enrollmentStudent1 == null || enrollmentStudent2 == null)
            {
                _loggerService.LogWarning("Research module enrollments not found. Skipping research submission seeding.");
                return;
            }

            var seedTime = DateTime.UtcNow;
            var submissions = new List<Submission>();

            if (!await SubmissionCodeExistsAsync("SUB-RML0301A"))
            {
                submissions.Add(new Submission
                {
                    Id = Guid.NewGuid(),
                    Code = "SUB-RML0301A",
                    AssignmentId = assignmentDesign.Id,
                    StudentId = student1.Id,
                    ModuleEnrollmentId = enrollmentStudent1.Id,
                    ResearchMilestoneId = milestoneDesign.Id,
                    AttemptNumber = 1,
                    Status = SubmissionStatus.Graded,
                    ContentText = "Our team chose a line-following chassis with ultrasonic obstacle detection.",
                    FileUrl = "https://storage.oboxsteam.com/submissions/robotics-design-brief-std001.pdf",
                    AssignedGrade = 85m,
                    MentorFeedback = "Strong design rationale. Consider adding a power budget table.",
                    VerifiedBy = mentor.Id,
                    SubmittedAt = seedTime.AddDays(-6),
                    GradedAt = seedTime.AddDays(-4),
                    CreatedAt = seedTime.AddDays(-8),
                    CreatedBy = mentor.Id,
                    IsDeleted = false
                });
            }

            if (!await SubmissionCodeExistsAsync("SUB-RML0302A"))
            {
                submissions.Add(new Submission
                {
                    Id = Guid.NewGuid(),
                    Code = "SUB-RML0302A",
                    AssignmentId = assignmentPrototype.Id,
                    StudentId = student1.Id,
                    ModuleEnrollmentId = enrollmentStudent1.Id,
                    ResearchMilestoneId = milestonePrototype.Id,
                    AttemptNumber = 0,
                    Status = SubmissionStatus.Pending,
                    CreatedAt = seedTime.AddDays(-1),
                    CreatedBy = mentor.Id,
                    IsDeleted = false
                });
            }

            if (!await SubmissionCodeExistsAsync("SUB-RML0301B"))
            {
                submissions.Add(new Submission
                {
                    Id = Guid.NewGuid(),
                    Code = "SUB-RML0301B",
                    AssignmentId = assignmentDesign.Id,
                    StudentId = student2.Id,
                    ModuleEnrollmentId = enrollmentStudent2.Id,
                    ResearchMilestoneId = milestoneDesign.Id,
                    AttemptNumber = 1,
                    Status = SubmissionStatus.ReturnedForRevision,
                    ContentText = "Initial design draft with motor placement notes.",
                    FileUrl = "https://storage.oboxsteam.com/submissions/robotics-design-brief-std002.pdf",
                    MentorFeedback = "Please add sensor placement diagrams and a parts list before resubmitting.",
                    SubmittedAt = seedTime.AddDays(-3),
                    CreatedAt = seedTime.AddDays(-5),
                    CreatedBy = mentor.Id,
                    UpdatedAt = seedTime.AddDays(-2),
                    UpdatedBy = mentor.Id,
                    IsDeleted = false
                });
            }

            if (submissions.Count == 0)
            {
                _loggerService.LogInformation("Research submissions already exist, skipping seeding");
                return;
            }

            await _unitOfWork.Submissions.AddRangeAsync(submissions);
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation(
                "Finished seed research submissions — {Count} submission(s) created.",
                submissions.Count);
        }

        private async Task<bool> SubmissionCodeExistsAsync(string code)
            => await _unitOfWork.Submissions.FirstOrDefaultAsync(s => s.Code == code && !s.IsDeleted) != null;

        private async Task SeedExtendedResearchDataAsync()
        {
            await SeedWebDevResearchMilestonesAsync();
            await SeedWebDevResearchEnrollmentsAsync();
            await SeedExtendedResearchSubmissionsAsync();
        }

        private async Task SeedWebDevResearchMilestonesAsync()
        {
            _loggerService.LogInformation("Starting seed webdev research milestones");
            var existingMilestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
                rm => rm.Code == "RML-WEBDEV-03-01" && !rm.IsDeleted);
            if (existingMilestone != null)
            {
                _loggerService.LogInformation("WebDev research milestones already exist, skipping");
                return;
            }

            var moduleWebDev3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-03");
            var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
            if (moduleWebDev3 == null || mentor == null)
            {
                _loggerService.LogWarning("MOD-WEBDEV-03 or mentor not found. Skipping webdev research milestones.");
                return;
            }

            var seedTime = DateTime.UtcNow;
            var courseWebDev3 = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == "CRS-WEBDEV-03");
            if (courseWebDev3 == null)
            {
                courseWebDev3 = new Course
                {
                    Id = Guid.NewGuid(),
                    Code = "CRS-WEBDEV-03",
                    ModuleId = moduleWebDev3.Id,
                    MentorId = mentor.Id,
                    Name = "Responsive Design & Deployment - Capstone Cohort",
                    Description = "Research cohort for responsive design and deployment capstone work.",
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                };
                await _unitOfWork.Courses.AddAsync(courseWebDev3);
                await _unitOfWork.SaveChangesAsync();

                var activities = new List<Activity>
                {
                    NewActivity("ACT-WEBDEV-03-01", "Responsive Design Brief", ActivityType.SelfPaced, 1,
                        "Review responsive design requirements and breakpoints.", null, null, null, null, false, false),
                    NewActivity("ACT-WEBDEV-03-02", "Deployment Workshop", ActivityType.LiveOnline, 2,
                        "Live session on hosting and deployment pipelines.",
                        "https://meet.google.com/webdev-deploy",
                        seedTime.AddDays(10).Date.AddHours(10),
                        seedTime.AddDays(10).Date.AddHours(12),
                        20, false, false),
                    NewActivity("ACT-WEBDEV-03-03", "Capstone Demo Day", ActivityType.LiveOnline, 3,
                        "Present deployed capstone sites to mentors.",
                        "https://meet.google.com/webdev-capstone",
                        seedTime.AddDays(28).Date.AddHours(14),
                        seedTime.AddDays(28).Date.AddHours(16),
                        30, false, true)
                };

                foreach (var activity in activities)
                {
                    activity.CourseId = courseWebDev3.Id;
                    activity.CreatedAt = seedTime;
                    activity.CreatedBy = Guid.Empty;
                    activity.IsDeleted = false;
                }

                await _unitOfWork.Activities.AddRangeAsync(activities);
                await _unitOfWork.SaveChangesAsync();
            }

            var wireframeActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-03-01");
            var deploymentActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-03-02");
            var capstoneActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-03-03");

            if (wireframeActivity == null || deploymentActivity == null || capstoneActivity == null)
            {
                _loggerService.LogWarning("WebDev research activities not found. Skipping milestones.");
                return;
            }

            var availabilityFrom = seedTime.AddDays(-30);
            var availabilityUntil = seedTime.AddDays(90);

            var assignmentWireframe = new Assignment
            {
                Id = Guid.NewGuid(),
                Code = "ASG-WEBDEV-03-01",
                ModuleId = moduleWebDev3.Id,
                Title = "Responsive Wireframe Package",
                Description = "Submit wireframes for mobile, tablet, and desktop breakpoints.",
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 60m,
                IsRequiredForModulePass = true,
                DueDate = seedTime.AddDays(14),
                AvailableFrom = availabilityFrom,
                AvailableUntil = availabilityUntil,
                MaxAttempts = 3,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            var assignmentCapstone = new Assignment
            {
                Id = Guid.NewGuid(),
                Code = "ASG-WEBDEV-03-02",
                ModuleId = moduleWebDev3.Id,
                Title = "Deployed Capstone Site",
                Description = "Submit the live URL and source archive for the capstone site.",
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 70m,
                IsRequiredForModulePass = true,
                DueDate = seedTime.AddDays(35),
                AvailableFrom = availabilityFrom,
                AvailableUntil = availabilityUntil,
                MaxAttempts = 2,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            var milestoneWireframe = new ResearchMilestone
            {
                Id = Guid.NewGuid(),
                Code = "RML-WEBDEV-03-01",
                ModuleId = moduleWebDev3.Id,
                Title = "Responsive Planning",
                Description = "Plan responsive layouts and document deployment approach.",
                MilestoneOrder = 1,
                IsCapstone = false,
                AssignmentId = assignmentWireframe.Id,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            var milestoneCapstone = new ResearchMilestone
            {
                Id = Guid.NewGuid(),
                Code = "RML-WEBDEV-03-02",
                ModuleId = moduleWebDev3.Id,
                Title = "Capstone Deployment",
                Description = "Ship and present the final responsive web project.",
                MilestoneOrder = 2,
                IsCapstone = true,
                AssignmentId = assignmentCapstone.Id,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            await _unitOfWork.Assignments.AddRangeAsync(
                new List<Assignment> { assignmentWireframe, assignmentCapstone });
            await _unitOfWork.ResearchMilestones.AddRangeAsync(
                new List<ResearchMilestone> { milestoneWireframe, milestoneCapstone });
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.ResearchMilestoneActivities.AddRangeAsync(new List<ResearchMilestoneActivity>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ResearchMilestoneId = milestoneWireframe.Id,
                    ActivityId = wireframeActivity.Id,
                    IsRequiredForSubmission = true,
                    DisplayOrder = 1,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ResearchMilestoneId = milestoneCapstone.Id,
                    ActivityId = deploymentActivity.Id,
                    IsRequiredForSubmission = true,
                    DisplayOrder = 1,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ResearchMilestoneId = milestoneCapstone.Id,
                    ActivityId = capstoneActivity.Id,
                    IsRequiredForSubmission = false,
                    DisplayOrder = 2,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                }
            });
            await _unitOfWork.SaveChangesAsync();

            _loggerService.LogInformation(
                "Finished seed webdev research milestones — 2 milestone(s) and 3 activity link(s) created.");
        }

        private async Task SeedWebDevResearchEnrollmentsAsync()
        {
            _loggerService.LogInformation("Starting seed webdev research enrollments");
            var moduleWebDev3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-03");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");

            if (moduleWebDev3 == null || student2 == null || programWebDev == null)
            {
                _loggerService.LogWarning("WebDev research enrollment prerequisites not found. Skipping.");
                return;
            }

            var existing = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student2.Id
                      && me.ModuleId == moduleWebDev3.Id
                      && !me.IsDeleted);

            if (existing != null)
            {
                _loggerService.LogInformation("WebDev research module enrollment already exists, skipping");
                return;
            }

            var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                pe => pe.StudentId == student2.Id
                      && pe.ProgramId == programWebDev.Id
                      && !pe.IsDeleted);

            var seedTime = DateTime.UtcNow;
            var moduleEnrollment = new ModuleEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student2.Id,
                ModuleId = moduleWebDev3.Id,
                ProgramEnrollmentId = programEnrollment?.Id,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 5m,
                EnrolledAt = seedTime.AddDays(-3),
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            };

            await _unitOfWork.ModuleEnrollments.AddAsync(moduleEnrollment);
            await _unitOfWork.SaveChangesAsync();

            var wireframeActivity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == "ACT-WEBDEV-03-01");
            if (wireframeActivity != null)
            {
                await _unitOfWork.ActivityProgresses.AddAsync(new ActivityProgress
                {
                    Id = Guid.NewGuid(),
                    StudentId = student2.Id,
                    ActivityId = wireframeActivity.Id,
                    ModuleEnrollmentId = moduleEnrollment.Id,
                    ActivityStatus = ActivityStatus.Done,
                    IsCompleted = true,
                    CompletedAt = seedTime.AddDays(-1),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
                await _unitOfWork.SaveChangesAsync();

                var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
                    _unitOfWork,
                    moduleEnrollment);

                if (moduleProgressPercent >= 100m && moduleEnrollment.ProgramEnrollmentId.HasValue)
                {
                    await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                        _unitOfWork,
                        moduleEnrollment.ProgramEnrollmentId.Value,
                        moduleEnrollment);
                }

                await _unitOfWork.SaveChangesAsync();
            }

            _loggerService.LogInformation("Finished seed webdev research enrollment for STD-002.");
        }

        private async Task SeedExtendedResearchSubmissionsAsync()
        {
            _loggerService.LogInformation("Starting seed extended research submissions");

            var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            if (mentor == null || student1 == null || student2 == null)
            {
                return;
            }

            var seedTime = DateTime.UtcNow;
            var createdCount = 0;

            var gradedSubmission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
                s => s.Code == "SUB-RML0301A" && !s.IsDeleted);
            if (gradedSubmission != null)
            {
                var existingEvidence = await _unitOfWork.SubmissionEvidences.FirstOrDefaultAsync(
                    se => se.SubmissionId == gradedSubmission.Id && !se.IsDeleted);

                if (existingEvidence == null)
                {
                    var media = new MediaAsset
                    {
                        Id = Guid.NewGuid(),
                        UploaderId = student1.Id,
                        FileUrl = "https://storage.oboxsteam.com/submissions/evidence/robotics-sensor-photo.jpg",
                        FileType = "image/jpeg",
                        UploadedAt = seedTime.AddDays(-6),
                        CreatedAt = seedTime,
                        CreatedBy = student1.Id,
                        IsDeleted = false
                    };
                    await _unitOfWork.MediaAssets.AddAsync(media);
                    await _unitOfWork.SubmissionEvidences.AddAsync(new SubmissionEvidence
                    {
                        SubmissionId = gradedSubmission.Id,
                        MediaId = media.Id,
                        CreatedAt = seedTime,
                        CreatedBy = student1.Id,
                        IsDeleted = false
                    });
                    await _unitOfWork.SaveChangesAsync();
                    createdCount++;
                }
            }

            var moduleRobotics3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-03");
            var milestoneCapstone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
                rm => rm.Code == "RML-ROBOTICS-03-03" && !rm.IsDeleted);
            var enrollmentStudent1 = moduleRobotics3 == null
                ? null
                : await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    me => me.StudentId == student1.Id
                          && me.ModuleId == moduleRobotics3.Id
                          && !me.IsDeleted);

            if (milestoneCapstone != null
                && enrollmentStudent1 != null
                && !await SubmissionCodeExistsAsync("SUB-RML0303A"))
            {
                var assignmentCapstone = await _unitOfWork.Assignments.GetByIdAsync(milestoneCapstone.AssignmentId);
                if (assignmentCapstone != null)
                {
                    await _unitOfWork.Submissions.AddAsync(new Submission
                    {
                        Id = Guid.NewGuid(),
                        Code = "SUB-RML0303A",
                        AssignmentId = assignmentCapstone.Id,
                        StudentId = student1.Id,
                        ModuleEnrollmentId = enrollmentStudent1.Id,
                        ResearchMilestoneId = milestoneCapstone.Id,
                        AttemptNumber = 0,
                        Status = SubmissionStatus.Pending,
                        CreatedAt = seedTime,
                        CreatedBy = mentor.Id,
                        IsDeleted = false
                    });
                    await _unitOfWork.SaveChangesAsync();
                    createdCount++;
                }
            }

            var moduleWebDev3 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-03");
            var milestoneWebDev1 = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
                rm => rm.Code == "RML-WEBDEV-03-01" && !rm.IsDeleted);
            var enrollmentStudent2WebDev = moduleWebDev3 == null
                ? null
                : await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    me => me.StudentId == student2.Id
                          && me.ModuleId == moduleWebDev3.Id
                          && !me.IsDeleted);

            if (milestoneWebDev1 != null
                && enrollmentStudent2WebDev != null
                && !await SubmissionCodeExistsAsync("SUB-WDV0301B"))
            {
                var assignmentWebDev = await _unitOfWork.Assignments.GetByIdAsync(milestoneWebDev1.AssignmentId);
                if (assignmentWebDev != null)
                {
                    await _unitOfWork.Submissions.AddAsync(new Submission
                    {
                        Id = Guid.NewGuid(),
                        Code = "SUB-WDV0301B",
                        AssignmentId = assignmentWebDev.Id,
                        StudentId = student2.Id,
                        ModuleEnrollmentId = enrollmentStudent2WebDev.Id,
                        ResearchMilestoneId = milestoneWebDev1.Id,
                        AttemptNumber = 1,
                        Status = SubmissionStatus.TurnedIn,
                        ContentText = "Wireframes for landing page across mobile and desktop breakpoints.",
                        FileUrl = "https://storage.oboxsteam.com/submissions/webdev-wireframes-std002.pdf",
                        SubmittedAt = seedTime.AddDays(-2),
                        CreatedAt = seedTime.AddDays(-4),
                        CreatedBy = mentor.Id,
                        IsDeleted = false
                    });
                    await _unitOfWork.SaveChangesAsync();
                    createdCount++;
                }
            }

            _loggerService.LogInformation(
                "Finished seed extended research submissions — {Count} update(s)/record(s).",
                createdCount);
        }

        public async Task ClearAllDataAsync()
        {
            _loggerService.LogInformation("Starting clear all data");

            await ClearS3ObjectsAsync();

            // ── Phase 1: True leaf tables (no children) ──────────────────────────
            await _unitOfWork.MediaTags.HardRemove(x => true);               // join table
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.QuizAnswers.HardRemove(x => true);             // → Submission, QuizQuestion, QuizOption
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.SubmissionEvidences.HardRemove(x => true);     // → Submission, MediaAsset
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.PortfolioItemSubmissions.HardRemove(x => true); // → PortfolioCustomItem, Submission
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.SessionAttendances.HardRemove(x => true);      // → ClassSession, ModuleEnrollment
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.ActivityProgresses.HardRemove(x => true);      // → ModuleEnrollment, Activity
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.ResearchMilestoneActivities.HardRemove(x => true); // → ResearchMilestone
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.BankQuestionOptions.HardRemove(x => true);     // → BankQuestion
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.QuizOptions.HardRemove(x => true);             // → QuizQuestion
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.QuizQuestions.HardRemove(x => true);           // → Submission (SetNull)
            await _unitOfWork.SaveChangesAsync();

            // ── Phase 2: Mid-leaf tables ──────────────────────────────────────────
            await _unitOfWork.Submissions.HardRemove(x => true);             // → ModuleEnrollment (Restrict)
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.PortfolioCustomItems.HardRemove(x => true);    // → ProgramEnrollment, ModuleEnrollment
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.ActivityBookings.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.ClassEnrollments.HardRemove(x => true);        // → ProgramEnrollment (Restrict)
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.PaymentRequests.HardRemove(x => true);         // → ProgramEnrollment (Restrict)
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Invoices.HardRemove(x => true);                // → Payment (Restrict)
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.ProgramReviews.HardRemove(x => true);          // → Program
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Certificates.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.HighlightVideos.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.FaceEmbeddings.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.MediaAssets.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.StudentSkills.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.StandardizedTests.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.OtpStorages.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.ProgramBoards.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Portfolios.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Payments.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();

            // ── Phase 3: Enrollments & content links ──────────────────────────────
            await _unitOfWork.ModuleEnrollments.HardRemove(x => true);       // → ProgramEnrollment (Restrict)
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CourseEnrollments.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.ProgramEnrollments.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.BankQuestions.HardRemove(x => true);           // → QuestionBank
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.QuestionBanks.HardRemove(x => true);           // → Course
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.ResearchMilestones.HardRemove(x => true);      // → Module, Assignment
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.ClassSessions.HardRemove(x => true);           // → Class, Activity, Assignment
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Materials.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();

            // ── Phase 4: Core LMS entities ────────────────────────────────────────
            await _unitOfWork.Assignments.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Activities.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Classes.HardRemove(x => true);                 // → Program (Restrict)
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Courses.HardRemove(x => true);                 // → Module (implicit)
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Modules.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Programs.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();

            // ── Phase 5: Users ────────────────────────────────────────────────────
            await _unitOfWork.ParentStudents.HardRemove(x => true);          // → User (Restrict) × 2
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.StudentProfiles.HardRemove(x => true);         // → User (Cascade)
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Experts.HardRemove(x => true);                 // → User (SetNull)
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.Users.HardRemove(x => true);
            await _unitOfWork.SaveChangesAsync();

            _loggerService.LogInformation("Finished clear all data");
        }


        /// <summary>
        /// Xóa toàn bộ objects trong S3 bucket trước khi xóa DB rows.
        /// Lỗi xóa từng object được log warning nhưng không làm dừng quá trình.
        /// </summary>
        private async Task ClearS3ObjectsAsync()
        {
            _loggerService.LogInformation("[ClearS3] Starting full bucket cleanup...");

            var (deleted, failed) = await _blobService.ClearAllObjectsAsync();

            _loggerService.LogInformation(
                "[ClearS3] S3 cleanup done. Deleted={Deleted}, Failed={Failed}",
                deleted, failed);
        }

        /// <summary>
        /// Trích xuất S3 key từ URL (path-style hoặc virtual-hosted style).
        /// Trả về null nếu URL không hợp lệ.
        /// </summary>
        private string? ExtractS3Key(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            var path = uri.AbsolutePath.TrimStart('/');

            // Path-style: /{bucket}/{key}
            var bucketPrefix = $"{_blobService.BucketName}/";
            if (path.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
                path = path[bucketPrefix.Length..];

            return string.IsNullOrWhiteSpace(path) ? null : path;
        }

        /// <summary>
        /// Kiểm tra URL có thuộc S3 bucket của project hay không.
        /// Chỉ xóa những URL chứa hostname amazonaws.com hoặc bucket name của project.
        /// </summary>
        private bool IsS3Url(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            var host = uri.Host;
            return host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase)
                || host.Contains(_blobService.BucketName, StringComparison.OrdinalIgnoreCase);
        }

        private sealed record SeedMaterialDefinition(
            string ActivityCode,
            string Title,
            MaterialType MaterialType,
            string FileUrl,
            long? FileSizeBytes = null);

        private static IReadOnlyList<SeedMaterialDefinition> GetSeedMaterialDefinitions() =>
        [
            new("ACT-ROBOTICS-01-01", "Pre-class Reading: What is a Robot?", MaterialType.Video,
                "https://storage.oboxsteam.com/materials/video/what-is-a-robot.mp4", 48_500_000L),
            new("ACT-ROBOTICS-01-04", "Reflection Journal Template", MaterialType.PDF,
                "https://storage.oboxsteam.com/materials/pdf/robotics-reflection-journal.pdf", 245_000L),
            new("ACT-ROBOTICS-02-02", "Block Programming Exercise Pack", MaterialType.PDF,
                "https://storage.oboxsteam.com/materials/pdf/block-programming-exercises.pdf", 1_120_000L),
            new("ACT-ROBOTICS-02-03", "Weekly Quiz Review Videos", MaterialType.Video,
                "https://storage.oboxsteam.com/materials/video/robotics-quiz-review.mp4", 62_000_000L),
            new("ACT-ROBOTICS-03-01", "Sensor Theory Overview", MaterialType.Video,
                "https://storage.oboxsteam.com/materials/video/sensor-theory-overview.mp4", 55_800_000L),
            new("ACT-ROBOTICS-04-01", "Build and Test Design Brief", MaterialType.PDF,
                "https://storage.oboxsteam.com/materials/pdf/robotics-design-brief.pdf", 890_000L),
            new("ACT-WEBDEV-01-01", "HTML Structure Overview", MaterialType.Video,
                "https://storage.oboxsteam.com/materials/video/html-structure-overview.mp4", 41_200_000L),
            new("ACT-WEBDEV-01-03", "Responsive Layout Exercise Workbook", MaterialType.PDF,
                "https://storage.oboxsteam.com/materials/pdf/responsive-layout-workbook.pdf", 760_000L),
            new("ACT-WEBDEV-02-01", "JavaScript Variables and Types", MaterialType.Video,
                "https://storage.oboxsteam.com/materials/video/javascript-variables-types.mp4", 38_400_000L),
            new("ACT-WEBDEV-02-04", "Code Review Checklist", MaterialType.DOC,
                "https://storage.oboxsteam.com/materials/doc/webdev-code-review-checklist.docx", 128_000L),
            new("ACT-WEBDEV-03-01", "Responsive Design Brief", MaterialType.PDF,
                "https://storage.oboxsteam.com/materials/pdf/responsive-design-brief.pdf", 512_000L),
            new("ACT-STEAM-01-02", "Science Experiment Kit Guide", MaterialType.PDF,
                "https://storage.oboxsteam.com/materials/pdf/steam-science-kit-guide.pdf", 680_000L),
            new("ACT-STEAM-02-01", "Prototyping Principles", MaterialType.Video,
                "https://storage.oboxsteam.com/materials/video/prototyping-principles.mp4", 44_600_000L),
            new("ACT-STEAM-02-03", "Design Critique Worksheet", MaterialType.PDF,
                "https://storage.oboxsteam.com/materials/pdf/design-critique-worksheet.pdf", 198_000L),
            new("ACT-STEAM-02-04", "Portfolio Documentation Template", MaterialType.DOC,
                "https://storage.oboxsteam.com/materials/doc/portfolio-documentation-template.docx", 156_000L),
            new("ACT-IOT-01-01", "Microcontroller Basics", MaterialType.Video,
                "https://storage.oboxsteam.com/materials/video/microcontroller-basics.mp4", 52_300_000L),
            new("ACT-IOT-01-02", "Sensor Wiring Guide", MaterialType.Image,
                "https://storage.oboxsteam.com/materials/image/sensor-wiring-diagram.png", 2_400_000L),
            new("ACT-IOT-02-01", "MQTT Concepts Explained", MaterialType.Video,
                "https://storage.oboxsteam.com/materials/video/mqtt-concepts.mp4", 36_700_000L),
            new("ACT-IOT-02-02", "Cloud Dashboard Setup Guide", MaterialType.PDF,
                "https://storage.oboxsteam.com/materials/pdf/cloud-dashboard-setup.pdf", 945_000L),
        ];

        private async Task SeedMaterialsAsync()
        {
            _loggerService.LogInformation("Starting seed materials");

            var definitions = GetSeedMaterialDefinitions();
            var definitionByCode = definitions.ToDictionary(
                d => d.ActivityCode,
                d => d,
                StringComparer.OrdinalIgnoreCase);

            var existingMaterials = await _unitOfWork.Materials.GetAllAsync(m => !m.IsDeleted);
            var activityIdsWithMaterial = existingMaterials
                .Select(m => m.ActivityId)
                .ToHashSet();

            var selfPacedActivities = await _unitOfWork.Activities.GetAllAsync(
                a => !a.IsDeleted && a.ActivityType == ActivityType.SelfPaced);

            var seedTime = DateTime.UtcNow;
            var materialsToAdd = new List<Material>();

            foreach (var activity in selfPacedActivities)
            {
                if (activityIdsWithMaterial.Contains(activity.Id))
                {
                    continue;
                }

                if (!definitionByCode.TryGetValue(activity.Code, out var definition))
                {
                    _loggerService.LogWarning(
                        "No seed material definition for SelfPaced activity '{ActivityCode}'. Skipping.",
                        activity.Code);
                    continue;
                }

                materialsToAdd.Add(new Material
                {
                    Id = Guid.NewGuid(),
                    ActivityId = activity.Id,
                    Title = definition.Title,
                    MaterialType = definition.MaterialType,
                    FileUrl = definition.FileUrl,
                    FileSizeBytes = definition.FileSizeBytes,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (materialsToAdd.Count == 0)
            {
                _loggerService.LogInformation("No new materials to seed.");
                return;
            }

            await _unitOfWork.Materials.AddRangeAsync(materialsToAdd);
            await _unitOfWork.SaveChangesAsync();

            _loggerService.LogInformation(
                "Finished seed materials — {Count} material(s) created.",
                materialsToAdd.Count);
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
                NewActivity("ACT-ROBOTICS-01-03", "Chassis Design Workshop", ActivityType.LiveOnline, 3,
                    "Live online workshop for robot chassis design and planning.",
                    "https://meet.google.com/robotics-chassis-workshop",
                    baseDate.AddDays(3).AddHours(14), baseDate.AddDays(3).AddHours(16), 30, false, false),
                NewActivity("ACT-ROBOTICS-01-04", "Reflection Journal", ActivityType.SelfPaced, 4,
                    "Submit a short reflection on what you learned this week.", null, null, null, null, false, false),
            });

            AddActivities("CRS-ROBOTICS-02", new[]
            {
                NewActivity("ACT-ROBOTICS-02-01", "Cohort B Kickoff", ActivityType.LiveOnline, 1,
                    "Welcome session for cohort B.",
                    "https://meet.google.com/robotics-cohort-b",
                    baseDate.AddDays(2).AddHours(10), baseDate.AddDays(2).AddHours(11), 25, false, false),
                NewActivity("ACT-ROBOTICS-02-02", "Block Programming Exercises", ActivityType.SelfPaced, 2,
                    "Self-paced block programming practice exercises.", null, null, null, null, false, false),
                NewActivity("ACT-ROBOTICS-02-03", "Weekly Quiz Review", ActivityType.SelfPaced, 3,
                    "Review quiz answers and supplementary videos.", null, null, null, null, false, false),
            });

            AddActivities("CRS-ROBOTICS-03", new[]
            {
                NewActivity("ACT-ROBOTICS-03-01", "Sensor Theory", ActivityType.SelfPaced, 1,
                    "Learn how ultrasonic and infrared sensors work.", null, null, null, null, false, false),
                NewActivity("ACT-ROBOTICS-03-02", "Movement Patterns Workshop", ActivityType.Offline, 2,
                    "Hands-on workshop on programming movement patterns.",
                    "Lab Room 103",
                    baseDate.AddDays(7).AddHours(9), baseDate.AddDays(7).AddHours(11), 30, true, false),
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
                NewActivity("ACT-WEBDEV-01-03", "Responsive Layout Exercises", ActivityType.SelfPaced, 3,
                    "Self-paced responsive layout practice exercises.", null, null, null, null, false, false),
            });

            AddActivities("CRS-WEBDEV-02", new[]
            {
                NewActivity("ACT-WEBDEV-02-01", "JavaScript Variables & Types", ActivityType.SelfPaced, 1,
                    "Self-paced module on JS fundamentals.", null, null, null, null, false, false),
                NewActivity("ACT-WEBDEV-02-02", "DOM Manipulation Lab", ActivityType.Offline, 2,
                    "Hands-on lab for DOM manipulation exercises.",
                    "Computer Lab 202",
                    baseDate.AddDays(6).AddHours(10), baseDate.AddDays(6).AddHours(12), 30, true, false),
                NewActivity("ACT-WEBDEV-02-03", "Weekend Hackathon", ActivityType.Offline, 3,
                    "Build a simple interactive page in teams.",
                    "Computer Lab 202",
                    baseDate.AddDays(12).AddHours(9), baseDate.AddDays(12).AddHours(15), 24, true, true),
                NewActivity("ACT-WEBDEV-02-04", "Code Review Checklist", ActivityType.SelfPaced, 4,
                    "Self-paced code review checklist and mentor feedback guide.", null, null, null, null, false, false),
            });

            AddActivities("CRS-STEAM-01", new[]
            {
                NewActivity("ACT-STEAM-01-01", "STEAM Lab Orientation", ActivityType.LiveOnline, 1,
                    "Orientation to interdisciplinary STEAM projects.",
                    "https://meet.google.com/steam-kickoff",
                    baseDate.AddDays(3).AddHours(9), baseDate.AddDays(3).AddHours(10), 40, false, false),
                NewActivity("ACT-STEAM-01-02", "Science Experiment Kit", ActivityType.SelfPaced, 2,
                    "Complete the at-home science experiment kit.", null, null, null, null, false, true),
                NewActivity("ACT-STEAM-01-03", "Art & Engineering Discussion", ActivityType.LiveOnline, 3,
                    "Live discussion on combining art and engineering in projects.",
                    "https://meet.google.com/steam-art-engineering",
                    baseDate.AddDays(9).AddHours(13), baseDate.AddDays(9).AddHours(16), 16, false, true),
            });

            AddActivities("CRS-STEAM-02", new[]
            {
                NewActivity("ACT-STEAM-02-01", "Prototyping Principles", ActivityType.SelfPaced, 1,
                    "Introduction to rapid prototyping methods.", null, null, null, null, false, false),
                NewActivity("ACT-STEAM-02-02", "Material Exploration Lab", ActivityType.Offline, 2,
                    "Explore recycled materials and simple circuits.",
                    "STEAM Studio 2",
                    baseDate.AddDays(11).AddHours(10), baseDate.AddDays(11).AddHours(13), 14, true, true),
                NewActivity("ACT-STEAM-02-03", "Design Critique Worksheet", ActivityType.SelfPaced, 3,
                    "Complete the peer design critique worksheet.", null, null, null, null, false, false),
                NewActivity("ACT-STEAM-02-04", "Portfolio Documentation", ActivityType.SelfPaced, 4,
                    "Document your prototype with photos and a short write-up.", null, null, null, null, false, true),
            });

            AddActivities("CRS-IOT-01", new[]
            {
                NewActivity("ACT-IOT-01-01", "Microcontroller Basics", ActivityType.SelfPaced, 1,
                    "Self-paced intro to Arduino and GPIO pins.", null, null, null, null, false, false),
                NewActivity("ACT-IOT-01-02", "Sensor Wiring Guide", ActivityType.SelfPaced, 2,
                    "Self-paced guide for wiring temperature and humidity sensors.", null, null, null, null, false, false),
                NewActivity("ACT-IOT-01-03", "Live Q&A: Sensor Data", ActivityType.LiveOnline, 3,
                    "Live Q&A on reading and interpreting sensor data.",
                    "https://meet.google.com/iot-sensors",
                    baseDate.AddDays(8).AddHours(14), baseDate.AddDays(8).AddHours(15), 25, false, false),
            });

            AddActivities("CRS-IOT-02", new[]
            {
                NewActivity("ACT-IOT-02-01", "MQTT Concepts", ActivityType.SelfPaced, 1,
                    "Learn MQTT publish/subscribe patterns.", null, null, null, null, false, false),
                NewActivity("ACT-IOT-02-02", "Cloud Dashboard Setup Guide", ActivityType.SelfPaced, 2,
                    "Self-paced guide for setting up a cloud dashboard.", null, null, null, null, false, false),
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