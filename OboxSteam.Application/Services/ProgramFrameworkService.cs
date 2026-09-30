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
        ProgramFrameworkValidator.ValidateName(request.Name, required: true);

        var framework = new ProgramFramework
        {
            Id = Guid.NewGuid(), ExpertId = expert.Id, Name = request.Name.Trim(), Category = request.Category,
        };
        var draft = new ProgramFrameworkVersion
        {
            Id = Guid.NewGuid(), FrameworkId = framework.Id, VersionNumber = 1,
            Description = NormalizeOptionalText(request.Description),
            AcademicGuidance = NormalizeOptionalText(request.AcademicGuidance),
            MinModules = request.MinModules,
            MaxModules = request.MaxModules,
            MinCoursesPerModule = request.MinCoursesPerModule,
            MaxCoursesPerModule = request.MaxCoursesPerModule,
            MinTotalHours = request.MinTotalHours,
            MaxTotalHours = request.MaxTotalHours,
            MaxActivityMinutes = request.MaxActivityMinutes,
            RequireActivityDuration = request.RequireActivityDuration,
            MinOfflineSessions = request.MinOfflineSessions,
            MinLiveSessions = request.MinLiveSessions,
            MinOfflineRatioPercent = request.MinOfflineRatioPercent,
            MinLiveRatioPercent = request.MinLiveRatioPercent,
            RequireAssignmentPerModule = request.RequireAssignmentPerModule,
            RequireAssignmentPassScore = request.RequireAssignmentPassScore,
            MinMaterialsPerActivity = request.MinMaterialsPerActivity,
            RequireCategoryMatch = request.RequireCategoryMatch,
            MinDescriptionLength = request.MinDescriptionLength,
            MinSkillsGained = request.MinSkillsGained,
            RequireThumbnail = request.RequireThumbnail,
            RequireCapstoneResearchMilestone = request.RequireCapstoneResearchMilestone,
        };
        ProgramFrameworkValidator.ValidateRules(draft);
        await _unitOfWork.ProgramFrameworks.AddAsync(framework);
        await _unitOfWork.ProgramFrameworkVersions.AddAsync(draft);
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

        if (!string.IsNullOrWhiteSpace(request.Name)) framework.Name = request.Name.Trim();
        if (request.Category.HasValue) framework.Category = request.Category.Value;
        if (request.Description != null) draft.Description = NormalizeOptionalText(request.Description);
        if (request.AcademicGuidance != null) draft.AcademicGuidance = NormalizeOptionalText(request.AcademicGuidance);
        ApplyRuleUpdates(draft, request);
        ProgramFrameworkValidator.ValidateRules(draft);

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
        return (await GetVersionsInternalAsync(frameworkId))
            .OrderByDescending(v => v.VersionNumber)
            .Select(MapVersion)
            .ToList();
    }

    public async Task<ProgramFrameworkVersionResponseDto> GetVersionAsync(Guid frameworkId, Guid versionId)
    {
        var actor = await ResolveActorAsync();
        var framework = await GetFrameworkAsync(frameworkId);
        await EnsureCanReadAsync(actor, framework);
        return MapVersion(await GetVersionInternalAsync(frameworkId, versionId));
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
            MinModules = source.MinModules,
            MaxModules = source.MaxModules,
            MinCoursesPerModule = source.MinCoursesPerModule,
            MaxCoursesPerModule = source.MaxCoursesPerModule,
            MinTotalHours = source.MinTotalHours,
            MaxTotalHours = source.MaxTotalHours,
            MaxActivityMinutes = source.MaxActivityMinutes,
            RequireActivityDuration = source.RequireActivityDuration,
            MinOfflineSessions = source.MinOfflineSessions,
            MinLiveSessions = source.MinLiveSessions,
            MinOfflineRatioPercent = source.MinOfflineRatioPercent,
            MinLiveRatioPercent = source.MinLiveRatioPercent,
            RequireAssignmentPerModule = source.RequireAssignmentPerModule,
            RequireAssignmentPassScore = source.RequireAssignmentPassScore,
            MinMaterialsPerActivity = source.MinMaterialsPerActivity,
            RequireCategoryMatch = source.RequireCategoryMatch,
            MinDescriptionLength = source.MinDescriptionLength,
            MinSkillsGained = source.MinSkillsGained,
            RequireThumbnail = source.RequireThumbnail,
            RequireCapstoneResearchMilestone = source.RequireCapstoneResearchMilestone,
        };
        await _unitOfWork.ProgramFrameworkVersions.AddAsync(draft);
        await _unitOfWork.SaveChangesAsync();
        return MapVersion(draft);
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
        return MapVersion(version);
    }

    private static void ApplyRuleUpdates(ProgramFrameworkVersion draft, UpdateProgramFrameworkRequest request)
    {
        ApplyOptionalInt(request.MinModules, request.ClearMinModules, value => draft.MinModules = value);
        ApplyOptionalInt(request.MaxModules, request.ClearMaxModules, value => draft.MaxModules = value);
        ApplyOptionalInt(request.MinCoursesPerModule, request.ClearMinCoursesPerModule, value => draft.MinCoursesPerModule = value);
        ApplyOptionalInt(request.MaxCoursesPerModule, request.ClearMaxCoursesPerModule, value => draft.MaxCoursesPerModule = value);
        ApplyOptionalInt(request.MinTotalHours, request.ClearMinTotalHours, value => draft.MinTotalHours = value);
        ApplyOptionalInt(request.MaxTotalHours, request.ClearMaxTotalHours, value => draft.MaxTotalHours = value);
        ApplyOptionalInt(request.MaxActivityMinutes, request.ClearMaxActivityMinutes, value => draft.MaxActivityMinutes = value);
        ApplyOptionalInt(request.MinOfflineSessions, request.ClearMinOfflineSessions, value => draft.MinOfflineSessions = value);
        ApplyOptionalInt(request.MinLiveSessions, request.ClearMinLiveSessions, value => draft.MinLiveSessions = value);
        ApplyOptionalInt(request.MinOfflineRatioPercent, request.ClearMinOfflineRatioPercent, value => draft.MinOfflineRatioPercent = value);
        ApplyOptionalInt(request.MinLiveRatioPercent, request.ClearMinLiveRatioPercent, value => draft.MinLiveRatioPercent = value);
        ApplyOptionalInt(request.MinMaterialsPerActivity, request.ClearMinMaterialsPerActivity, value => draft.MinMaterialsPerActivity = value);
        ApplyOptionalInt(request.MinDescriptionLength, request.ClearMinDescriptionLength, value => draft.MinDescriptionLength = value);
        ApplyOptionalInt(request.MinSkillsGained, request.ClearMinSkillsGained, value => draft.MinSkillsGained = value);

        if (request.RequireActivityDuration.HasValue) draft.RequireActivityDuration = request.RequireActivityDuration.Value;
        if (request.RequireAssignmentPerModule.HasValue) draft.RequireAssignmentPerModule = request.RequireAssignmentPerModule.Value;
        if (request.RequireAssignmentPassScore.HasValue) draft.RequireAssignmentPassScore = request.RequireAssignmentPassScore.Value;
        if (request.RequireCategoryMatch.HasValue) draft.RequireCategoryMatch = request.RequireCategoryMatch.Value;
        if (request.RequireThumbnail.HasValue) draft.RequireThumbnail = request.RequireThumbnail.Value;
        if (request.RequireCapstoneResearchMilestone.HasValue) draft.RequireCapstoneResearchMilestone = request.RequireCapstoneResearchMilestone;
        else if (request.ClearRequireCapstoneResearchMilestone == true) draft.RequireCapstoneResearchMilestone = null;
    }

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
        var versionDtos = versions.OrderByDescending(v => v.VersionNumber).Select(MapVersion).ToList();
        var currentDto = current == null ? null : versionDtos.Single(v => v.Id == current.Id);
        return new ProgramFrameworkResponseDto
        {
            Id = framework.Id, ExpertId = framework.ExpertId, ExpertName = expert?.FullName,
            Name = framework.Name, Category = framework.Category, IsArchived = framework.IsArchived,
            CurrentVersionId = current?.Id, CurrentVersionNumber = current?.VersionNumber,
            HasDraftVersion = versions.Any(v => !v.IsPublished), Description = currentDto?.Description,
            MinModules = currentDto?.MinModules,
            MaxModules = currentDto?.MaxModules,
            MinCoursesPerModule = currentDto?.MinCoursesPerModule,
            MaxCoursesPerModule = currentDto?.MaxCoursesPerModule,
            MinTotalHours = currentDto?.MinTotalHours,
            MaxTotalHours = currentDto?.MaxTotalHours,
            MaxActivityMinutes = currentDto?.MaxActivityMinutes,
            RequireActivityDuration = currentDto?.RequireActivityDuration ?? false,
            MinOfflineSessions = currentDto?.MinOfflineSessions,
            MinLiveSessions = currentDto?.MinLiveSessions,
            MinOfflineRatioPercent = currentDto?.MinOfflineRatioPercent,
            MinLiveRatioPercent = currentDto?.MinLiveRatioPercent,
            RequireAssignmentPerModule = currentDto?.RequireAssignmentPerModule ?? false,
            RequireAssignmentPassScore = currentDto?.RequireAssignmentPassScore ?? false,
            MinMaterialsPerActivity = currentDto?.MinMaterialsPerActivity,
            RequireCategoryMatch = currentDto?.RequireCategoryMatch ?? false,
            MinDescriptionLength = currentDto?.MinDescriptionLength,
            MinSkillsGained = currentDto?.MinSkillsGained,
            RequireThumbnail = currentDto?.RequireThumbnail ?? false,
            RequireCapstoneResearchMilestone = currentDto?.RequireCapstoneResearchMilestone,
            RequiresExpertReview = true, Versions = versionDtos,
            CreatedAt = framework.CreatedAt, UpdatedAt = framework.UpdatedAt,
        };
    }

    private static ProgramFrameworkVersionResponseDto MapVersion(ProgramFrameworkVersion version) => new()
    {
        Id = version.Id, FrameworkId = version.FrameworkId, VersionNumber = version.VersionNumber,
        Description = version.Description, AcademicGuidance = version.AcademicGuidance,
        MinModules = version.MinModules,
        MaxModules = version.MaxModules,
        MinCoursesPerModule = version.MinCoursesPerModule,
        MaxCoursesPerModule = version.MaxCoursesPerModule,
        MinTotalHours = version.MinTotalHours,
        MaxTotalHours = version.MaxTotalHours,
        MaxActivityMinutes = version.MaxActivityMinutes,
        RequireActivityDuration = version.RequireActivityDuration,
        MinOfflineSessions = version.MinOfflineSessions,
        MinLiveSessions = version.MinLiveSessions,
        MinOfflineRatioPercent = version.MinOfflineRatioPercent,
        MinLiveRatioPercent = version.MinLiveRatioPercent,
        RequireAssignmentPerModule = version.RequireAssignmentPerModule,
        RequireAssignmentPassScore = version.RequireAssignmentPassScore,
        MinMaterialsPerActivity = version.MinMaterialsPerActivity,
        RequireCategoryMatch = version.RequireCategoryMatch,
        MinDescriptionLength = version.MinDescriptionLength,
        MinSkillsGained = version.MinSkillsGained,
        RequireThumbnail = version.RequireThumbnail,
        RequireCapstoneResearchMilestone = version.RequireCapstoneResearchMilestone,
        IsPublished = version.IsPublished, PublishedAt = version.PublishedAt,
        CreatedAt = version.CreatedAt, UpdatedAt = version.UpdatedAt,
    };

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
