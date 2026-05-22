using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CourseDTO;
using OboxSteam.Application.DTOs.ModuleDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
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

    public async Task<ModulesResponseDto> GetModuleByIdAsync(Guid id)
    {
        _logger.LogInformation("[GetModuleByIdAsync] Fetching module with Id: {Id}", id);

        var module = await _unitOfWork.Modules.GetByIdAsync(id, m => m.Courses);

        if (module == null || module.IsDeleted)
        {
            _logger.LogWarning("[GetModuleByIdAsync] Module with Id {Id} not found.", id);
            throw ErrorHelper.NotFound($"Module with id '{id}' not found.");
        }

        _logger.LogInformation("[GetModuleByIdAsync] Module with Id {Id} retrieved successfully.", id);
        return new ModulesResponseDto
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
            Courses = module.Courses?
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new CourseResponseDto
                {
                    Id = c.Id,
                    Code = c.Code,
                    ModuleId = c.ModuleId,
                    MentorId = c.MentorId,
                    Name = c.Name,
                    Description = c.Description,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                }).ToList() ?? new(),
        };
    }

    // =========================================================================
    // GET BY NAME
    // =========================================================================

    public async Task<ModulesResponseDto> GetModuleByNameAsync(string name)
    {
        _logger.LogInformation("[GetModuleByNameAsync] Fetching module with name: {Name}", name);

        var module = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Name.ToLower() == name.ToLower() && !m.IsDeleted,
            m => m.Courses);

        if (module == null)
        {
            _logger.LogWarning("[GetModuleByNameAsync] Module with name '{Name}' not found.", name);
            throw ErrorHelper.NotFound($"Module with name '{name}' not found.");
        }

        _logger.LogInformation("[GetModuleByNameAsync] Module '{Name}' retrieved successfully.", name);
        return new ModulesResponseDto
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
            Courses = module.Courses?
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new CourseResponseDto
                {
                    Id = c.Id,
                    Code = c.Code,
                    ModuleId = c.ModuleId,
                    MentorId = c.MentorId,
                    Name = c.Name,
                    Description = c.Description,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                }).ToList() ?? new(),
        };
    }

    // =========================================================================
    // GET ALL (PAGINATION + FILTER + SORT)
    // =========================================================================

    public async Task<Pagination<ModulesResponseDto>> GetAllModulesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code,
        ModuleType? moduleType)
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

        var moduleIds = items.Select(module => module.Id).ToList();

        var courses = await _unitOfWork.Courses.GetAllAsync(
            c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);

        var coursesByModuleId = courses
            .GroupBy(course => course.ModuleId)
            .ToDictionary(group => group.Key, group => group.OrderBy(c => c.Name).ToList());

        var dtos = items.Select(module => new ModulesResponseDto
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
            Courses = coursesByModuleId.TryGetValue(module.Id, out var moduleCourses)
                ? moduleCourses.Select(c => new CourseResponseDto
                {
                    Id = c.Id,
                    Code = c.Code,
                    ModuleId = c.ModuleId,
                    MentorId = c.MentorId,
                    Name = c.Name,
                    Description = c.Description,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                }).ToList()
                : new(),
        }).ToList();

        _logger.LogInformation("[GetAllModulesAsync] Retrieved {Count}/{Total} modules.", dtos.Count, totalCount);

        return new Pagination<ModulesResponseDto>(dtos, totalCount, page, pageSize);
    }

    // =========================================================================
    // CREATE
    // =========================================================================

    public async Task<ModulesResponseDto> CreateModuleAsync(CreateModuleRequestDto request)
    {
        _logger.LogInformation("[CreateModuleAsync] Start creating module: {Name} (Code: {Code})",
            request.Name, request.Code);

        var program = await _unitOfWork.Programs.GetByIdAsync(request.ProgramId);

        if (program == null || program.IsDeleted)
        {
            _logger.LogWarning("[CreateModuleAsync] Program with Id {Id} not found.", request.ProgramId);
            throw ErrorHelper.NotFound($"Program with id '{request.ProgramId}' not found.");
        }

        var existing = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code.ToLower() == request.Code.ToLower() && !m.IsDeleted);

        if (existing != null)
        {
            _logger.LogWarning("[CreateModuleAsync] Module with code '{Code}' already exists.", request.Code);
            throw ErrorHelper.Conflict($"Module with code '{request.Code}' already exists.");
        }

        if (request.PrerequisiteModuleId.HasValue)
        {
            var prerequisite = await _unitOfWork.Modules.GetByIdAsync(request.PrerequisiteModuleId.Value);

            if (prerequisite == null || prerequisite.IsDeleted)
            {
                throw ErrorHelper.NotFound($"Prerequisite module with id '{request.PrerequisiteModuleId}' not found.");
            }

            if (prerequisite.ProgramId != request.ProgramId)
            {
                throw ErrorHelper.BadRequest("Prerequisite module must belong to the same program.");
            }
        }

        var programModules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == request.ProgramId && !m.IsDeleted);
        var currentMaxOrder = programModules.Count == 0 ? 0 : programModules.Max(m => m.ModuleOrder);

        SequentialOrderValidator.ValidateMustExceedMax(
            request.ModuleOrder,
            currentMaxOrder,
            orderPropertyName: "ModuleOrder",
            scopeDescription: $"program '{request.ProgramId}'");

        var module = new Module
        {
            Code = request.Code,
            ProgramId = request.ProgramId,
            Name = request.Name,
            ModuleType = request.ModuleType,
            ModuleOrder = request.ModuleOrder,
            PrerequisiteModuleId = request.PrerequisiteModuleId,
            IsMandatory = request.IsMandatory,
            Price = request.Price,
            RetakeFee = request.RetakeFee,
        };

        await _unitOfWork.Modules.AddAsync(module);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[CreateModuleAsync] Module '{Code}' added successfully with Id {Id}.",
            module.Code, module.Id);

        return new ModulesResponseDto
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

    // =========================================================================
    // UPDATE
    // =========================================================================

    public async Task<ModulesResponseDto> UpdateModuleAsync(Guid id, UpdateModuleRequestDto request)
    {
        _logger.LogInformation("[UpdateModuleAsync] Attempting to update module with Id: {Id}", id);

        var module = await _unitOfWork.Modules.GetByIdAsync(id);

        if (module == null || module.IsDeleted)
        {
            _logger.LogWarning("[UpdateModuleAsync] Module with Id {Id} not found.", id);
            throw ErrorHelper.NotFound($"Module with id '{id}' not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Code) &&
            !module.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _unitOfWork.Modules.FirstOrDefaultAsync(
                m => m.Code.ToLower() == request.Code.ToLower() &&
                     !m.IsDeleted &&
                     m.Id != id);

            if (duplicate != null)
            {
                _logger.LogWarning("[UpdateModuleAsync] Code '{Code}' is already in use.", request.Code);
                throw ErrorHelper.Conflict($"Module with code '{request.Code}' already exists.");
            }
        }

        var isUpdated = false;

        if (request.ProgramId.HasValue && module.ProgramId != request.ProgramId.Value)
        {
            var program = await _unitOfWork.Programs.GetByIdAsync(request.ProgramId.Value);

            if (program == null || program.IsDeleted)
            {
                _logger.LogWarning("[UpdateModuleAsync] Program with Id {Id} not found.", request.ProgramId.Value);
                throw ErrorHelper.NotFound($"Program with id '{request.ProgramId}' not found.");
            }

            module.ProgramId = request.ProgramId.Value;
            isUpdated = true;
        }

        if (request.PrerequisiteModuleId.HasValue &&
            module.PrerequisiteModuleId != request.PrerequisiteModuleId)
        {
            if (request.PrerequisiteModuleId.Value == id)
            {
                throw ErrorHelper.BadRequest("Module cannot be its own prerequisite.");
            }

            var prerequisite = await _unitOfWork.Modules.GetByIdAsync(request.PrerequisiteModuleId.Value);

            if (prerequisite == null || prerequisite.IsDeleted)
            {
                throw ErrorHelper.NotFound($"Prerequisite module with id '{request.PrerequisiteModuleId}' not found.");
            }

            var targetProgramId = request.ProgramId ?? module.ProgramId;
            if (prerequisite.ProgramId != targetProgramId)
            {
                throw ErrorHelper.BadRequest("Prerequisite module must belong to the same program.");
            }

            module.PrerequisiteModuleId = request.PrerequisiteModuleId;
            isUpdated = true;
        }

        isUpdated = UpdateHelper.ApplyUpdates(module, request) || isUpdated;

        if (!isUpdated)
        {
            _logger.LogWarning("[UpdateModuleAsync] No changes detected for module Id: {Id}", id);
            return new ModulesResponseDto
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

        return new ModulesResponseDto
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

    // =========================================================================
    // DELETE (Soft Delete)
    // =========================================================================

    public async Task<bool> DeleteModuleAsync(Guid id)
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

}
