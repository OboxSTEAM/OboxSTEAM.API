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
        return await LoadSkillDtosAsync(mentorId);
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

        var entity = new MentorSkill
        {
            MentorId = mentorId,
            SkillId = request.SkillId,
            ProficiencyLevel = request.ProficiencyLevel,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        await _unitOfWork.MentorSkills.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[AddMySkillAsync] Mentor {MentorId} added skill {SkillId}.",
            mentorId,
            request.SkillId);

        return MapMentorSkill(entity, skill!);
    }

    public async Task RemoveMySkillAsync(Guid mentorSkillId)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var entity = await _unitOfWork.MentorSkills.GetByIdAsync(mentorSkillId);
        MentorSkillValidator.ValidateMentorSkillExists(entity, mentorSkillId);
        MentorSkillValidator.ValidateOwnership(entity!, mentorId);

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
            dtos.Add(await BuildProfileAsync(mentor));
        }

        return new Pagination<MentorProfileDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<MentorProfileDto> GetMentorProfileAsync(Guid mentorId)
    {
        await EnsureManagerOrSuperAdminAsync();

        var mentor = await _unitOfWork.Users.GetByIdAsync(mentorId);
        MentorSkillValidator.ValidateMentorUser(mentor, mentorId);

        if (mentor!.Role != RoleType.Mentor)
        {
            throw ErrorHelper.BadRequest($"User '{mentorId}' is not a Mentor.");
        }

        return await BuildProfileAsync(mentor);
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

        return await BuildProfileAsync(mentor);
    }

    private async Task<MentorProfileDto> BuildProfileAsync(User mentor)
    {
        var (assigned, pending) = await ClassMentorRequestValidator.GetUsageBreakdownAsync(
            _unitOfWork,
            mentor.Id);
        var skills = await LoadSkillDtosAsync(mentor.Id);
        var effective = ClassMentorRequestValidator.ResolveMaxConcurrentClasses(mentor);

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
            Skills = skills,
        };
    }

    private async Task<List<MentorSkillDto>> LoadSkillDtosAsync(Guid mentorId)
    {
        var mentorSkills = await _unitOfWork.MentorSkills.GetAllAsync(
            ms => ms.MentorId == mentorId && !ms.IsDeleted);

        if (mentorSkills.Count == 0)
        {
            return new List<MentorSkillDto>();
        }

        var skillIds = mentorSkills.Select(ms => ms.SkillId).Distinct().ToList();
        var skills = await _unitOfWork.Skills.GetAllAsync(s => skillIds.Contains(s.Id) && !s.IsDeleted);
        var skillsById = skills.ToDictionary(s => s.Id);

        return mentorSkills
            .Where(ms => skillsById.ContainsKey(ms.SkillId))
            .OrderBy(ms => skillsById[ms.SkillId].Name)
            .Select(ms => MapMentorSkill(ms, skillsById[ms.SkillId]))
            .ToList();
    }

    private static MentorSkillDto MapMentorSkill(MentorSkill entity, Skill skill)
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
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
        };

    private async Task<Guid> GetCurrentMentorIdAsync()
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

        if (user.Role != RoleType.Mentor)
        {
            throw ErrorHelper.Forbidden("Only mentors can perform this action.");
        }

        return userId;
    }

    private async Task EnsureManagerOrSuperAdminAsync()
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

        if (user.Role is not (RoleType.Manager or RoleType.SuperAdmin))
        {
            throw ErrorHelper.Forbidden("Only Manager or SuperAdmin can manage mentor profiles.");
        }
    }
}
