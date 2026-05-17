using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ModuleDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class ModuleService : IModuleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ModuleService> _logger;

    public ModuleService(IUnitOfWork unitOfWork, ILogger<ModuleService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }



    // =========================================================================
    // GET BY ID
    // =========================================================================

    public async Task<ModuleResponseDto> GetModuleByIdAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("[GetModuleByIdAsync] Fetching module with Id: {Id}", id);

            var module = await _unitOfWork.Modules.GetByIdAsync(id);

            if (module == null || module.IsDeleted)
            {
                _logger.LogWarning("[GetModuleByIdAsync] Module with Id {Id} not found.", id);
                throw ErrorHelper.NotFound($"Module with id '{id}' not found.");
            }

            _logger.LogInformation("[GetModuleByIdAsync] Module with Id {Id} retrieved successfully.", id);
            return new ModuleResponseDto
            {
                Id = module.Id,
                Code = module.Code,
                ProgramId = module.ProgramId,
                Name = module.Name,
                ModuleType = module.ModuleType,
                ModuleOrder = module.ModuleOrder,
                PrerequisiteModuleId = module.PrerequisiteModuleId,
                IsMandatory = module.IsMandatory,
                Price = module.Price,
                RetakeFee = module.RetakeFee,
                CreatedAt = module.CreatedAt,
                UpdatedAt = module.UpdatedAt,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("[GetModuleByIdAsync] Error fetching module with Id {Id}. Exception: {Message}", id, ex.Message);
            throw;
        }
    }

    // =========================================================================
    // GET BY NAME
    // =========================================================================

    public async Task<ModuleResponseDto> GetModuleByNameAsync(string name)
    {
        try
        {
            _logger.LogInformation("[GetModuleByNameAsync] Fetching module with name: {Name}", name);

            var module = await _unitOfWork.Modules.FirstOrDefaultAsync(
                m => m.Name.ToLower() == name.ToLower() && !m.IsDeleted);

            if (module == null)
            {
                _logger.LogWarning("[GetModuleByNameAsync] Module with name '{Name}' not found.", name);
                throw ErrorHelper.NotFound($"Module with name '{name}' not found.");
            }

            _logger.LogInformation("[GetModuleByNameAsync] Module '{Name}' retrieved successfully.", name);
            return new ModuleResponseDto
            {
                Id = module.Id,
                Code = module.Code,
                ProgramId = module.ProgramId,
                Name = module.Name,
                ModuleType = module.ModuleType,
                ModuleOrder = module.ModuleOrder,
                PrerequisiteModuleId = module.PrerequisiteModuleId,
                IsMandatory = module.IsMandatory,
                Price = module.Price,
                RetakeFee = module.RetakeFee,
                CreatedAt = module.CreatedAt,
                UpdatedAt = module.UpdatedAt,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("[GetModuleByNameAsync] Error fetching module with name '{Name}'. Exception: {Message}", name, ex.Message);
            throw;
        }
    }

    // =========================================================================
    // GET ALL (PAGINATION + FILTER + SORT)
    // =========================================================================

    public async Task<Pagination<ModuleResponseDto>> GetAllModulesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code,
        ModuleType? moduleType)
    {
        try
        {
            _logger.LogInformation(
                "[GetAllModulesAsync] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
                page, pageSize, search);

            var query = _unitOfWork.Modules
                .GetQueryable()
                .Where(m => !m.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(m =>
                    m.Name.ToLower().Contains(lowerSearch) ||
                    m.Code.ToLower().Contains(lowerSearch));
            }

            if (!string.IsNullOrWhiteSpace(code))
            {
                query = query.Where(m => m.Code.ToLower().Contains(code.ToLower()));
            }

            if (moduleType.HasValue)
            {
                query = query.Where(m => m.ModuleType == moduleType.Value);
            }

            query = sortBy?.ToLower() switch
            {
                "name" => isDescending ? query.OrderByDescending(m => m.Name) : query.OrderBy(m => m.Name),
                "code" => isDescending ? query.OrderByDescending(m => m.Code) : query.OrderBy(m => m.Code),
                "moduleorder" => isDescending ? query.OrderByDescending(m => m.ModuleOrder) : query.OrderBy(m => m.ModuleOrder),
                "moduletype" => isDescending ? query.OrderByDescending(m => m.ModuleType) : query.OrderBy(m => m.ModuleType),
                "price" => isDescending ? query.OrderByDescending(m => m.Price) : query.OrderBy(m => m.Price),
                "createdat" => isDescending ? query.OrderByDescending(m => m.CreatedAt) : query.OrderBy(m => m.CreatedAt),
                _ => isDescending ? query.OrderByDescending(m => m.CreatedAt) : query.OrderBy(m => m.CreatedAt),
            };

            var totalCount = query.Count();

            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var dtos = items.Select(module => new ModuleResponseDto
            {
                Id = module.Id,
                Code = module.Code,
                ProgramId = module.ProgramId,
                Name = module.Name,
                ModuleType = module.ModuleType,
                ModuleOrder = module.ModuleOrder,
                PrerequisiteModuleId = module.PrerequisiteModuleId,
                IsMandatory = module.IsMandatory,
                Price = module.Price,
                RetakeFee = module.RetakeFee,
                CreatedAt = module.CreatedAt,
                UpdatedAt = module.UpdatedAt,
            }).ToList();

            _logger.LogInformation("[GetAllModulesAsync] Retrieved {Count}/{Total} modules.", dtos.Count, totalCount);

            return new Pagination<ModuleResponseDto>(dtos, totalCount, page, pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError("[GetAllModulesAsync] Failed to retrieve modules. Exception: {Message}", ex.Message);
            throw new Exception("An error occurred while retrieving modules. Please try again later.");
        }
    }

    // =========================================================================
    // ADD
    // =========================================================================

    public async Task<ModuleResponseDto> AddModuleAsync(ModuleCreateDto moduleCreateDto)
    {
        _logger.LogInformation("[AddModuleAsync] Start adding module: {Name} (Code: {Code})",
            moduleCreateDto.Name, moduleCreateDto.Code);

        try
        {
            var program = await _unitOfWork.Programs.GetByIdAsync(moduleCreateDto.ProgramId);

            if (program == null || program.IsDeleted)
            {
                _logger.LogWarning("[AddModuleAsync] Program with Id {Id} not found.", moduleCreateDto.ProgramId);
                throw ErrorHelper.NotFound($"Program with id '{moduleCreateDto.ProgramId}' not found.");
            }

            var existing = await _unitOfWork.Modules.FirstOrDefaultAsync(
                m => m.Code.ToLower() == moduleCreateDto.Code.ToLower() && !m.IsDeleted);

            if (existing != null)
            {
                _logger.LogWarning("[AddModuleAsync] Module with code '{Code}' already exists.", moduleCreateDto.Code);
                throw ErrorHelper.Conflict($"Module with code '{moduleCreateDto.Code}' already exists.");
            }

            if (moduleCreateDto.PrerequisiteModuleId.HasValue)
            {
                var prerequisite = await _unitOfWork.Modules.GetByIdAsync(moduleCreateDto.PrerequisiteModuleId.Value);

                if (prerequisite == null || prerequisite.IsDeleted)
                {
                    throw ErrorHelper.NotFound($"Prerequisite module with id '{moduleCreateDto.PrerequisiteModuleId}' not found.");
                }

                if (prerequisite.ProgramId != moduleCreateDto.ProgramId)
                {
                    throw ErrorHelper.BadRequest("Prerequisite module must belong to the same program.");
                }
            }

            var module = new Module
            {
                Code = moduleCreateDto.Code,
                ProgramId = moduleCreateDto.ProgramId,
                Name = moduleCreateDto.Name,
                ModuleType = moduleCreateDto.ModuleType,
                ModuleOrder = moduleCreateDto.ModuleOrder,
                PrerequisiteModuleId = moduleCreateDto.PrerequisiteModuleId,
                IsMandatory = moduleCreateDto.IsMandatory,
                Price = moduleCreateDto.Price,
                RetakeFee = moduleCreateDto.RetakeFee,
            };

            await _unitOfWork.Modules.AddAsync(module);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("[AddModuleAsync] Module '{Code}' added successfully with Id {Id}.",
                module.Code, module.Id);

            return new ModuleResponseDto
            {
                Id = module.Id,
                Code = module.Code,
                ProgramId = module.ProgramId,
                Name = module.Name,
                ModuleType = module.ModuleType,
                ModuleOrder = module.ModuleOrder,
                PrerequisiteModuleId = module.PrerequisiteModuleId,
                IsMandatory = module.IsMandatory,
                Price = module.Price,
                RetakeFee = module.RetakeFee,
                CreatedAt = module.CreatedAt,
                UpdatedAt = module.UpdatedAt,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("[AddModuleAsync] Error adding module '{Name}'. Exception: {Message}",
                moduleCreateDto.Name, ex.Message);
            throw;
        }
    }

    // =========================================================================
    // UPDATE
    // =========================================================================

    public async Task<ModuleResponseDto> UpdateModuleAsync(Guid id, ModuleUpdateDto moduleUpdateDto)
    {
        try
        {
            _logger.LogInformation("[UpdateModuleAsync] Attempting to update module with Id: {Id}", id);

            var module = await _unitOfWork.Modules.GetByIdAsync(id);

            if (module == null || module.IsDeleted)
            {
                _logger.LogWarning("[UpdateModuleAsync] Module with Id {Id} not found.", id);
                throw ErrorHelper.NotFound($"Module with id '{id}' not found.");
            }

            if (!string.IsNullOrWhiteSpace(moduleUpdateDto.Code) &&
                !module.Code.Equals(moduleUpdateDto.Code, StringComparison.OrdinalIgnoreCase))
            {
                var duplicate = await _unitOfWork.Modules.FirstOrDefaultAsync(
                    m => m.Code.ToLower() == moduleUpdateDto.Code.ToLower() &&
                         !m.IsDeleted &&
                         m.Id != id);

                if (duplicate != null)
                {
                    _logger.LogWarning("[UpdateModuleAsync] Code '{Code}' is already in use.", moduleUpdateDto.Code);
                    throw ErrorHelper.Conflict($"Module with code '{moduleUpdateDto.Code}' already exists.");
                }
            }

            var isUpdated = false;

            if (moduleUpdateDto.ProgramId.HasValue && module.ProgramId != moduleUpdateDto.ProgramId.Value)
            {
                var program = await _unitOfWork.Programs.GetByIdAsync(moduleUpdateDto.ProgramId.Value);

                if (program == null || program.IsDeleted)
                {
                    _logger.LogWarning("[UpdateModuleAsync] Program with Id {Id} not found.", moduleUpdateDto.ProgramId.Value);
                    throw ErrorHelper.NotFound($"Program with id '{moduleUpdateDto.ProgramId}' not found.");
                }

                module.ProgramId = moduleUpdateDto.ProgramId.Value;
                isUpdated = true;
            }

            if (moduleUpdateDto.PrerequisiteModuleId.HasValue &&
                module.PrerequisiteModuleId != moduleUpdateDto.PrerequisiteModuleId)
            {
                if (moduleUpdateDto.PrerequisiteModuleId.Value == id)
                {
                    throw ErrorHelper.BadRequest("Module cannot be its own prerequisite.");
                }

                var prerequisite = await _unitOfWork.Modules.GetByIdAsync(moduleUpdateDto.PrerequisiteModuleId.Value);

                if (prerequisite == null || prerequisite.IsDeleted)
                {
                    throw ErrorHelper.NotFound($"Prerequisite module with id '{moduleUpdateDto.PrerequisiteModuleId}' not found.");
                }

                var targetProgramId = moduleUpdateDto.ProgramId ?? module.ProgramId;
                if (prerequisite.ProgramId != targetProgramId)
                {
                    throw ErrorHelper.BadRequest("Prerequisite module must belong to the same program.");
                }

                module.PrerequisiteModuleId = moduleUpdateDto.PrerequisiteModuleId;
                isUpdated = true;
            }

            isUpdated = UpdateHelper.ApplyUpdates(module, moduleUpdateDto) || isUpdated;

            if (!isUpdated)
            {
                _logger.LogWarning("[UpdateModuleAsync] No changes detected for module Id: {Id}", id);
                return new ModuleResponseDto
                {
                    Id = module.Id,
                    Code = module.Code,
                    ProgramId = module.ProgramId,
                    Name = module.Name,
                    ModuleType = module.ModuleType,
                    ModuleOrder = module.ModuleOrder,
                    PrerequisiteModuleId = module.PrerequisiteModuleId,
                    IsMandatory = module.IsMandatory,
                    Price = module.Price,
                    RetakeFee = module.RetakeFee,
                    CreatedAt = module.CreatedAt,
                    UpdatedAt = module.UpdatedAt,
                };
            }

            await _unitOfWork.Modules.Update(module);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("[UpdateModuleAsync] Module Id {Id} updated successfully.", id);

            return new ModuleResponseDto
            {
                Id = module.Id,
                Code = module.Code,
                ProgramId = module.ProgramId,
                Name = module.Name,
                ModuleType = module.ModuleType,
                ModuleOrder = module.ModuleOrder,
                PrerequisiteModuleId = module.PrerequisiteModuleId,
                IsMandatory = module.IsMandatory,
                Price = module.Price,
                RetakeFee = module.RetakeFee,
                CreatedAt = module.CreatedAt,
                UpdatedAt = module.UpdatedAt,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("[UpdateModuleAsync] Error updating module Id {Id}. Exception: {Message}", id, ex.Message);
            throw;
        }
    }

    // =========================================================================
    // DELETE (Soft Delete)
    // =========================================================================

    public async Task<bool> DeleteModuleAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("[DeleteModuleAsync] Attempting to soft-delete module Id: {Id}", id);

            var module = await _unitOfWork.Modules.GetByIdAsync(id);

            if (module == null || module.IsDeleted)
            {
                _logger.LogWarning("[DeleteModuleAsync] Module with Id {Id} not found.", id);
                throw ErrorHelper.NotFound($"Module with id '{id}' not found.");
            }

            await _unitOfWork.Modules.SoftRemove(module);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("[DeleteModuleAsync] Module Id {Id} soft-deleted successfully.", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("[DeleteModuleAsync] Error deleting module Id {Id}. Exception: {Message}", id, ex.Message);
            throw;
        }
    }

}
