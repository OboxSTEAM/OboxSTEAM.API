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
    private static readonly (string ExpertCode, string UserCode, string Email, string FullName, string? Title, string? Organization, string? Bio, string? AvatarUrl, string? Achievements)[] SeedExpertAccounts =
    [
        ("EXP-001", "EXP-U001", "expert@oboxsteam.com", "Dr. Linh Tran",
            "Senior Robotics Mentor", "OboxSTEAM",
            "Robotics mentor with a focus on hands-on learning and STEM outreach.",
            "https://images.unsplash.com/photo-1758685848006-1bc450061624?q=80&w=1332&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            "National STEM Educator Award"),
        ("EXP-002", "EXP-U002", "expert2@oboxsteam.com", "Prof. Minh Hoang",
            "Visiting STEAM Advisor", "STEAM Research Lab",
            "Advisor for STEAM curriculum design and experiential learning.",
            "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?q=80&w=687&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            "Published 20+ STEAM research papers"),
        ("EXP-003", "EXP-U003", "expert3@oboxsteam.com", "Dr. Anh Pham",
            "Web Development Specialist", "Tech Academy Vietnam",
            "Full-stack developer teaching modern web technologies to young learners.",
            "https://images.unsplash.com/photo-1555436169-20e93ea9a7ff?q=80&w=1170&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            "10+ years industry experience"),
        ("EXP-004", "EXP-U004", "expert4@oboxsteam.com", "Dr. Mai Nguyen",
            "AI & Machine Learning Specialist", "Vietnam AI Institute",
            "Researcher and educator specializing in introductory AI and data science for students.",
            "https://placeholder.local/avatars/exp-004",
            "Led 5 national AI education initiatives"),
        ("EXP-005", "EXP-U005", "expert5@oboxsteam.com", "Prof. Hoa Le",
            "Mathematics Educator", "National University of Education",
            "Passionate about making mathematics engaging through puzzles and real-world applications.",
            "https://images.unsplash.com/photo-1561346745-5db62ae43861?q=80&w=1283&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            "Author of 3 popular math textbooks"),
        ("EXP-006", "EXP-U006", "expert6@oboxsteam.com", "Ms. Thao Vu",
            "Digital Arts Director", "Creative Minds Studio",
            "Professional illustrator mentoring students in digital art, design, and creative expression.",
            "https://plus.unsplash.com/premium_photo-1658506656752-4f1b1c1d5916?q=80&w=687&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            "Award-winning digital artist"),
        ("EXP-007", "EXP-U007", "expert7@oboxsteam.com", "Dr. Khoa Bui",
            "Environmental Scientist", "Green Earth Foundation",
            "Environmental researcher focused on sustainability education and climate science outreach.",
            "https://images.unsplash.com/photo-1581368129682-e2d66324045b?q=80&w=687&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D",
            "UN Youth Climate Ambassador 2023")
    ];

    private static List<User> CreateSeedExpertUsers(DateTime seedNow)
    {
        var users = new List<User>();
        var phoneBase = 0123456691;
        for (var i = 0; i < SeedExpertAccounts.Length; i++)
        {
            var account = SeedExpertAccounts[i];
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Code = account.UserCode,
                Email = account.Email,
                PasswordHash = new PasswordHasher().HashPassword("Expert@123")!,
                FullName = account.FullName,
                Phone = (phoneBase + i).ToString(),
                Role = RoleType.Expert,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
        }

        return users;
    }

    private async Task EnsureExpertUsersAsync()
    {
        var expertUsers = CreateSeedExpertUsers(_seedNow);
        var usersToAdd = new List<User>();
        foreach (var user in expertUsers)
        {
            var exists = await _unitOfWork.Users.FirstOrDefaultAsync(
                u => u.Code == user.Code || u.Email == user.Email);
            if (exists == null)
                usersToAdd.Add(user);
        }

        if (usersToAdd.Count == 0)
            return;

        await _unitOfWork.Users.AddRangeAsync(usersToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Backfilled {Count} expert login user(s).", usersToAdd.Count);
    }

    private async Task SeedExpertsAsync()
    {
        _loggerService.LogInformation("Starting seed experts");
        var existingExperts = await _unitOfWork.Experts.GetAllAsync();

        if (!existingExperts.Any())
        {
            var experts = new List<Expert>();
            foreach (var account in SeedExpertAccounts)
            {
                var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == account.Email);
                experts.Add(new Expert
                {
                    Id = Guid.NewGuid(),
                    Code = account.ExpertCode,
                    UserId = user?.Id,
                    FullName = account.FullName,
                    Title = account.Title,
                    Organization = account.Organization,
                    Bio = account.Bio,
                    AvatarUrl = account.AvatarUrl,
                    LinkedInUrl = "https://www.linkedin.com/company/anthropicresearch",
                    Achievements = account.Achievements,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            await _unitOfWork.Experts.AddRangeAsync(experts);
            await _unitOfWork.SaveChangesAsync();
            _loggerService.LogInformation("Finished seed experts");
        }
        else
        {
            _loggerService.LogInformation("Experts already exist, aligning expert logins");
            await AlignSeedExpertLoginsAsync(existingExperts);
        }
    }

    /// <summary>
    /// Detaches seed experts from mentor accounts and links each to a dedicated Expert-role user.
    /// </summary>
    private async Task AlignSeedExpertLoginsAsync(List<Expert> existingExperts)
    {
        var changed = false;
        foreach (var account in SeedExpertAccounts)
        {
            var expert = existingExperts.FirstOrDefault(e => e.Code == account.ExpertCode && !e.IsDeleted);
            if (expert == null)
                continue;

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == account.Email);
            if (user == null || user.Role != RoleType.Expert)
                continue;

            if (expert.UserId == user.Id)
                continue;

            expert.UserId = user.Id;
            await _unitOfWork.Experts.Update(expert);
            changed = true;
        }

        if (!changed)
            return;

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Aligned seed expert profiles to Expert-role logins");
    }
}
