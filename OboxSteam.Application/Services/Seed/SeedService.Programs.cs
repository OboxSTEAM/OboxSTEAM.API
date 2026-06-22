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
    private async Task SeedProgramsAsync()
    {
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

    }
}

