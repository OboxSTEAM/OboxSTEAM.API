
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ModuleDTO;
using OboxSteam.Application.DTOs.ProgramDTO;

using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class ProgramService : IProgramService
{
    private static readonly HashSet<string> AllowedThumbnailExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxThumbnailSize = 5 * 1024 * 1024; // 5 MB
    private const string ThumbnailFolder = "program-thumbnails";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;
    private readonly ILogger<ProgramService> _logger;

    public ProgramService(IUnitOfWork unitOfWork, IBlobService blobService, ILogger<ProgramService> logger)
    {
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _logger = logger;
    }

    // =========================================================================
    // GET BY ID
    // =========================================================================

    public async Task<ProgramsResponseDto> GetProgramByIdAsync(Guid id)
    {
        _logger.LogInformation("[GetProgramByIdAsync] Fetching program with Id: {Id}", id);

        var program = await _unitOfWork.Programs.GetByIdAsync(id, p => p.Modules);

        if (program == null || program.IsDeleted)
        {
            _logger.LogWarning("[GetProgramByIdAsync] Program with Id {Id} not found.", id);
            throw ErrorHelper.NotFound($"Program with id '{id}' not found.");
        }

        var programBoards = await _unitOfWork.ProgramBoards.GetAllAsync(pb => pb.ProgramId == id);
        var expertIds = programBoards.Select(pb => pb.ExpertId).Distinct().ToList();
        var experts = expertIds.Any()
            ? await _unitOfWork.Experts.GetAllAsync(e => expertIds.Contains(e.Id) && !e.IsDeleted)
            : new List<Expert>();
        var expertsById = experts.ToDictionary(e => e.Id, e => e);

        _logger.LogInformation("[GetProgramByIdAsync] Program with Id {Id} retrieved successfully.", id);
        return new ProgramsResponseDto
        {
            Id = program.Id,
            Code = program.Code,
            Name = program.Name,
            SeriesName = program.SeriesName,
            Description = program.Description,
            Level = program.Level,
            Category = program.Category,
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
            FrameworkId = program.FrameworkId,
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
            Modules = program.Modules?.OrderBy(m => m.ModuleOrder).Select(m => new ModulesResponseDto
            {
                Id = m.Id,
                Code = m.Code,
                ProgramId = m.ProgramId,
                Name = m.Name,
                ModuleType = m.ModuleType,
                ModuleOrder = m.ModuleOrder,
                PrerequisiteModuleId = m.PrerequisiteModuleId,
                IsMandatory = m.IsMandatory,
                LearningOutcomes = m.LearningOutcomes,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
            }).ToList() ?? new(),
            Experts = MapExpertsForProgram(programBoards, expertsById),
        };
    }

    // =========================================================================
    // GET CURRICULUM (compact tree)
    // =========================================================================

    public async Task<ProgramCurriculumDto> GetProgramCurriculumAsync(Guid id)
    {
        _logger.LogInformation("[GetProgramCurriculumAsync] Fetching curriculum for program Id: {Id}", id);

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, id);

        _logger.LogInformation(
            "[GetProgramCurriculumAsync] Curriculum for program Id {Id} retrieved — {ModuleCount} module(s).",
            id,
            snapshot.Modules.Count);

        return ProgramCurriculumTreeMapper.ToProgramCurriculumDto(snapshot);
    }

    // =========================================================================
    // GET BY NAME
    // =========================================================================

    public async Task<ProgramsResponseDto> GetProgramByNameAsync(string name)
    {
        _logger.LogInformation("[GetProgramByNameAsync] Fetching program with name: {Name}", name);

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Name.ToLower() == name.ToLower() && !p.IsDeleted,
            p => p.Modules);

        if (program == null)
        {
            _logger.LogWarning("[GetProgramByNameAsync] Program with name '{Name}' not found.", name);
            throw ErrorHelper.NotFound($"Program with name '{name}' not found.");
        }

        _logger.LogInformation("[GetProgramByNameAsync] Program '{Name}' retrieved successfully.", name);
        return new ProgramsResponseDto
        {
            Id = program.Id,
            Code = program.Code,
            Name = program.Name,
            SeriesName = program.SeriesName,
            Description = program.Description,
            Level = program.Level,
            Category = program.Category,
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
            FrameworkId = program.FrameworkId,
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
            Modules = program.Modules?.OrderBy(m => m.ModuleOrder).Select(m => new ModulesResponseDto
            {
                Id = m.Id,
                Code = m.Code,
                ProgramId = m.ProgramId,
                Name = m.Name,
                ModuleType = m.ModuleType,
                ModuleOrder = m.ModuleOrder,
                PrerequisiteModuleId = m.PrerequisiteModuleId,
                IsMandatory = m.IsMandatory,
                LearningOutcomes = m.LearningOutcomes,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
            }).ToList() ?? new(),
        };
    }

    // =========================================================================
    // GET ALL (PAGINATION + FILTER + SORT)
    // =========================================================================

    public async Task<Pagination<ProgramListItemDto>> GetAllProgramsAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code = null,
        DifficultyLevel? level = null,
        decimal? rating = null,
        string? skillsGained = null,
        ProgramStatus? status = null,
        ProgramCategory? category = null)
    {
        _logger.LogInformation(
            "[GetAllProgramsAsync] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
            page, pageSize, search);

        var query = BuildProgramsQuery(search, sortBy, isDescending, code, level, rating, skillsGained, status, category);

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var programIds = items.Select(program => program.Id).ToList();

        var programBoards = programIds.Any()
            ? await _unitOfWork.ProgramBoards.GetAllAsync(pb => programIds.Contains(pb.ProgramId))
            : new List<ProgramBoard>();

        var expertIds = programBoards.Select(pb => pb.ExpertId).Distinct().ToList();
        var experts = expertIds.Any()
            ? await _unitOfWork.Experts.GetAllAsync(e => expertIds.Contains(e.Id) && !e.IsDeleted)
            : new List<Expert>();

        var expertsById = experts.ToDictionary(e => e.Id, e => e);
        var programBoardsByProgramId = programBoards
            .GroupBy(pb => pb.ProgramId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var dtos = items.Select(program =>
        {
            var dto = MapToProgramListItemDto(program);
            dto.Experts = programBoardsByProgramId.TryGetValue(program.Id, out var boards)
                ? MapExpertsForProgram(boards, expertsById)
                : new();
            return dto;
        }).ToList();

        _logger.LogInformation("[GetAllProgramsAsync] Retrieved {Count}/{Total} programs.", dtos.Count, totalCount);

        return new Pagination<ProgramListItemDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<Pagination<ProgramsResponseDto>> GetAllProgramsWithModulesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code = null,
        DifficultyLevel? level = null,
        decimal? rating = null,
        string? skillsGained = null,
        ProgramStatus? status = null,
        ProgramCategory? category = null)
    {
        _logger.LogInformation(
            "[GetAllProgramsWithModulesAsync] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
            page, pageSize, search);

        var query = BuildProgramsQuery(search, sortBy, isDescending, code, level, rating, skillsGained, status, category);

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var programIds = items.Select(program => program.Id).ToList();

        var modules = await _unitOfWork.Repository<Module>().GetAllAsync(
            module => programIds.Contains(module.ProgramId) && !module.IsDeleted);

        var modulesByProgramId = modules
            .GroupBy(module => module.ProgramId)
            .ToDictionary(group => group.Key, group => group.OrderBy(m => m.ModuleOrder).ToList());

        var dtos = items.Select(program => new ProgramsResponseDto
        {
            Id = program.Id,
            Code = program.Code,
            Name = program.Name,
            SeriesName = program.SeriesName,
            Description = program.Description,
            Level = program.Level,
            Category = program.Category,
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
            FrameworkId = program.FrameworkId,
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
            Modules = modulesByProgramId.TryGetValue(program.Id, out var programModules)
                ? programModules.Select(m => new ModulesResponseDto
                {
                    Id = m.Id,
                    Code = m.Code,
                    ProgramId = m.ProgramId,
                    Name = m.Name,
                    ModuleType = m.ModuleType,
                    ModuleOrder = m.ModuleOrder,
                    PrerequisiteModuleId = m.PrerequisiteModuleId,
                    IsMandatory = m.IsMandatory,
                    LearningOutcomes = m.LearningOutcomes,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt,
                }).ToList()
                : new(),
        }).ToList();

        _logger.LogInformation(
            "[GetAllProgramsWithModulesAsync] Retrieved {Count}/{Total} programs.",
            dtos.Count,
            totalCount);

        return new Pagination<ProgramsResponseDto>(dtos, totalCount, page, pageSize);
    }

    private IQueryable<Program> BuildProgramsQuery(
        string? search,
        string? sortBy,
        bool isDescending,
        string? code,
        DifficultyLevel? level,
        decimal? rating,
        string? skillsGained,
        ProgramStatus? status,
        ProgramCategory? category = null)
    {
        var query = _unitOfWork.Programs
            .GetQueryable()
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(lowerSearch) ||
                p.Code.ToLower().Contains(lowerSearch));
        }

        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(p => p.Code.ToLower().Contains(code.ToLower()));

        if (level.HasValue)
            query = query.Where(p => p.Level == level.Value);

        if (rating.HasValue)
            query = query.Where(p => p.Rating >= rating.Value);

        if (!string.IsNullOrWhiteSpace(skillsGained))
            query = query.Where(p =>
                p.SkillsGained != null &&
                p.SkillsGained.ToLower().Contains(skillsGained.ToLower()));

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (category.HasValue)
            query = query.Where(p => p.Category == category.Value);

        return sortBy?.ToLower() switch
        {
            "name" => isDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "code" => isDescending ? query.OrderByDescending(p => p.Code) : query.OrderBy(p => p.Code),
            "level" => isDescending ? query.OrderByDescending(p => p.Level) : query.OrderBy(p => p.Level),
            "rating" => isDescending ? query.OrderByDescending(p => p.Rating) : query.OrderBy(p => p.Rating),
            "price" => isDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "createdat" => isDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            _ => isDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
        };
    }

    private static List<ProgramExpertSummaryDto> MapExpertsForProgram(
        IEnumerable<ProgramBoard> boards,
        IReadOnlyDictionary<Guid, Expert> expertsById) =>
        boards
            .Where(pb => expertsById.ContainsKey(pb.ExpertId))
            .Select(pb =>
            {
                var expert = expertsById[pb.ExpertId];
                return new ProgramExpertSummaryDto
                {
                    ExpertId = expert.Id,
                    Code = expert.Code,
                    FullName = expert.FullName,
                    Title = expert.Title,
                    Organization = expert.Organization,
                    AvatarUrl = expert.AvatarUrl,
                    LinkedInUrl = expert.LinkedInUrl,
                    RoleInBoard = pb.RoleInBoard,
                };
            })
            .ToList();

    private static ProgramListItemDto MapToProgramListItemDto(Program program) => new()
    {
        Id = program.Id,
        Code = program.Code,
        Name = program.Name,
        SeriesName = program.SeriesName,
        Description = program.Description,
        Level = program.Level,
        Category = program.Category,
        EstimatedDuration = program.EstimatedDuration,
        SkillsGained = program.SkillsGained,
        Rating = program.Rating,
        TotalReviews = program.TotalReviews,
        ThumbnailUrl = program.ThumbnailUrl,
        Status = program.Status,
        Price = program.Price,
            FrameworkId = program.FrameworkId,
        CreatedAt = program.CreatedAt,
        UpdatedAt = program.UpdatedAt,
    };

    // =========================================================================
    // CREATE
    // =========================================================================


    public async Task<ProgramsResponseDto> CreateProgramAsync(CreateProgramRequestDto request, IFormFile? thumbnailFile = null)
    {
        _logger.LogInformation("[CreateProgramAsync] Start creating program: {Name} (Code: {Code})",
            request.Name, request.Code);

        // Kiểm tra trùng Code
        var existing = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code.ToLower() == request.Code.ToLower() && !p.IsDeleted);

        if (existing != null)
        { 
            _logger.LogWarning("[CreateProgramAsync] Program with code '{Code}' already exists.", request.Code);
            throw ErrorHelper.Conflict($"Program with code '{request.Code}' already exists.");
        }

        ProgramCatalogStatusGuard.EnsureCreateIsDraft(request.Status);

        var program = new Program
        {
            Code = request.Code,
            Name = request.Name,
            SeriesName = request.SeriesName,
            Description = request.Description,
            Level = request.Level,
            Category = request.Category,
            EstimatedDuration = request.EstimatedDuration,
            SkillsGained = request.SkillsGained,
            ThumbnailUrl = request.ThumbnailUrl,
            Status = ProgramStatus.Draft,
            Price = request.Price,
            FrameworkId = await ResolveFrameworkIdAsync(request.FrameworkId),
        };

        if (thumbnailFile != null)
        {
            if (program.Id == Guid.Empty)
            {
                program.Id = Guid.NewGuid();
            }

            program.ThumbnailUrl = await UploadThumbnailFileAsync(program.Id, thumbnailFile);
        }

        await _unitOfWork.Programs.AddAsync(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[CreateProgramAsync] Program '{Code}' added successfully with Id {Id}.",
            program.Code, program.Id);

        return new ProgramsResponseDto
        {
            Id = program.Id,
            Code = program.Code,
            Name = program.Name,
            SeriesName = program.SeriesName,
            Description = program.Description,
            Level = program.Level,
            Category = program.Category,
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
            FrameworkId = program.FrameworkId,
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
            Modules = new(),
        };
    }

    // =========================================================================
    // UPDATE
    // =========================================================================

    public async Task<ProgramsResponseDto> UpdateProgramAsync(Guid id, UpdateProgramRequestDto request)
    {
        _logger.LogInformation("[UpdateProgramAsync] Attempting to update program with Id: {Id}", id);

        var program = await _unitOfWork.Programs.GetByIdAsync(id, p => p.Modules);

        if (program == null || program.IsDeleted)
        {
            _logger.LogWarning("[UpdateProgramAsync] Program with Id {Id} not found.", id);
            throw ErrorHelper.NotFound($"Program with id '{id}' not found.");
        }

        await CurriculumEditGuard.EnsureProgramEditableAsync(_unitOfWork, id);

        // Kiểm tra trùng Code khi đổi Code
        if (!string.IsNullOrWhiteSpace(request.Code) &&
            !program.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _unitOfWork.Programs.FirstOrDefaultAsync(
                p => p.Code.ToLower() == request.Code.ToLower() &&
                     !p.IsDeleted &&
                     p.Id != id);

            if (duplicate != null)
            {
                _logger.LogWarning("[UpdateProgramAsync] Code '{Code}' is already in use.", request.Code);
                throw ErrorHelper.Conflict($"Program with code '{request.Code}' already exists.");
            }
        }

        var requestedStatus = request.Status;
        request.Status = null;
        var statusChanged = ProgramCatalogStatusGuard.ApplyUpdate(program, requestedStatus);
        var frameworkChanged = await ApplyFrameworkAssignmentAsync(program, request);
        var isUpdated = UpdateHelper.ApplyUpdates(program, request) || frameworkChanged || statusChanged;

        if (!isUpdated)
        {
            _logger.LogWarning("[UpdateProgramAsync] No changes detected for program Id: {Id}", id);
            return new ProgramsResponseDto
            {
                Id = program.Id,
                Code = program.Code,
                Name = program.Name,
                SeriesName = program.SeriesName,
                Description = program.Description,
                Level = program.Level,
                Category = program.Category,
                EstimatedDuration = program.EstimatedDuration,
                SkillsGained = program.SkillsGained,
                Rating = program.Rating,
                TotalReviews = program.TotalReviews,
                ThumbnailUrl = program.ThumbnailUrl,
                Status = program.Status,
                Price = program.Price,
            FrameworkId = program.FrameworkId,
                CreatedAt = program.CreatedAt,
                UpdatedAt = program.UpdatedAt,
                Modules = program.Modules?.Select(m => new ModulesResponseDto
                {
                    Id = m.Id,
                    Code = m.Code,
                    ProgramId = m.ProgramId,
                    Name = m.Name,
                    ModuleType = m.ModuleType,
                    ModuleOrder = m.ModuleOrder,
                    PrerequisiteModuleId = m.PrerequisiteModuleId,
                    IsMandatory = m.IsMandatory,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt,
                }).ToList() ?? new(),
            };
        }

        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[UpdateProgramAsync] Program Id {Id} updated successfully.", id);

        return new ProgramsResponseDto
        {
            Id = program.Id,
            Code = program.Code,
            Name = program.Name,
            SeriesName = program.SeriesName,
            Description = program.Description,
            Level = program.Level,
            Category = program.Category,
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
            FrameworkId = program.FrameworkId,
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
            Modules = program.Modules?.Select(m => new ModulesResponseDto
            {
                Id = m.Id,
                Code = m.Code,
                ProgramId = m.ProgramId,
                Name = m.Name,
                ModuleType = m.ModuleType,
                ModuleOrder = m.ModuleOrder,
                PrerequisiteModuleId = m.PrerequisiteModuleId,
                IsMandatory = m.IsMandatory,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
            }).ToList() ?? new(),
        };
    }

    public async Task<ProgramsResponseDto> UploadProgramThumbnailAsync(Guid id, IFormFile file)
    {
        _logger.LogInformation("[UploadProgramThumbnailAsync] Uploading thumbnail for ProgramId: {ProgramId}", id);

        var program = await _unitOfWork.Programs.GetByIdAsync(id, p => p.Modules);
        if (program == null || program.IsDeleted)
            throw ErrorHelper.NotFound($"Program with id '{id}' not found.");

        await CurriculumEditGuard.EnsureProgramEditableAsync(_unitOfWork, id);

        if (!string.IsNullOrWhiteSpace(program.ThumbnailUrl))
        {
            _logger.LogInformation("[UploadProgramThumbnailAsync] Deleting old thumbnail for ProgramId: {ProgramId}", id);
            await _blobService.DeleteFileAsync(program.ThumbnailUrl);
        }

        var previewUrl = await UploadThumbnailFileAsync(id, file);

        program.ThumbnailUrl = previewUrl;
        await _unitOfWork.Programs.Update(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[UploadProgramThumbnailAsync] Thumbnail updated for ProgramId: {ProgramId}, Url: {ThumbnailUrl}",
            id,
            previewUrl);

        return new ProgramsResponseDto
        {
            Id = program.Id,
            Code = program.Code,
            Name = program.Name,
            SeriesName = program.SeriesName,
            Description = program.Description,
            Level = program.Level,
            Category = program.Category,
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
            FrameworkId = program.FrameworkId,
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
            Modules = program.Modules?.Select(m => new ModulesResponseDto
            {
                Id = m.Id,
                Code = m.Code,
                ProgramId = m.ProgramId,
                Name = m.Name,
                ModuleType = m.ModuleType,
                ModuleOrder = m.ModuleOrder,
                PrerequisiteModuleId = m.PrerequisiteModuleId,
                IsMandatory = m.IsMandatory,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
            }).ToList() ?? new(),
        };
    }

    private async Task<string> UploadThumbnailFileAsync(Guid programId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw ErrorHelper.BadRequest("Thumbnail file is required.");

        if (string.IsNullOrWhiteSpace(file.FileName))
            throw ErrorHelper.BadRequest("Thumbnail file name is invalid.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedThumbnailExtensions.Contains(extension))
            throw ErrorHelper.BadRequest("Only image files (.jpg, .jpeg, .png, .webp) are allowed for program thumbnail.");

        if (file.Length > MaxThumbnailSize)
            throw ErrorHelper.BadRequest("Thumbnail file size must not exceed 5 MB.");

        var fileName = $"{programId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension.ToLowerInvariant()}";
        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream, ThumbnailFolder);

        var s3Key = $"{ThumbnailFolder}/{fileName}";
        return await _blobService.GetPreviewUrlAsync(s3Key);
    }

    private async Task<Guid?> ResolveFrameworkIdAsync(Guid? frameworkId)
    {
        if (!frameworkId.HasValue)
        {
            return null;
        }

        if (frameworkId.Value == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("FrameworkId cannot be an empty guid.");
        }

        var framework = await _unitOfWork.ProgramFrameworks.GetByIdAsync(frameworkId.Value);
        if (framework == null || framework.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program framework with id '{frameworkId.Value}' not found.");
        }

        return framework.Id;
    }

    private async Task<bool> ApplyFrameworkAssignmentAsync(Program program, UpdateProgramRequestDto request)
    {
        if (request.FrameworkId.HasValue)
        {
            var resolved = await ResolveFrameworkIdAsync(request.FrameworkId);
            if (program.FrameworkId == resolved)
            {
                return false;
            }

            program.FrameworkId = resolved;
            return true;
        }

        if (request.ClearFramework == true)
        {
            if (!program.FrameworkId.HasValue)
            {
                return false;
            }

            program.FrameworkId = null;
            return true;
        }

        return false;
    }

    // =========================================================================
    // DELETE (Soft Delete)
    // =========================================================================

    public async Task<bool> DeleteProgramAsync(Guid id)
    {
        _logger.LogInformation("[DeleteProgramAsync] Attempting to soft-delete program Id: {Id}", id);

        var program = await _unitOfWork.Programs.GetByIdAsync(id);

        if (program == null || program.IsDeleted)
        {
            _logger.LogWarning("[DeleteProgramAsync] Program with Id {Id} not found.", id);
            throw ErrorHelper.NotFound($"Program with id '{id}' not found.");
        }

        await CurriculumEditGuard.EnsureProgramEditableAsync(_unitOfWork, id);

        await _unitOfWork.Programs.SoftRemove(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[DeleteProgramAsync] Program Id {Id} soft-deleted successfully.", id);

        return true;
    }

}
