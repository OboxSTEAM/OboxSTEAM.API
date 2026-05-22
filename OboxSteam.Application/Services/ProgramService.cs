
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ModuleDTO;
using OboxSteam.Application.DTOs.ProgramDTO;

using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class ProgramService : IProgramService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProgramService> _logger;

    public ProgramService(IUnitOfWork unitOfWork, ILogger<ProgramService> logger)
    {
        _unitOfWork = unitOfWork;
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

        _logger.LogInformation("[GetProgramByIdAsync] Program with Id {Id} retrieved successfully.", id);
        return new ProgramsResponseDto
        {
            Id = program.Id,
            Code = program.Code,
            Name = program.Name,
            SeriesName = program.SeriesName,
            Description = program.Description,
            Level = program.Level,
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
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
                Price = m.Price,
                RetakeFee = m.RetakeFee,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
            }).ToList() ?? new(),
        };
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
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
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
                Price = m.Price,
                RetakeFee = m.RetakeFee,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
            }).ToList() ?? new(),
        };
    }

    // =========================================================================
    // GET ALL (PAGINATION + FILTER + SORT)
    // =========================================================================

    public async Task<Pagination<ProgramsResponseDto>> GetAllProgramAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code = null,
        DifficultyLevel? level = null,
        decimal? rating = null,
        string? skillsGained = null,
        string? status = null)
    {
        _logger.LogInformation(
            "[GetAllProgramAsync] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
            page, pageSize, search);

        var query = _unitOfWork.Programs
            .GetQueryable()
            .Where(p => !p.IsDeleted);

        // ── Filters ───────────────────────────────────────────────────────
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

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p =>
                p.Status != null &&
                p.Status.ToLower() == status.ToLower());

        // ── Sorting ───────────────────────────────────────────────────────
        query = sortBy?.ToLower() switch
        {
            "name" => isDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "code" => isDescending ? query.OrderByDescending(p => p.Code) : query.OrderBy(p => p.Code),
            "level" => isDescending ? query.OrderByDescending(p => p.Level) : query.OrderBy(p => p.Level),
            "rating" => isDescending ? query.OrderByDescending(p => p.Rating) : query.OrderBy(p => p.Rating),
            "price" => isDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "createdat" => isDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            _ => isDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
        };

        // ── Pagination ────────────────────────────────────────────────────
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
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
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
                Price = m.Price,
                RetakeFee = m.RetakeFee,
                CreatedAt = m.CreatedAt,
                 UpdatedAt = m.UpdatedAt,
             }).ToList()
                 : new(),
        }).ToList();

        _logger.LogInformation("[GetAllProgramAsync] Retrieved {Count}/{Total} programs.", dtos.Count, totalCount);

        return new Pagination<ProgramsResponseDto>(dtos, totalCount, page, pageSize);
    }

    // =========================================================================
    // CREATE
    // =========================================================================


    public async Task<ProgramsResponseDto> CreateProgramAsync(CreateProgramRequestDto request)
    {
        _logger.LogInformation("[CreateProgramAsync] Start creating program: {Name} (Code: {Code})",
            request.Name, request.Code);

        // Kiểm tra trùng Code
        var existing = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code.ToLower() == request.Code.ToLower() && !p.IsDeleted);

        if (existing != null)

            _logger.LogWarning("[CreateProgramAsync] Program with code '{Code}' already exists.", request.Code);
            throw ErrorHelper.Conflict($"Program with code '{request.Code}' already exists.");
        }

        var program = new Program
        {
            Code = request.Code,
            Name = request.Name,
            SeriesName = request.SeriesName,
            Description = request.Description,
            Level = request.Level,
            EstimatedDuration = request.EstimatedDuration,
            SkillsGained = request.SkillsGained,
            ThumbnailUrl = request.ThumbnailUrl,
            Status = request.Status,
            Price = request.Price,
        };

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
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
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

        var isUpdated = UpdateHelper.ApplyUpdates(program, request);

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
                EstimatedDuration = program.EstimatedDuration,
                SkillsGained = program.SkillsGained,
                Rating = program.Rating,
                TotalReviews = program.TotalReviews,
                ThumbnailUrl = program.ThumbnailUrl,
                Status = program.Status,
                Price = program.Price,
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
                    Price = m.Price,
                    RetakeFee = m.RetakeFee,
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
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            Status = program.Status,
            Price = program.Price,
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
                Price = m.Price,
                RetakeFee = m.RetakeFee,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
            }).ToList() ?? new(),
        };
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

        await _unitOfWork.Programs.SoftRemove(program);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[DeleteProgramAsync] Program Id {Id} soft-deleted successfully.", id);

        return true;
    }

}
