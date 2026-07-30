using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.MentorDTO;
using OboxSteam.Application.DTOs.SkillDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class MentorService : IMentorService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<MentorService> _logger;

    public MentorService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<MentorService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<List<MentorSkillDto>> GetMySkillsAsync()
    {
        var mentorId = await GetCurrentMentorIdAsync();
        return await LoadSkillDtosAsync(mentorId, publicOnly: false);
    }

    public async Task<MentorSkillDto> AddMySkillAsync(CreateMentorSkillRequestDto request)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var mentor = await _unitOfWork.Users.GetByIdAsync(mentorId);
        MentorSkillValidator.ValidateMentorUser(mentor, mentorId);

        var skill = await _unitOfWork.Skills.GetByIdAsync(request.SkillId);
        MentorSkillValidator.ValidateSkillExists(skill, request.SkillId);

        var existing = await _unitOfWork.MentorSkills.FirstOrDefaultAsync(
            ms => ms.MentorId == mentorId && ms.SkillId == request.SkillId && !ms.IsDeleted);
        MentorSkillValidator.ValidateNoDuplicate(existing);

        var utcNow = DateTime.UtcNow;
        MentorSkillValidator.ValidateYearsOfExperience(request.YearsOfExperience);
        MentorSkillValidator.ValidateDescription(request.Description);
        MentorSkillValidator.ValidateEvidenceList(request.Evidences, utcNow);

        var entity = new MentorSkill
        {
            Id = Guid.NewGuid(),
            MentorId = mentorId,
            SkillId = request.SkillId,
            ProficiencyLevel = request.ProficiencyLevel,
            YearsOfExperience = request.YearsOfExperience,
            Description = NormalizeOptionalText(request.Description),
            Notes = NormalizeOptionalText(request.Notes),
            IsPublic = request.IsPublic,
        };

        await _unitOfWork.MentorSkills.AddAsync(entity);

        if (request.Evidences is { Count: > 0 })
        {
            await AddEvidenceRowsAsync(entity.Id, request.Evidences);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[AddMySkillAsync] Mentor {MentorId} added skill {SkillId}.",
            mentorId,
            request.SkillId);

        var evidences = await LoadEvidencesByMentorSkillIdsAsync(new[] { entity.Id });
        return MapMentorSkill(entity, skill!, evidences.GetValueOrDefault(entity.Id));
    }

    public async Task<MentorSkillDto> UpdateMySkillAsync(
        Guid mentorSkillId,
        UpdateMentorSkillRequestDto request)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var entity = await _unitOfWork.MentorSkills.GetByIdAsync(mentorSkillId);
        MentorSkillValidator.ValidateMentorSkillExists(entity, mentorSkillId);
        MentorSkillValidator.ValidateOwnership(entity!, mentorId);

        var utcNow = DateTime.UtcNow;
        MentorSkillValidator.ValidateYearsOfExperience(request.YearsOfExperience);
        MentorSkillValidator.ValidateDescription(request.Description);
        MentorSkillValidator.ValidateEvidenceList(request.Evidences, utcNow);

        entity!.ProficiencyLevel = request.ProficiencyLevel;
        entity.YearsOfExperience = request.YearsOfExperience;
        entity.Description = NormalizeOptionalText(request.Description);
        entity.Notes = NormalizeOptionalText(request.Notes);
        entity.IsPublic = request.IsPublic;

        await _unitOfWork.MentorSkills.Update(entity);

        if (request.Evidences != null)
        {
            await ReplaceEvidenceAsync(entity.Id, request.Evidences);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[UpdateMySkillAsync] Mentor {MentorId} updated mentor skill {MentorSkillId}.",
            mentorId,
            mentorSkillId);

        var skill = await _unitOfWork.Skills.GetByIdAsync(entity.SkillId);
        MentorSkillValidator.ValidateSkillExists(skill, entity.SkillId);
        var evidences = await LoadEvidencesByMentorSkillIdsAsync(new[] { entity.Id });
        return MapMentorSkill(entity, skill!, evidences.GetValueOrDefault(entity.Id));
    }

    public async Task<MentorSkillDto> SetMySkillVisibilityAsync(
        Guid mentorSkillId,
        UpdateMentorSkillVisibilityRequestDto request)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var entity = await _unitOfWork.MentorSkills.GetByIdAsync(mentorSkillId);
        MentorSkillValidator.ValidateMentorSkillExists(entity, mentorSkillId);
        MentorSkillValidator.ValidateOwnership(entity!, mentorId);

        entity!.IsPublic = request.IsPublic;
        await _unitOfWork.MentorSkills.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[SetMySkillVisibilityAsync] Mentor {MentorId} set mentor skill {MentorSkillId} IsPublic={IsPublic}.",
            mentorId,
            mentorSkillId,
            request.IsPublic);

        var skill = await _unitOfWork.Skills.GetByIdAsync(entity.SkillId);
        MentorSkillValidator.ValidateSkillExists(skill, entity.SkillId);
        var evidences = await LoadEvidencesByMentorSkillIdsAsync(new[] { entity.Id });
        return MapMentorSkill(entity, skill!, evidences.GetValueOrDefault(entity.Id));
    }

    public async Task RemoveMySkillAsync(Guid mentorSkillId)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var entity = await _unitOfWork.MentorSkills.GetByIdAsync(mentorSkillId);
        MentorSkillValidator.ValidateMentorSkillExists(entity, mentorSkillId);
        MentorSkillValidator.ValidateOwnership(entity!, mentorId);

        var evidences = await _unitOfWork.MentorSkillEvidences.GetAllAsync(
            e => e.MentorSkillId == mentorSkillId && !e.IsDeleted);
        if (evidences.Count > 0)
        {
            await _unitOfWork.MentorSkillEvidences.SoftRemoveRange(evidences);
        }

        await _unitOfWork.MentorSkills.SoftRemove(entity!);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[RemoveMySkillAsync] Mentor {MentorId} removed mentor skill {MentorSkillId}.",
            mentorId,
            mentorSkillId);
    }

    public async Task<Pagination<MentorProfileDto>> GetMentorsAsync(
        string? search,
        int page,
        int pageSize)
    {
        ClassMentorRequestValidator.ValidatePagination(page, pageSize);
        await EnsureManagerOrSuperAdminAsync();

        var query = _unitOfWork.Users
            .GetQueryable()
            .Where(u => !u.IsDeleted && u.Role == RoleType.Mentor);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(u =>
                u.Code.ToLower().Contains(lower) ||
                (u.FullName != null && u.FullName.ToLower().Contains(lower)) ||
                u.Email.ToLower().Contains(lower));
        }

        query = query.OrderBy(u => u.Code);

        var totalCount = query.Count();
        var mentors = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var dtos = new List<MentorProfileDto>();

        foreach (var mentor in mentors)
        {
            dtos.Add(await BuildProfileAsync(mentor, publicSkillsOnly: false));
        }

        return new Pagination<MentorProfileDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<MentorProfileDto> GetMentorProfileAsync(Guid mentorId)
    {
        var viewer = await EnsureCanViewMentorProfileAsync();

        var mentor = await _unitOfWork.Users.GetByIdAsync(mentorId);
        MentorSkillValidator.ValidateMentorUser(mentor, mentorId);

        if (mentor!.Role != RoleType.Mentor)
        {
            throw ErrorHelper.BadRequest($"User '{mentorId}' is not a Mentor.");
        }

        var publicSkillsOnly = viewer.Role == RoleType.Student;
        return await BuildProfileAsync(mentor, publicSkillsOnly);
    }

    public async Task<MentorProfileDto> GetMyProfileAsync()
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var mentor = await _unitOfWork.Users.GetByIdAsync(mentorId);
        MentorSkillValidator.ValidateMentorUser(mentor, mentorId);
        return await BuildProfileAsync(mentor!, publicSkillsOnly: false);
    }

    public async Task<MentorProfileDto> UpdateMyProfileAsync(UpdateMentorProfileRequestDto request)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var mentor = await _unitOfWork.Users.GetByIdAsync(mentorId);
        MentorSkillValidator.ValidateMentorUser(mentor, mentorId);

        var profile = await _unitOfWork.MentorProfiles.FirstOrDefaultAsync(
            mp => mp.MentorId == mentorId && !mp.IsDeleted);

        var isNew = profile == null;
        if (isNew)
        {
            profile = new MentorProfile
            {
                Id = Guid.NewGuid(),
                MentorId = mentorId,
            };
        }

        if (request.Title != null)
            profile!.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();

        if (request.Organization != null)
            profile!.Organization = string.IsNullOrWhiteSpace(request.Organization)
                ? null
                : request.Organization.Trim();

        if (request.Bio != null)
            profile!.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();

        if (request.Achievements != null)
            profile!.Achievements = string.IsNullOrWhiteSpace(request.Achievements)
                ? null
                : request.Achievements.Trim();

        if (request.LinkedInUrl != null)
            profile!.LinkedInUrl = string.IsNullOrWhiteSpace(request.LinkedInUrl)
                ? null
                : request.LinkedInUrl.Trim();

        if (isNew)
            await _unitOfWork.MentorProfiles.AddAsync(profile!);
        else
            await _unitOfWork.MentorProfiles.Update(profile!);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[UpdateMyProfileAsync] Mentor {MentorId} updated mentor profile.",
            mentorId);

        return await BuildProfileAsync(mentor!, publicSkillsOnly: false);
    }

    public async Task<MentorProfileDto> SetClassLimitAsync(
        Guid mentorId,
        UpdateMentorClassLimitRequestDto request)
    {
        await EnsureManagerOrSuperAdminAsync();
        MentorSkillValidator.ValidateClassLimitValue(request.MaxConcurrentClasses);

        var mentor = await _unitOfWork.Users.GetByIdAsync(mentorId);
        MentorSkillValidator.ValidateMentorUser(mentor, mentorId);

        if (mentor!.Role != RoleType.Mentor)
        {
            throw ErrorHelper.BadRequest($"User '{mentorId}' is not a Mentor.");
        }

        mentor.MaxConcurrentClasses = request.MaxConcurrentClasses;
        await _unitOfWork.Users.Update(mentor);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[SetClassLimitAsync] Mentor {MentorId} MaxConcurrentClasses set to {Limit}.",
            mentorId,
            request.MaxConcurrentClasses);

        return await BuildProfileAsync(mentor, publicSkillsOnly: false);
    }

    private async Task<MentorProfileDto> BuildProfileAsync(User mentor, bool publicSkillsOnly)
    {
        var (assigned, pending) = await ClassMentorRequestValidator.GetUsageBreakdownAsync(
            _unitOfWork,
            mentor.Id);
        var skills = await LoadSkillDtosAsync(mentor.Id, publicSkillsOnly);
        var effective = ClassMentorRequestValidator.ResolveMaxConcurrentClasses(mentor);
        var profile = await _unitOfWork.MentorProfiles.FirstOrDefaultAsync(
            mp => mp.MentorId == mentor.Id && !mp.IsDeleted);

        return new MentorProfileDto
        {
            Id = mentor.Id,
            Code = mentor.Code,
            FullName = mentor.FullName,
            Email = mentor.Email,
            Phone = mentor.Phone,
            AvatarUrl = mentor.AvatarUrl,
            Role = mentor.Role,
            Status = mentor.Status,
            MaxConcurrentClasses = mentor.MaxConcurrentClasses,
            EffectiveMaxConcurrentClasses = effective,
            AssignedClassCount = assigned,
            PendingRequestCount = pending,
            ConcurrentUsage = assigned + pending,
            Title = profile?.Title,
            Organization = profile?.Organization,
            Bio = profile?.Bio,
            Achievements = profile?.Achievements,
            LinkedInUrl = profile?.LinkedInUrl,
            Skills = skills,
        };
    }

    private async Task<List<MentorSkillDto>> LoadSkillDtosAsync(Guid mentorId, bool publicOnly)
    {
        var mentorSkills = await _unitOfWork.MentorSkills.GetAllAsync(
            ms => ms.MentorId == mentorId && !ms.IsDeleted && (!publicOnly || ms.IsPublic));

        if (mentorSkills.Count == 0)
        {
            return new List<MentorSkillDto>();
        }

        var skillIds = mentorSkills.Select(ms => ms.SkillId).Distinct().ToList();
        var skills = await _unitOfWork.Skills.GetAllAsync(s => skillIds.Contains(s.Id) && !s.IsDeleted);
        var skillsById = skills.ToDictionary(s => s.Id);

        var mentorSkillIds = mentorSkills.Select(ms => ms.Id).ToList();
        var evidencesByMentorSkillId = await LoadEvidencesByMentorSkillIdsAsync(mentorSkillIds);

        return mentorSkills
            .Where(ms => skillsById.ContainsKey(ms.SkillId))
            .OrderBy(ms => skillsById[ms.SkillId].Name)
            .Select(ms => MapMentorSkill(
                ms,
                skillsById[ms.SkillId],
                evidencesByMentorSkillId.GetValueOrDefault(ms.Id)))
            .ToList();
    }

    private async Task<Dictionary<Guid, List<MentorSkillEvidence>>> LoadEvidencesByMentorSkillIdsAsync(
        IReadOnlyCollection<Guid> mentorSkillIds)
    {
        if (mentorSkillIds.Count == 0)
        {
            return new Dictionary<Guid, List<MentorSkillEvidence>>();
        }

        var evidences = await _unitOfWork.MentorSkillEvidences.GetAllAsync(
            e => mentorSkillIds.Contains(e.MentorSkillId) && !e.IsDeleted);

        return evidences
            .GroupBy(e => e.MentorSkillId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(e => e.Title).ToList());
    }

    private async Task AddEvidenceRowsAsync(
        Guid mentorSkillId,
        IReadOnlyList<MentorSkillEvidenceRequestDto> evidences)
    {
        var entities = evidences.Select(e => MapEvidenceEntity(mentorSkillId, e)).ToList();
        await _unitOfWork.MentorSkillEvidences.AddRangeAsync(entities);
    }

    private async Task ReplaceEvidenceAsync(
        Guid mentorSkillId,
        IReadOnlyList<MentorSkillEvidenceRequestDto> evidences)
    {
        var existing = await _unitOfWork.MentorSkillEvidences.GetAllAsync(
            e => e.MentorSkillId == mentorSkillId && !e.IsDeleted);
        if (existing.Count > 0)
        {
            await _unitOfWork.MentorSkillEvidences.SoftRemoveRange(existing);
        }

        if (evidences.Count > 0)
        {
            await AddEvidenceRowsAsync(mentorSkillId, evidences);
        }
    }

    private static MentorSkillEvidence MapEvidenceEntity(
        Guid mentorSkillId,
        MentorSkillEvidenceRequestDto request)
        => new()
        {
            Id = Guid.NewGuid(),
            MentorSkillId = mentorSkillId,
            Title = request.Title.Trim(),
            Issuer = NormalizeOptionalText(request.Issuer),
            Url = request.Url.Trim(),
            IssuedAt = request.IssuedAt?.ToUniversalTime(),
            CredentialId = NormalizeOptionalText(request.CredentialId),
        };

    private static MentorSkillDto MapMentorSkill(
        MentorSkill entity,
        Skill skill,
        List<MentorSkillEvidence>? evidences)
        => new()
        {
            Id = entity.Id,
            MentorId = entity.MentorId,
            SkillId = entity.SkillId,
            Skill = new SkillSummaryDto
            {
                Id = skill.Id,
                Code = skill.Code,
                Name = skill.Name,
                Category = skill.Category,
                Subcategory = skill.Subcategory,
            },
            ProficiencyLevel = entity.ProficiencyLevel,
            YearsOfExperience = entity.YearsOfExperience,
            Description = entity.Description,
            Notes = entity.Notes,
            IsPublic = entity.IsPublic,
            Evidences = (evidences ?? new List<MentorSkillEvidence>())
                .Select(MapEvidenceDto)
                .ToList(),
            CreatedAt = entity.CreatedAt,
        };

    private static MentorSkillEvidenceDto MapEvidenceDto(MentorSkillEvidence entity)
        => new()
        {
            Id = entity.Id,
            Title = entity.Title,
            Issuer = entity.Issuer,
            Url = entity.Url,
            IssuedAt = entity.IssuedAt,
            CredentialId = entity.CredentialId,
        };

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<Guid> GetCurrentMentorIdAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user.Role != RoleType.Mentor)
        {
            throw ErrorHelper.Forbidden("Only mentors can perform this action.");
        }

        return user.Id;
    }

    private async Task<User> EnsureCanViewMentorProfileAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user.Role is not (RoleType.Manager or RoleType.SuperAdmin or RoleType.Student))
        {
            throw ErrorHelper.Forbidden("Only Student, Manager, or SuperAdmin can view mentor profiles.");
        }

        return user;
    }

    private async Task EnsureManagerOrSuperAdminAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user.Role is not (RoleType.Manager or RoleType.SuperAdmin))
        {
            throw ErrorHelper.Forbidden("Only Manager or SuperAdmin can manage mentor profiles.");
        }
    }

    private async Task<User> GetCurrentUserAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        return user;
    }
}
