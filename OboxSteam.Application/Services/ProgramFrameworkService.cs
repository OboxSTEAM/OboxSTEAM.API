using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramFrameworkDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class ProgramFrameworkService : IProgramFrameworkService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ProgramFrameworkService> _logger;

    public ProgramFrameworkService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ProgramFrameworkService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<Pagination<ProgramFrameworkResponseDto>> GetFrameworksAsync(
        string? search,
        ProgramCategory? category,
        int page,
        int pageSize)
    {
        var actor = await ResolveActorAsync();

        var query = _unitOfWork.ProgramFrameworks
            .GetQueryable()
            .Where(f => !f.IsDeleted);

        if (actor.Role == RoleType.Expert)
        {
            var expert = await RequireCurrentExpertAsync(actor);
            query = query.Where(f => f.ExpertId == expert.Id);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(f => f.Name.ToLower().Contains(lowerSearch));
        }

        if (category.HasValue)
        {
            query = query.Where(f => f.Category == category.Value);
        }

        var totalCount = query.Count();
        var items = query
            .OrderBy(f => f.Name)
            .ThenBy(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var expertIds = items.Select(f => f.ExpertId).Distinct().ToList();
        var experts = expertIds.Count > 0
            ? await _unitOfWork.Experts.GetAllAsync(e => expertIds.Contains(e.Id) && !e.IsDeleted)
            : [];
        var expertsById = experts.ToDictionary(e => e.Id);

        var frameworkIds = items.Select(f => f.Id).ToList();
        var criteria = frameworkIds.Count > 0
            ? await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(
                c => frameworkIds.Contains(c.FrameworkId) && !c.IsDeleted)
            : [];
        var criteriaByFrameworkId = criteria
            .GroupBy(c => c.FrameworkId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToList());

        var dtos = items.Select(framework => MapFramework(
            framework,
            expertsById.GetValueOrDefault(framework.ExpertId),
            criteriaByFrameworkId.GetValueOrDefault(framework.Id) ?? [])).ToList();

        return new Pagination<ProgramFrameworkResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ProgramFrameworkResponseDto> GetFrameworkByIdAsync(Guid id)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetActiveFrameworkAsync(id);
        await EnsureCanReadAsync(actor, framework);
        return await MapFrameworkAsync(framework);
    }

    public async Task<ProgramFrameworkResponseDto> CreateFrameworkAsync(CreateProgramFrameworkRequest request)
    {
        var actor = await ResolveActorAsync();
        if (actor.Role != RoleType.Expert)
        {
            throw ErrorHelper.Forbidden("Only an expert can create a program framework.");
        }

        var expert = await RequireCurrentExpertAsync(actor);

        ProgramFrameworkValidator.ValidateName(request.Name, required: true);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(request.MinModules), request.MinModules);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(request.MinOfflineSessions), request.MinOfflineSessions);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(request.MinLiveSessions), request.MinLiveSessions);
        ProgramFrameworkValidator.ValidateCriteriaList(request.Criteria);

        var framework = new ProgramFramework
        {
            Id = Guid.NewGuid(),
            ExpertId = expert.Id,
            Name = request.Name.Trim(),
            Description = NormalizeOptionalText(request.Description),
            Category = request.Category,
            MinModules = request.MinModules,
            MinOfflineSessions = request.MinOfflineSessions,
            MinLiveSessions = request.MinLiveSessions,
            RequireFinalAssessment = request.RequireFinalAssessment,
        };

        await _unitOfWork.ProgramFrameworks.AddAsync(framework);

        if (request.Criteria is { Count: > 0 })
        {
            var displayOrder = 1;
            foreach (var item in request.Criteria)
            {
                await _unitOfWork.FrameworkRubricCriteria.AddAsync(new FrameworkRubricCriterion
                {
                    Id = Guid.NewGuid(),
                    FrameworkId = framework.Id,
                    Name = item.Name.Trim(),
                    Description = NormalizeOptionalText(item.Description),
                    MaxScore = item.MaxScore,
                    DisplayOrder = item.DisplayOrder ?? displayOrder,
                });
                displayOrder++;
            }
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[CreateFrameworkAsync] Expert {ExpertId} created framework {FrameworkId}.",
            expert.Id,
            framework.Id);

        return await MapFrameworkAsync(framework);
    }

    public async Task<ProgramFrameworkResponseDto> UpdateFrameworkAsync(Guid id, UpdateProgramFrameworkRequest request)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetActiveFrameworkAsync(id);
        await EnsureCanWriteAsync(actor, framework, allowManagerOverride: true);

        ProgramFrameworkValidator.ValidateName(request.Name, required: false);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(request.MinModules), request.MinModules);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(request.MinOfflineSessions), request.MinOfflineSessions);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(request.MinLiveSessions), request.MinLiveSessions);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            framework.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            framework.Description = NormalizeOptionalText(request.Description);
        }

        if (request.Category.HasValue)
        {
            framework.Category = request.Category.Value;
        }

        ApplyOptionalInt(request.MinModules, request.ClearMinModules, value => framework.MinModules = value);
        ApplyOptionalInt(
            request.MinOfflineSessions,
            request.ClearMinOfflineSessions,
            value => framework.MinOfflineSessions = value);
        ApplyOptionalInt(
            request.MinLiveSessions,
            request.ClearMinLiveSessions,
            value => framework.MinLiveSessions = value);

        if (request.RequireFinalAssessment.HasValue)
        {
            framework.RequireFinalAssessment = request.RequireFinalAssessment;
        }
        else if (request.ClearRequireFinalAssessment == true)
        {
            framework.RequireFinalAssessment = null;
        }

        await _unitOfWork.ProgramFrameworks.Update(framework);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[UpdateFrameworkAsync] User {UserId} updated framework {FrameworkId}.",
            actor.Id,
            framework.Id);

        return await MapFrameworkAsync(framework);
    }

    public async Task<bool> DeleteFrameworkAsync(Guid id)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetActiveFrameworkAsync(id);
        await EnsureCanWriteAsync(actor, framework, allowManagerOverride: false);

        var attachedPrograms = await _unitOfWork.Programs.GetAllAsync(
            p => p.FrameworkId == id && !p.IsDeleted);
        foreach (var program in attachedPrograms)
        {
            program.FrameworkId = null;
            await _unitOfWork.Programs.Update(program);
        }

        var criteria = await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(
            c => c.FrameworkId == id && !c.IsDeleted);
        if (criteria.Count > 0)
        {
            await _unitOfWork.FrameworkRubricCriteria.SoftRemoveRange(criteria);
        }

        await _unitOfWork.ProgramFrameworks.SoftRemove(framework);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[DeleteFrameworkAsync] Expert {UserId} deleted framework {FrameworkId}.",
            actor.Id,
            id);

        return true;
    }

    public async Task<FrameworkRubricCriterionResponseDto> AddCriterionAsync(
        Guid frameworkId,
        FrameworkRubricCriterionRequest request)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetActiveFrameworkAsync(frameworkId);
        await EnsureCanWriteAsync(actor, framework, allowManagerOverride: true);

        ProgramFrameworkValidator.ValidateCriterion(request);

        var existing = await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(
            c => c.FrameworkId == frameworkId && !c.IsDeleted);
        var displayOrder = request.DisplayOrder
            ?? (existing.Count == 0 ? 1 : existing.Max(c => c.DisplayOrder) + 1);

        var criterion = new FrameworkRubricCriterion
        {
            Id = Guid.NewGuid(),
            FrameworkId = frameworkId,
            Name = request.Name.Trim(),
            Description = NormalizeOptionalText(request.Description),
            MaxScore = request.MaxScore,
            DisplayOrder = displayOrder,
        };

        await _unitOfWork.FrameworkRubricCriteria.AddAsync(criterion);
        await _unitOfWork.SaveChangesAsync();

        return MapCriterion(criterion);
    }

    public async Task<FrameworkRubricCriterionResponseDto> UpdateCriterionAsync(
        Guid frameworkId,
        Guid criterionId,
        FrameworkRubricCriterionRequest request)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetActiveFrameworkAsync(frameworkId);
        await EnsureCanWriteAsync(actor, framework, allowManagerOverride: true);

        ProgramFrameworkValidator.ValidateCriterion(request);

        var criterion = await GetActiveCriterionAsync(frameworkId, criterionId);
        criterion.Name = request.Name.Trim();
        criterion.Description = NormalizeOptionalText(request.Description);
        criterion.MaxScore = request.MaxScore;
        if (request.DisplayOrder.HasValue)
        {
            criterion.DisplayOrder = request.DisplayOrder.Value;
        }

        await _unitOfWork.FrameworkRubricCriteria.Update(criterion);
        await _unitOfWork.SaveChangesAsync();

        return MapCriterion(criterion);
    }

    public async Task<bool> DeleteCriterionAsync(Guid frameworkId, Guid criterionId)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetActiveFrameworkAsync(frameworkId);
        await EnsureCanWriteAsync(actor, framework, allowManagerOverride: true);

        var criterion = await GetActiveCriterionAsync(frameworkId, criterionId);
        await _unitOfWork.FrameworkRubricCriteria.SoftRemove(criterion);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task<ProgramFramework> GetActiveFrameworkAsync(Guid id)
    {
        var framework = await _unitOfWork.ProgramFrameworks.GetByIdAsync(id);
        if (framework == null || framework.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program framework with id '{id}' not found.");
        }

        return framework;
    }

    private async Task<FrameworkRubricCriterion> GetActiveCriterionAsync(Guid frameworkId, Guid criterionId)
    {
        var criterion = await _unitOfWork.FrameworkRubricCriteria.GetByIdAsync(criterionId);
        if (criterion == null || criterion.IsDeleted || criterion.FrameworkId != frameworkId)
        {
            throw ErrorHelper.NotFound($"Rubric criterion with id '{criterionId}' not found.");
        }

        return criterion;
    }

    private async Task<User> ResolveActorAsync()
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

        if (user.Role is not (RoleType.Expert or RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.Forbidden("Only Expert, Manager, or Admin can access program frameworks.");
        }

        return user;
    }

    private async Task<Expert> RequireCurrentExpertAsync(User actor)
    {
        var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(
            e => e.UserId == actor.Id && !e.IsDeleted);
        if (expert == null)
        {
            throw ErrorHelper.Forbidden("Current user is not linked to an expert profile.");
        }

        return expert;
    }

    private async Task EnsureCanReadAsync(User actor, ProgramFramework framework)
    {
        if (actor.Role is RoleType.Manager or RoleType.Admin)
        {
            return;
        }

        var expert = await RequireCurrentExpertAsync(actor);
        if (framework.ExpertId != expert.Id)
        {
            throw ErrorHelper.NotFound($"Program framework with id '{framework.Id}' not found.");
        }
    }

    private async Task EnsureCanWriteAsync(
        User actor,
        ProgramFramework framework,
        bool allowManagerOverride)
    {
        if (actor.Role == RoleType.Expert)
        {
            var expert = await RequireCurrentExpertAsync(actor);
            if (framework.ExpertId != expert.Id)
            {
                throw ErrorHelper.Forbidden("You can only manage your own program frameworks.");
            }

            return;
        }

        if (allowManagerOverride && actor.Role is RoleType.Manager or RoleType.Admin)
        {
            return;
        }

        throw ErrorHelper.Forbidden("Only the owning expert can perform this action.");
    }

    private async Task<ProgramFrameworkResponseDto> MapFrameworkAsync(ProgramFramework framework)
    {
        var expert = await _unitOfWork.Experts.GetByIdAsync(framework.ExpertId);
        var criteria = await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(
            c => c.FrameworkId == framework.Id && !c.IsDeleted);
        return MapFramework(
            framework,
            expert == null || expert.IsDeleted ? null : expert,
            criteria.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToList());
    }

    private static ProgramFrameworkResponseDto MapFramework(
        ProgramFramework framework,
        Expert? expert,
        IReadOnlyList<FrameworkRubricCriterion> criteria) => new()
    {
        Id = framework.Id,
        ExpertId = framework.ExpertId,
        ExpertName = expert?.FullName,
        Name = framework.Name,
        Description = framework.Description,
        Category = framework.Category,
        MinModules = framework.MinModules,
        MinOfflineSessions = framework.MinOfflineSessions,
        MinLiveSessions = framework.MinLiveSessions,
        RequireFinalAssessment = framework.RequireFinalAssessment,
        RequiresExpertReview = criteria.Count > 0,
        Criteria = criteria.Select(MapCriterion).ToList(),
        CreatedAt = framework.CreatedAt,
        UpdatedAt = framework.UpdatedAt,
    };

    private static FrameworkRubricCriterionResponseDto MapCriterion(FrameworkRubricCriterion criterion) => new()
    {
        Id = criterion.Id,
        FrameworkId = criterion.FrameworkId,
        Name = criterion.Name,
        Description = criterion.Description,
        MaxScore = criterion.MaxScore,
        DisplayOrder = criterion.DisplayOrder,
        CreatedAt = criterion.CreatedAt,
        UpdatedAt = criterion.UpdatedAt,
    };

    private static void ApplyOptionalInt(int? value, bool? clear, Action<int?> assign)
    {
        if (value.HasValue)
        {
            assign(value);
            return;
        }

        if (clear == true)
        {
            assign(null);
        }
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
