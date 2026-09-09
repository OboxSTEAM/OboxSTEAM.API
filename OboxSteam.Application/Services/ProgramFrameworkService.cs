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

public sealed class ProgramFrameworkService : IProgramFrameworkService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ProgramFrameworkService> _logger;

    public ProgramFrameworkService(IUnitOfWork unitOfWork, IClaimsService claimsService, ILogger<ProgramFrameworkService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<Pagination<ProgramFrameworkResponseDto>> GetFrameworksAsync(
        string? search, ProgramCategory? category, int page, int pageSize)
    {
        var actor = await ResolveActorAsync();
        var query = _unitOfWork.ProgramFrameworks.GetQueryable().Where(f => !f.IsDeleted);
        if (actor.Role == RoleType.Expert)
        {
            var expert = await RequireCurrentExpertAsync(actor);
            query = query.Where(f => f.ExpertId == expert.Id);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(f => f.Name.ToLower().Contains(normalizedSearch));
        }

        if (category.HasValue) query = query.Where(f => f.Category == category.Value);

        var totalCount = query.Count();
        var frameworks = query.OrderBy(f => f.Name).ThenBy(f => f.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var items = new List<ProgramFrameworkResponseDto>();
        foreach (var framework in frameworks) items.Add(await MapFrameworkAsync(framework));
        return new Pagination<ProgramFrameworkResponseDto>(items, totalCount, page, pageSize);
    }

    public async Task<ProgramFrameworkResponseDto> GetFrameworkByIdAsync(Guid id)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(id);
        await EnsureCanReadAsync(actor, framework);
        return await MapFrameworkAsync(framework);
    }

    public async Task<ProgramFrameworkResponseDto> CreateFrameworkAsync(CreateProgramFrameworkRequest request)
    {
        var actor = await ResolveActorAsync();
        if (actor.Role != RoleType.Expert) throw ErrorHelper.Forbidden("Only an expert can create a program framework.");
        var expert = await RequireCurrentExpertAsync(actor);
        ValidateVersionFields(request.Name, request.MinModules, request.MinOfflineSessions, request.MinLiveSessions);
        ProgramFrameworkValidator.ValidateCriteriaList(request.Criteria);

        var framework = new ProgramFramework
        {
            Id = Guid.NewGuid(), ExpertId = expert.Id, Name = request.Name.Trim(), Category = request.Category,
        };
        var draft = new ProgramFrameworkVersion
        {
            Id = Guid.NewGuid(), FrameworkId = framework.Id, VersionNumber = 1,
            Description = NormalizeOptionalText(request.Description),
            AcademicGuidance = NormalizeOptionalText(request.AcademicGuidance),
            MinModules = request.MinModules, MinOfflineSessions = request.MinOfflineSessions,
            MinLiveSessions = request.MinLiveSessions,
            RequireCapstoneResearchMilestone = request.RequireCapstoneResearchMilestone,
        };
        await _unitOfWork.ProgramFrameworks.AddAsync(framework);
        await _unitOfWork.ProgramFrameworkVersions.AddAsync(draft);
        await AddCriteriaAsync(framework.Id, draft.Id, request.Criteria ?? []);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Expert {ExpertId} created framework {FrameworkId} with draft version 1.", expert.Id, framework.Id);
        return await MapFrameworkAsync(framework);
    }

    public async Task<ProgramFrameworkResponseDto> UpdateFrameworkAsync(Guid id, UpdateProgramFrameworkRequest request)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(id);
        await EnsureCanWriteAsync(actor, framework);
        var draft = await RequireDraftAsync(id);
        ProgramFrameworkValidator.ValidateName(request.Name, required: false);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(request.MinModules), request.MinModules);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(request.MinOfflineSessions), request.MinOfflineSessions);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(request.MinLiveSessions), request.MinLiveSessions);

        if (!string.IsNullOrWhiteSpace(request.Name)) framework.Name = request.Name.Trim();
        if (request.Category.HasValue) framework.Category = request.Category.Value;
        if (request.Description != null) draft.Description = NormalizeOptionalText(request.Description);
        if (request.AcademicGuidance != null) draft.AcademicGuidance = NormalizeOptionalText(request.AcademicGuidance);
        ApplyOptionalInt(request.MinModules, request.ClearMinModules, value => draft.MinModules = value);
        ApplyOptionalInt(request.MinOfflineSessions, request.ClearMinOfflineSessions, value => draft.MinOfflineSessions = value);
        ApplyOptionalInt(request.MinLiveSessions, request.ClearMinLiveSessions, value => draft.MinLiveSessions = value);
        if (request.RequireCapstoneResearchMilestone.HasValue) draft.RequireCapstoneResearchMilestone = request.RequireCapstoneResearchMilestone;
        else if (request.ClearRequireCapstoneResearchMilestone == true) draft.RequireCapstoneResearchMilestone = null;

        await _unitOfWork.ProgramFrameworks.Update(framework);
        await _unitOfWork.ProgramFrameworkVersions.Update(draft);
        await _unitOfWork.SaveChangesAsync();
        return await MapFrameworkAsync(framework);
    }

    public async Task<bool> DeleteFrameworkAsync(Guid id)
    {
        await ArchiveFrameworkAsync(id);
        return true;
    }

    public async Task<ProgramFrameworkResponseDto> ArchiveFrameworkAsync(Guid id)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(id);
        await EnsureCanWriteAsync(actor, framework);
        if (!framework.IsArchived)
        {
            framework.IsArchived = true;
            framework.ArchivedAt = DateTime.UtcNow;
            await _unitOfWork.ProgramFrameworks.Update(framework);
            await _unitOfWork.SaveChangesAsync();
        }
        return await MapFrameworkAsync(framework);
    }

    public async Task<IReadOnlyList<ProgramFrameworkVersionResponseDto>> GetVersionsAsync(Guid frameworkId)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(frameworkId);
        await EnsureCanReadAsync(actor, framework);
        var result = new List<ProgramFrameworkVersionResponseDto>();
        foreach (var version in (await GetVersionsInternalAsync(frameworkId)).OrderByDescending(v => v.VersionNumber))
            result.Add(await MapVersionAsync(version));
        return result;
    }

    public async Task<ProgramFrameworkVersionResponseDto> GetVersionAsync(Guid frameworkId, Guid versionId)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(frameworkId);
        await EnsureCanReadAsync(actor, framework);
        return await MapVersionAsync(await GetVersionInternalAsync(frameworkId, versionId));
    }

    public async Task<ProgramFrameworkVersionResponseDto> CreateDraftVersionAsync(Guid frameworkId)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(frameworkId);
        await EnsureCanWriteAsync(actor, framework);
        if (framework.IsArchived) throw ErrorHelper.Conflict("Archived frameworks cannot create new versions.");
        var versions = await GetVersionsInternalAsync(frameworkId);
        if (versions.Any(v => !v.IsPublished)) throw ErrorHelper.Conflict("This framework already has a draft version.");
        var source = versions.Where(v => v.IsPublished).OrderByDescending(v => v.VersionNumber).FirstOrDefault()
            ?? throw ErrorHelper.Conflict("Publish the initial draft before creating another version.");
        var draft = new ProgramFrameworkVersion
        {
            Id = Guid.NewGuid(), FrameworkId = frameworkId, VersionNumber = versions.Max(v => v.VersionNumber) + 1,
            Description = source.Description, AcademicGuidance = source.AcademicGuidance,
            MinModules = source.MinModules, MinOfflineSessions = source.MinOfflineSessions,
            MinLiveSessions = source.MinLiveSessions,
            RequireCapstoneResearchMilestone = source.RequireCapstoneResearchMilestone,
        };
        await _unitOfWork.ProgramFrameworkVersions.AddAsync(draft);
        var sourceCriteria = await GetCriteriaAsync(source.Id);
        await AddCriteriaAsync(frameworkId, draft.Id, sourceCriteria.Select(c => new FrameworkRubricCriterionRequest
        {
            Name = c.Name, Description = c.Description, EvidenceGuidance = c.EvidenceGuidance,
            MaxScore = c.MaxScore, DisplayOrder = c.DisplayOrder,
        }).ToList());
        await _unitOfWork.SaveChangesAsync();
        return await MapVersionAsync(draft);
    }

    public async Task<ProgramFrameworkVersionResponseDto> PublishDraftVersionAsync(Guid frameworkId, Guid versionId)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(frameworkId);
        await EnsureCanWriteAsync(actor, framework);
        if (framework.IsArchived) throw ErrorHelper.Conflict("Archived frameworks cannot publish new versions.");
        var version = await GetVersionInternalAsync(frameworkId, versionId);
        EnsureDraft(version);
        version.IsPublished = true;
        version.PublishedAt = DateTime.UtcNow;
        await _unitOfWork.ProgramFrameworkVersions.Update(version);
        await _unitOfWork.SaveChangesAsync();
        return await MapVersionAsync(version);
    }

    public async Task<ProgramFrameworkVersionResponseDto> SaveDraftRubricAsync(
        Guid frameworkId, Guid versionId, SaveFrameworkRubricRequest request)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(frameworkId);
        await EnsureCanWriteAsync(actor, framework);
        var version = await GetVersionInternalAsync(frameworkId, versionId);
        EnsureDraft(version);
        ProgramFrameworkValidator.ValidateCriteriaList(request.Criteria);
        var existing = await GetCriteriaAsync(version.Id);
        if (existing.Count > 0) await _unitOfWork.FrameworkRubricCriteria.SoftRemoveRange(existing);
        await AddCriteriaAsync(frameworkId, version.Id, request.Criteria);
        await _unitOfWork.SaveChangesAsync();
        return await MapVersionAsync(version);
    }

    public async Task<FrameworkRubricCriterionResponseDto> AddCriterionAsync(Guid frameworkId, FrameworkRubricCriterionRequest request)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(frameworkId);
        await EnsureCanWriteAsync(actor, framework);
        var draft = await RequireDraftAsync(frameworkId);
        ProgramFrameworkValidator.ValidateCriterion(request);
        var existing = await GetCriteriaAsync(draft.Id);
        var criterion = CreateCriterion(frameworkId, draft.Id, request,
            request.DisplayOrder ?? (existing.Count == 0 ? 1 : existing.Max(c => c.DisplayOrder) + 1));
        await _unitOfWork.FrameworkRubricCriteria.AddAsync(criterion);
        await _unitOfWork.SaveChangesAsync();
        return MapCriterion(criterion, frameworkId);
    }

    public async Task<FrameworkRubricCriterionResponseDto> UpdateCriterionAsync(
        Guid frameworkId, Guid criterionId, FrameworkRubricCriterionRequest request)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(frameworkId);
        await EnsureCanWriteAsync(actor, framework);
        var draft = await RequireDraftAsync(frameworkId);
        ProgramFrameworkValidator.ValidateCriterion(request);
        var criterion = await GetCriterionAsync(draft.Id, criterionId);
        criterion.Name = request.Name.Trim();
        criterion.Description = NormalizeOptionalText(request.Description);
        criterion.EvidenceGuidance = NormalizeOptionalText(request.EvidenceGuidance);
        criterion.MaxScore = request.MaxScore;
        if (request.DisplayOrder.HasValue) criterion.DisplayOrder = request.DisplayOrder.Value;
        await _unitOfWork.FrameworkRubricCriteria.Update(criterion);
        await _unitOfWork.SaveChangesAsync();
        return MapCriterion(criterion, frameworkId);
    }

    public async Task<bool> DeleteCriterionAsync(Guid frameworkId, Guid criterionId)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(frameworkId);
        await EnsureCanWriteAsync(actor, framework);
        var draft = await RequireDraftAsync(frameworkId);
        var criterion = await GetCriterionAsync(draft.Id, criterionId);
        await _unitOfWork.FrameworkRubricCriteria.SoftRemove(criterion);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task AddCriteriaAsync(
        Guid frameworkId,
        Guid versionId,
        IReadOnlyCollection<FrameworkRubricCriterionRequest> criteria)
    {
        var fallbackOrder = 1;
        foreach (var request in criteria)
        {
            await _unitOfWork.FrameworkRubricCriteria.AddAsync(
                CreateCriterion(frameworkId, versionId, request, request.DisplayOrder ?? fallbackOrder));
            fallbackOrder++;
        }
    }

    private static FrameworkRubricCriterion CreateCriterion(
        Guid frameworkId,
        Guid versionId,
        FrameworkRubricCriterionRequest request,
        int displayOrder) => new()
    {
        Id = Guid.NewGuid(), FrameworkId = frameworkId, FrameworkVersionId = versionId, Name = request.Name.Trim(),
        Description = NormalizeOptionalText(request.Description), EvidenceGuidance = NormalizeOptionalText(request.EvidenceGuidance),
        MaxScore = request.MaxScore, DisplayOrder = displayOrder,
    };

    private async Task<ProgramFramework> GetFrameworkAsync(Guid id)
    {
        var framework = await _unitOfWork.ProgramFrameworks.GetByIdAsync(id);
        if (framework == null || framework.IsDeleted) throw ErrorHelper.NotFound($"Program framework with id '{id}' not found.");
        return framework;
    }

    private async Task<List<ProgramFrameworkVersion>> GetVersionsInternalAsync(Guid frameworkId)
        => await _unitOfWork.ProgramFrameworkVersions.GetAllAsync(v => v.FrameworkId == frameworkId && !v.IsDeleted);

    private async Task<ProgramFrameworkVersion> GetVersionInternalAsync(Guid frameworkId, Guid versionId)
    {
        var version = await _unitOfWork.ProgramFrameworkVersions.GetByIdAsync(versionId);
        if (version == null || version.IsDeleted || version.FrameworkId != frameworkId)
            throw ErrorHelper.NotFound($"Framework version with id '{versionId}' not found.");
        return version;
    }

    private async Task<ProgramFrameworkVersion> RequireDraftAsync(Guid frameworkId)
        => await _unitOfWork.ProgramFrameworkVersions.FirstOrDefaultAsync(v => v.FrameworkId == frameworkId && !v.IsPublished && !v.IsDeleted)
            ?? throw ErrorHelper.Conflict("Create a draft framework version before editing.");

    private async Task<List<FrameworkRubricCriterion>> GetCriteriaAsync(Guid versionId)
        => await _unitOfWork.FrameworkRubricCriteria.GetAllAsync(c => c.FrameworkVersionId == versionId && !c.IsDeleted);

    private async Task<FrameworkRubricCriterion> GetCriterionAsync(Guid versionId, Guid criterionId)
    {
        var criterion = await _unitOfWork.FrameworkRubricCriteria.GetByIdAsync(criterionId);
        if (criterion == null || criterion.IsDeleted || criterion.FrameworkVersionId != versionId)
            throw ErrorHelper.NotFound($"Rubric criterion with id '{criterionId}' not found.");
        return criterion;
    }

    private async Task<User> ResolveActorAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty) throw ErrorHelper.Unauthorized("Unauthorized access.");
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted) throw ErrorHelper.NotFound("Current user not found.");
        if (user.Role is not (RoleType.Expert or RoleType.Manager or RoleType.Admin))
            throw ErrorHelper.Forbidden("Only Expert, Manager, or Admin can access program frameworks.");
        return user;
    }

    private async Task<Expert> RequireCurrentExpertAsync(User actor)
        => await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.UserId == actor.Id && !e.IsDeleted)
            ?? throw ErrorHelper.Forbidden("Current user is not linked to an expert profile.");

    private async Task EnsureCanReadAsync(User actor, ProgramFramework framework)
    {
        if (actor.Role is RoleType.Manager or RoleType.Admin) return;
        var expert = await RequireCurrentExpertAsync(actor);
        if (framework.ExpertId != expert.Id) throw ErrorHelper.NotFound($"Program framework with id '{framework.Id}' not found.");
    }

    private async Task EnsureCanWriteAsync(User actor, ProgramFramework framework)
    {
        if (actor.Role != RoleType.Expert) throw ErrorHelper.Forbidden("Only the authoring expert can perform this action.");
        var expert = await RequireCurrentExpertAsync(actor);
        if (framework.ExpertId != expert.Id) throw ErrorHelper.Forbidden("You can only manage your own program frameworks.");
    }

    private async Task<ProgramFrameworkResponseDto> MapFrameworkAsync(ProgramFramework framework)
    {
        var versions = await GetVersionsInternalAsync(framework.Id);
        var current = versions.FirstOrDefault(v => !v.IsPublished)
            ?? versions.Where(v => v.IsPublished).OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        var expert = await _unitOfWork.Experts.GetByIdAsync(framework.ExpertId);
        var versionDtos = new List<ProgramFrameworkVersionResponseDto>();
        foreach (var version in versions.OrderByDescending(v => v.VersionNumber)) versionDtos.Add(await MapVersionAsync(version));
        var currentDto = current == null ? null : versionDtos.Single(v => v.Id == current.Id);
        return new ProgramFrameworkResponseDto
        {
            Id = framework.Id, ExpertId = framework.ExpertId, ExpertName = expert?.FullName,
            Name = framework.Name, Category = framework.Category, IsArchived = framework.IsArchived,
            CurrentVersionId = current?.Id, CurrentVersionNumber = current?.VersionNumber,
            HasDraftVersion = versions.Any(v => !v.IsPublished), Description = currentDto?.Description,
            MinModules = currentDto?.MinModules, MinOfflineSessions = currentDto?.MinOfflineSessions,
            MinLiveSessions = currentDto?.MinLiveSessions,
            RequireCapstoneResearchMilestone = currentDto?.RequireCapstoneResearchMilestone,
            RequiresExpertReview = true, Criteria = currentDto?.Criteria.ToList() ?? [], Versions = versionDtos,
            CreatedAt = framework.CreatedAt, UpdatedAt = framework.UpdatedAt,
        };
    }

    private async Task<ProgramFrameworkVersionResponseDto> MapVersionAsync(ProgramFrameworkVersion version)
    {
        var criteria = await GetCriteriaAsync(version.Id);
        return new ProgramFrameworkVersionResponseDto
        {
            Id = version.Id, FrameworkId = version.FrameworkId, VersionNumber = version.VersionNumber,
            Description = version.Description, AcademicGuidance = version.AcademicGuidance,
            MinModules = version.MinModules, MinOfflineSessions = version.MinOfflineSessions,
            MinLiveSessions = version.MinLiveSessions,
            RequireCapstoneResearchMilestone = version.RequireCapstoneResearchMilestone,
            IsPublished = version.IsPublished, PublishedAt = version.PublishedAt,
            Criteria = criteria.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => MapCriterion(c, version.FrameworkId)).ToList(),
            CreatedAt = version.CreatedAt, UpdatedAt = version.UpdatedAt,
        };
    }

    private static FrameworkRubricCriterionResponseDto MapCriterion(FrameworkRubricCriterion criterion, Guid frameworkId) => new()
    {
        Id = criterion.Id, FrameworkId = frameworkId, FrameworkVersionId = criterion.FrameworkVersionId ?? Guid.Empty,
        Name = criterion.Name, Description = criterion.Description, EvidenceGuidance = criterion.EvidenceGuidance,
        MaxScore = criterion.MaxScore, DisplayOrder = criterion.DisplayOrder,
        CreatedAt = criterion.CreatedAt, UpdatedAt = criterion.UpdatedAt,
    };

    private static void ValidateVersionFields(string? name, int? minModules, int? minOffline, int? minLive)
    {
        ProgramFrameworkValidator.ValidateName(name, required: true);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(minModules), minModules);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(minOffline), minOffline);
        ProgramFrameworkValidator.ValidatePositiveConstraint(nameof(minLive), minLive);
    }

    private static void EnsureDraft(ProgramFrameworkVersion version)
    {
        if (version.IsPublished) throw ErrorHelper.Conflict("Published framework versions are immutable.");
    }

    private static void ApplyOptionalInt(int? value, bool? clear, Action<int?> assign)
    {
        if (value.HasValue) assign(value);
        else if (clear == true) assign(null);
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
