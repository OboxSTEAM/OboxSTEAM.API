using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MentorDTO;

namespace OboxSteam.Application.Interfaces;

public interface IMentorService
{
    Task<List<MentorSkillDto>> GetMySkillsAsync();

    Task<MentorSkillDto> AddMySkillAsync(CreateMentorSkillRequestDto request);

    Task<MentorSkillDto> UpdateMySkillAsync(Guid mentorSkillId, UpdateMentorSkillRequestDto request);

    Task<MentorSkillDto> SetMySkillVisibilityAsync(
        Guid mentorSkillId,
        UpdateMentorSkillVisibilityRequestDto request);

    Task RemoveMySkillAsync(Guid mentorSkillId);

    Task<Pagination<MentorProfileDto>> GetMentorsAsync(
        string? search,
        int page,
        int pageSize);

    Task<MentorProfileDto> GetMentorProfileAsync(Guid mentorId);

    Task<MentorProfileDto> GetMyProfileAsync();

    Task<MentorProfileDto> UpdateMyProfileAsync(UpdateMentorProfileRequestDto request);

    Task<MentorProfileDto> SetClassLimitAsync(Guid mentorId, UpdateMentorClassLimitRequestDto request);
}
