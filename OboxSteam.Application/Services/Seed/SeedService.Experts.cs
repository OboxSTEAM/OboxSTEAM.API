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
    private async Task SeedExpertsAsync()
    {
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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
                    CreatedAt = _seedNow,
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

    }
}

