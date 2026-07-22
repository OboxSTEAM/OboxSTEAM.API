using OboxSteam.Application.DTOs.MentorDTO;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Commons;

public static class MentorSummaryMapper
{
    public static MentorSummaryDto ToSummary(User mentor, MentorProfile? profile)
        => new()
        {
            Id = mentor.Id,
            FullName = mentor.FullName,
            AvatarUrl = mentor.AvatarUrl,
            Title = profile?.Title,
            Organization = profile?.Organization,
            Bio = profile?.Bio,
            Achievements = profile?.Achievements,
            LinkedInUrl = profile?.LinkedInUrl,
        };
}
