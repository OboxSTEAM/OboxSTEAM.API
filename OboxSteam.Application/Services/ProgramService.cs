
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

    public async Task<ProgramResponseDto> GetProgramByIdAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("[GetProgramByIdAsync] Fetching program with Id: {Id}", id);

            var program = await _unitOfWork.Programs.GetByIdAsync(id, p => p.Modules);

            if (program == null || program.IsDeleted)
            {
                _logger.LogWarning("[GetProgramByIdAsync] Program with Id {Id} not found.", id);
                throw ErrorHelper.NotFound($"Program with id '{id}' not found.");
            }

            _logger.LogInformation("[GetProgramByIdAsync] Program with Id {Id} retrieved successfully.", id);
            return new ProgramResponseDto
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
                Modules = program.Modules?.Select(m => new ModuleResponseDto
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
        catch (Exception ex)
        {
            _logger.LogError("[GetProgramByIdAsync] Error fetching program with Id {Id}. Exception: {Message}", id, ex.Message);
            throw;
        }
    }

    // =========================================================================
    // GET BY NAME
    // =========================================================================

    public async Task<ProgramResponseDto> GetProgramByNameAsync(string name)
    {
        try
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
            return new ProgramResponseDto
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
                Modules = program.Modules?.Select(m => new ModuleResponseDto
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
        catch (Exception ex)
        {
            _logger.LogError("[GetProgramByNameAsync] Error fetching program with name '{Name}'. Exception: {Message}", name, ex.Message);
            throw;
        }
    }

    // =========================================================================
    // GET ALL (PAGINATION + FILTER + SORT)
    // =========================================================================

    public async Task<Pagination<ProgramResponseDto>> GetAllProgramAsync(
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
        try
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
                .ToDictionary(group => group.Key, group => group.ToList());

            var dtos = items.Select(program => new ProgramResponseDto
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
                     ? programModules.Select(m => new ModuleResponseDto
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

            return new Pagination<ProgramResponseDto>(dtos, totalCount, page, pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError("[GetAllProgramAsync] Failed to retrieve programs. Exception: {Message}", ex.Message);
            throw new Exception("An error occurred while retrieving programs. Please try again later.");
        }
    }

    // =========================================================================
    // ADD
    // =========================================================================

    public async Task<ProgramResponseDto> AddProgramAsync(ProgramCreateDto programCreateDto)
    {
        _logger.LogInformation("[AddProgramAsync] Start adding program: {Name} (Code: {Code})",
            programCreateDto.Name, programCreateDto.Code);
        try
        {
            // Kiểm tra trùng Code
            var existing = await _unitOfWork.Programs.FirstOrDefaultAsync(
                p => p.Code.ToLower() == programCreateDto.Code.ToLower() && !p.IsDeleted);

            if (existing != null)
            {
                _logger.LogWarning("[AddProgramAsync] Program with code '{Code}' already exists.", programCreateDto.Code);
                throw ErrorHelper.Conflict($"Program with code '{programCreateDto.Code}' already exists.");
            }

            var program = new Program
            {
                Code = programCreateDto.Code,
                Name = programCreateDto.Name,
                SeriesName = programCreateDto.SeriesName,
                Description = programCreateDto.Description,
                Level = programCreateDto.Level,
                EstimatedDuration = programCreateDto.EstimatedDuration,
                SkillsGained = programCreateDto.SkillsGained,
                ThumbnailUrl = programCreateDto.ThumbnailUrl,
                Status = programCreateDto.Status,
                Price = programCreateDto.Price,
            };

            await _unitOfWork.Programs.AddAsync(program);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("[AddProgramAsync] Program '{Code}' added successfully with Id {Id}.",
                program.Code, program.Id);

            return new ProgramResponseDto
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
        catch (Exception ex)
        {
            _logger.LogError("[AddProgramAsync] Error adding program '{Name}'. Exception: {Message}",
                programCreateDto.Name, ex.Message);
            throw;
        }
    }

    // =========================================================================
    // UPDATE
    // =========================================================================

    public async Task<ProgramResponseDto> UpdateProgramAsync(Guid id, ProgramUpdateDto programUpdateDto)
    {
        try
        {
            _logger.LogInformation("[UpdateProgramAsync] Attempting to update program with Id: {Id}", id);

            var program = await _unitOfWork.Programs.GetByIdAsync(id, p => p.Modules);

            if (program == null || program.IsDeleted)
            {
                _logger.LogWarning("[UpdateProgramAsync] Program with Id {Id} not found.", id);
                throw ErrorHelper.NotFound($"Program with id '{id}' not found.");
            }

            // Kiểm tra trùng Code khi đổi Code
            if (!string.IsNullOrWhiteSpace(programUpdateDto.Code) &&
                !program.Code.Equals(programUpdateDto.Code, StringComparison.OrdinalIgnoreCase))
            {
                var duplicate = await _unitOfWork.Programs.FirstOrDefaultAsync(
                    p => p.Code.ToLower() == programUpdateDto.Code.ToLower() &&
                         !p.IsDeleted &&
                         p.Id != id);

                if (duplicate != null)
                {
                    _logger.LogWarning("[UpdateProgramAsync] Code '{Code}' is already in use.", programUpdateDto.Code);
                    throw ErrorHelper.Conflict($"Program with code '{programUpdateDto.Code}' already exists.");
                }
            }

            bool isUpdated = false;

            if (!string.IsNullOrWhiteSpace(programUpdateDto.Code) && program.Code != programUpdateDto.Code)
            {
                program.Code = programUpdateDto.Code;
                isUpdated = true;
            }

            if (!string.IsNullOrWhiteSpace(programUpdateDto.Name) && program.Name != programUpdateDto.Name)
            {
                program.Name = programUpdateDto.Name;
                isUpdated = true;
            }

            if (programUpdateDto.SeriesName != null && program.SeriesName != programUpdateDto.SeriesName)
            {
                program.SeriesName = programUpdateDto.SeriesName;
                isUpdated = true;
            }

            if (programUpdateDto.Description != null && program.Description != programUpdateDto.Description)
            {
                program.Description = programUpdateDto.Description;
                isUpdated = true;
            }

            if (programUpdateDto.Level.HasValue && program.Level != programUpdateDto.Level.Value)
            {
                program.Level = programUpdateDto.Level.Value;
                isUpdated = true;
            }

            if (programUpdateDto.EstimatedDuration != null && program.EstimatedDuration != programUpdateDto.EstimatedDuration)
            {
                program.EstimatedDuration = programUpdateDto.EstimatedDuration;
                isUpdated = true;
            }

            if (programUpdateDto.SkillsGained != null && program.SkillsGained != programUpdateDto.SkillsGained)
            {
                program.SkillsGained = programUpdateDto.SkillsGained;
                isUpdated = true;
            }

            if (programUpdateDto.ThumbnailUrl != null && program.ThumbnailUrl != programUpdateDto.ThumbnailUrl)
            {
                program.ThumbnailUrl = programUpdateDto.ThumbnailUrl;
                isUpdated = true;
            }

            if (programUpdateDto.Status != null && program.Status != programUpdateDto.Status)
            {
                program.Status = programUpdateDto.Status;
                isUpdated = true;
            }

            if (programUpdateDto.Price.HasValue && program.Price != programUpdateDto.Price)
            {
                program.Price = programUpdateDto.Price;
                isUpdated = true;
            }

            if (!isUpdated)
            {
                _logger.LogWarning("[UpdateProgramAsync] No changes detected for program Id: {Id}", id);
                return new ProgramResponseDto
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
                    Modules = program.Modules?.Select(m => new ModuleResponseDto
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

            return new ProgramResponseDto
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
                Modules = program.Modules?.Select(m => new ModuleResponseDto
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
        catch (Exception ex)
        {
            _logger.LogError("[UpdateProgramAsync] Error updating program Id {Id}. Exception: {Message}", id, ex.Message);
            throw;
        }
    }

    // =========================================================================
    // DELETE (Soft Delete)
    // =========================================================================

    public async Task<bool> DeleteProgramAsync(Guid id)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError("[DeleteProgramAsync] Error deleting program Id {Id}. Exception: {Message}", id, ex.Message);
            throw;
        }
    }

}
