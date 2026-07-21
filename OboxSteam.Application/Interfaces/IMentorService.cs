using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MentorDTO;

namespace OboxSteam.Application.Interfaces;

public interface IMentorService
{
    Task<List<MentorSkillDto>> GetMySkillsAsync();

    Task<MentorSkillDto> AddMySkillAsync(CreateMentorSkillRequestDto request);

    Task RemoveMySkillAsync(Guid mentorSkillId);

    Task<Pagination<MentorProfileDto>> GetMentorsAsync(
        string? search,
        int page,
        int pageSize);

    Task<MentorProfileDto> GetMentorProfileAsync(Guid mentorId);

    Task<MentorProfileDto> SetClassLimitAsync(Guid mentorId, UpdateMentorClassLimitRequestDto request);
}
