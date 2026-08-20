using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ActivityDTO;
using OboxSteam.Application.DTOs.CourseDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Realtime;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class CourseService : ICourseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CourseService> _logger;
    private readonly ISyncEventPublisher _syncEventPublisher;

    public CourseService(
        IUnitOfWork unitOfWork,
        ILogger<CourseService> logger,
        ISyncEventPublisher syncEventPublisher)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _syncEventPublisher = syncEventPublisher;
    }

    private Task PublishCurriculumStructureChangedAsync(Guid programId)
        => _syncEventPublisher.PublishAsync(
            SyncScopes.CurriculumStructureChanged,
            NotificationAudience.ForProgramParticipants(programId),
            entityType: "Program",
            entityId: programId);

    private async Task PublishCurriculumStructureChangedForModuleAsync(Guid moduleId)
    {
        var module = await _unitOfWork.Modules.GetByIdAsync(moduleId);
        if (module is null || module.IsDeleted)
        {
            return;
        }

        await PublishCurriculumStructureChangedAsync(module.ProgramId);
    }

    // =========================================================================
    // GET ALL (PAGINATION + FILTER + SORT)
    // =========================================================================

    public async Task<Pagination<CourseResponseDto>> GetAllCoursesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code,
        string? moduleName)
    {
        _logger.LogInformation(
            "[GetAllCoursesAsync] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
            page, pageSize, search);

        var query = _unitOfWork.Courses
            .GetQueryable()
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(lowerSearch) ||
                c.Code.ToLower().Contains(lowerSearch));
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            query = query.Where(c => c.Code.ToLower().Contains(code.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            var lowerModuleName = moduleName.ToLower();
            query = query.Where(c => c.Module.Name.ToLower().Contains(lowerModuleName));
        }

        query = sortBy?.ToLower() switch
        {
            "name" => isDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            "code" => isDescending ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code),
            "moduleid" => isDescending ? query.OrderByDescending(c => c.ModuleId) : query.OrderBy(c => c.ModuleId),
            "courseorder" => isDescending ? query.OrderByDescending(c => c.CourseOrder) : query.OrderBy(c => c.CourseOrder),
            "createdat" => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
            _ => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = items.Select(c => new CourseResponseDto
        {
            Id = c.Id,
            Code = c.Code,
            ModuleId = c.ModuleId,
            Name = c.Name,
            Description = c.Description,
            CourseOrder = c.CourseOrder,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
        }).ToList();

        _logger.LogInformation("[GetAllCoursesAsync] Retrieved {Count}/{Total} courses.", dtos.Count, totalCount);

        return new Pagination<CourseResponseDto>(dtos, totalCount, page, pageSize);
    }

    // =========================================================================
    // GET BY ID
    // =========================================================================

    public async Task<CourseResponseDto?> GetCourseByIdAsync(Guid courseId)
    {
        _logger.LogInformation("[GetCourseByIdAsync] Fetching course with Id: {Id}", courseId);

        var course = await _unitOfWork.Courses.GetByIdAsync(courseId, c => c.Activities);

        if (course == null || course.IsDeleted)
        {
            _logger.LogWarning("[GetCourseByIdAsync] Course with Id {Id} not found.", courseId);
            return null;
        }

        _logger.LogInformation("[GetCourseByIdAsync] Course with Id {Id} retrieved successfully.", courseId);
        return new CourseResponseDto
        {
            Id = course.Id,
            Code = course.Code,
            ModuleId = course.ModuleId,
            Name = course.Name,
            Description = course.Description,
            CourseOrder = course.CourseOrder,
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt,
            Activities = course.Activities?
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.ActivityOrder)
                .Select(a => new ActivitiesResponseDto
                {
                    Id = a.Id,
                    Code = a.Code,
                    CourseId = a.CourseId,
                    Name = a.Name,
                    ActivityType = a.ActivityType,
                    Description = a.Description,
                    ActivityOrder = a.ActivityOrder,
                    DurationMinutes = a.DurationMinutes,
                    RequireQrCheckin = a.RequireQrCheckin,
                    RequireMediaEvidence = a.RequireMediaEvidence,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                }).ToList() ?? new(),
        };
    }

    // =========================================================================
    // GET BY NAME
    // =========================================================================

    public async Task<CourseResponseDto?> GetCourseByNameAsync(string? courseName)
    {
        if (string.IsNullOrWhiteSpace(courseName))
        {
            throw new BadRequestException("Course name is required.");
        }

        _logger.LogInformation("[GetCourseByNameAsync] Fetching course with name: {Name}", courseName);

        var course = await _unitOfWork.Courses.FirstOrDefaultAsync(
            c => c.Name.ToLower() == courseName.ToLower() && !c.IsDeleted,
            c => c.Activities);

        if (course == null)
        {
            _logger.LogWarning("[GetCourseByNameAsync] Course with name '{Name}' not found.", courseName);
            return null;
        }

        _logger.LogInformation("[GetCourseByNameAsync] Course '{Name}' retrieved successfully.", courseName);
        return new CourseResponseDto
        {
            Id = course.Id,
            Code = course.Code,
            ModuleId = course.ModuleId,
            Name = course.Name,
            Description = course.Description,
            CourseOrder = course.CourseOrder,
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt,
            Activities = course.Activities?
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.ActivityOrder)
                .Select(a => new ActivitiesResponseDto
                {
                    Id = a.Id,
                    Code = a.Code,
                    CourseId = a.CourseId,
                    Name = a.Name,
                    ActivityType = a.ActivityType,
                    Description = a.Description,
                    ActivityOrder = a.ActivityOrder,
                    DurationMinutes = a.DurationMinutes,
                    RequireQrCheckin = a.RequireQrCheckin,
                    RequireMediaEvidence = a.RequireMediaEvidence,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                }).ToList() ?? new(),
        };
    }

    // =========================================================================
    // CREATE
    // =========================================================================

    public async Task<CourseResponseDto> CreateCourseAsync(CreateCourseRequestDto request)
    {
        _logger.LogInformation("[CreateCourseAsync] Start creating course: {Name} (Code: {Code})",
            request.Name, request.Code);

        var module = await _unitOfWork.Modules.GetByIdAsync(request.ModuleId);

        if (module == null || module.IsDeleted)
        {
            _logger.LogWarning("[CreateCourseAsync] Module with Id {Id} not found.", request.ModuleId);
            throw new NotFoundException($"Module with id '{request.ModuleId}' not found.");
        }

        var existing = await _unitOfWork.Courses.FirstOrDefaultAsync(
            c => c.Code.ToLower() == request.Code.ToLower() && !c.IsDeleted);

        if (existing != null)
        {
            _logger.LogWarning("[CreateCourseAsync] Course with code '{Code}' already exists.", request.Code);
            throw new ConflictException($"Course with code '{request.Code}' already exists.");
        }

        await CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_unitOfWork, module.ProgramId);

        var moduleCourses = await _unitOfWork.Courses.GetAllAsync(
            c => c.ModuleId == request.ModuleId && !c.IsDeleted);

        SequentialOrderValidator.ValidateWithinRange(
            request.CourseOrder,
            minOrder: 1,
            maxOrder: moduleCourses.Count + 1,
            orderPropertyName: "CourseOrder",
            scopeDescription: $"module '{request.ModuleId}'");

        // Insert-in-the-middle: shift existing courses at or after the requested slot.
        var coursesToShift = moduleCourses
            .Where(c => c.CourseOrder >= request.CourseOrder)
            .ToList();

        foreach (var existingCourse in coursesToShift)
        {
            existingCourse.CourseOrder += 1;
        }

        if (coursesToShift.Count > 0)
        {
            await _unitOfWork.Courses.UpdateRange(coursesToShift);
        }

        var course = new Course
        {
            Code = request.Code,
            ModuleId = request.ModuleId,
            Name = request.Name,
            Description = request.Description,
            CourseOrder = request.CourseOrder,
        };

        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();

        await PublishCurriculumStructureChangedAsync(module.ProgramId);

        _logger.LogInformation("[CreateCourseAsync] Course '{Code}' created successfully with Id {Id}.",
            course.Code, course.Id);

        return new CourseResponseDto
        {
            Id = course.Id,
            Code = course.Code,
            ModuleId = course.ModuleId,
            Name = course.Name,
            Description = course.Description,
            CourseOrder = course.CourseOrder,
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt,
        };
    }

    // =========================================================================
    // UPDATE
    // =========================================================================

    public async Task<CourseResponseDto?> UpdateCourseAsync(Guid courseId, UpdateCourseRequestDto request)
    {
        _logger.LogInformation("[UpdateCourseAsync] Attempting to update course with Id: {Id}", courseId);

        var course = await _unitOfWork.Courses.GetByIdAsync(courseId);

        if (course == null || course.IsDeleted)
        {
            _logger.LogWarning("[UpdateCourseAsync] Course with Id {Id} not found.", courseId);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Code) &&
            !course.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _unitOfWork.Courses.FirstOrDefaultAsync(
                c => c.Code.ToLower() == request.Code.ToLower() &&
                     !c.IsDeleted &&
                     c.Id != courseId);

            if (duplicate != null)
            {
                _logger.LogWarning("[UpdateCourseAsync] Code '{Code}' is already in use.", request.Code);
                throw new ConflictException($"Course with code '{request.Code}' already exists.");
            }

            course.Code = request.Code;
        }

        var oldModuleId = course.ModuleId;
        var oldOrder = course.CourseOrder;
        var moduleChanged = request.ModuleId.HasValue && request.ModuleId.Value != oldModuleId;
        var targetModuleId = moduleChanged ? request.ModuleId!.Value : oldModuleId;

        var currentModule = await _unitOfWork.Modules.GetByIdAsync(oldModuleId);
        if (currentModule != null && !currentModule.IsDeleted)
        {
            await CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_unitOfWork, currentModule.ProgramId);
        }

        Module? targetModule = null;
        if (moduleChanged)
        {
            targetModule = await _unitOfWork.Modules.GetByIdAsync(targetModuleId);

            if (targetModule == null || targetModule.IsDeleted)
            {
                _logger.LogWarning("[UpdateCourseAsync] Module with Id {Id} not found.", targetModuleId);
                throw new NotFoundException($"Module with id '{request.ModuleId}' not found.");
            }

            if (currentModule == null || targetModule.ProgramId != currentModule.ProgramId)
            {
                await CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_unitOfWork, targetModule.ProgramId);
            }
        }

        if (moduleChanged)
        {
            await MoveCourseToModuleAsync(course, oldModuleId, oldOrder, targetModuleId, request.CourseOrder);
        }
        else if (request.CourseOrder.HasValue && request.CourseOrder.Value != oldOrder)
        {
            await ReorderWithinModuleAsync(course, targetModuleId, oldOrder, request.CourseOrder.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && course.Name != request.Name)
        {
            course.Name = request.Name;
        }

        if (request.Description != null && course.Description != request.Description)
        {
            course.Description = request.Description;
        }

        await _unitOfWork.Courses.Update(course);
        await _unitOfWork.SaveChangesAsync();

        await PublishCurriculumStructureChangedForModuleAsync(course.ModuleId);

        _logger.LogInformation("[UpdateCourseAsync] Course Id {Id} updated successfully.", courseId);

        return new CourseResponseDto
        {
            Id = course.Id,
            Code = course.Code,
            ModuleId = course.ModuleId,
            Name = course.Name,
            Description = course.Description,
            CourseOrder = course.CourseOrder,
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt,
        };
    }

    private async Task ReorderWithinModuleAsync(Course course, Guid moduleId, int oldOrder, int newOrder)
    {
        var moduleCourses = await _unitOfWork.Courses.GetAllAsync(
            c => c.ModuleId == moduleId && !c.IsDeleted);

        SequentialOrderValidator.ValidateWithinRange(
            newOrder,
            minOrder: 1,
            maxOrder: moduleCourses.Count,
            orderPropertyName: "CourseOrder",
            scopeDescription: $"module '{moduleId}'");

        var others = moduleCourses.Where(c => c.Id != course.Id).ToList();

        var shifted = newOrder < oldOrder
            ? others.Where(c => c.CourseOrder >= newOrder && c.CourseOrder < oldOrder).ToList()
            : others.Where(c => c.CourseOrder > oldOrder && c.CourseOrder <= newOrder).ToList();

        var delta = newOrder < oldOrder ? 1 : -1;

        foreach (var neighbor in shifted)
        {
            neighbor.CourseOrder += delta;
        }

        if (shifted.Count > 0)
        {
            await _unitOfWork.Courses.UpdateRange(shifted);
        }

        course.CourseOrder = newOrder;
    }

    /// <summary>
    /// Moves <paramref name="course"/> to a different module: closes the gap it leaves in the old
    /// module and inserts it at the requested slot (or appends) in the new module, shifting neighbors.
    /// </summary>
    private async Task MoveCourseToModuleAsync(
        Course course,
        Guid oldModuleId,
        int oldOrder,
        Guid newModuleId,
        int? requestedOrder)
    {
        var oldModuleCourses = await _unitOfWork.Courses.GetAllAsync(
            c => c.ModuleId == oldModuleId && !c.IsDeleted && c.Id != course.Id);
        var newModuleCourses = await _unitOfWork.Courses.GetAllAsync(
            c => c.ModuleId == newModuleId && !c.IsDeleted);

        var targetOrder = requestedOrder ?? newModuleCourses.Count + 1;

        SequentialOrderValidator.ValidateWithinRange(
            targetOrder,
            minOrder: 1,
            maxOrder: newModuleCourses.Count + 1,
            orderPropertyName: "CourseOrder",
            scopeDescription: $"module '{newModuleId}'");

        var gapShifted = oldModuleCourses
            .Where(c => c.CourseOrder > oldOrder)
            .ToList();

        foreach (var neighbor in gapShifted)
        {
            neighbor.CourseOrder -= 1;
        }

        var insertShifted = newModuleCourses
            .Where(c => c.CourseOrder >= targetOrder)
            .ToList();

        foreach (var neighbor in insertShifted)
        {
            neighbor.CourseOrder += 1;
        }

        var toUpdate = gapShifted.Concat(insertShifted).ToList();

        if (toUpdate.Count > 0)
        {
            await _unitOfWork.Courses.UpdateRange(toUpdate);
        }

        course.ModuleId = newModuleId;
        course.CourseOrder = targetOrder;
    }

    // =========================================================================
    // DELETE (Soft Delete)
    // =========================================================================

    public async Task<bool> DeleteCourseAsync(Guid courseId)
    {
        _logger.LogInformation("[DeleteCourseAsync] Attempting to soft-delete course Id: {Id}", courseId);

        var course = await _unitOfWork.Courses.GetByIdAsync(courseId);

        if (course == null || course.IsDeleted)
        {
            _logger.LogWarning("[DeleteCourseAsync] Course with Id {Id} not found.", courseId);
            return false;
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(course.ModuleId);
        if (module != null && !module.IsDeleted)
        {
            await CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_unitOfWork, module.ProgramId);
        }

        // Close the ordering gap the deleted course leaves behind.
        var coursesToShift = await _unitOfWork.Courses.GetAllAsync(
            c => c.ModuleId == course.ModuleId
                 && !c.IsDeleted
                 && c.Id != courseId
                 && c.CourseOrder > course.CourseOrder);

        foreach (var neighbor in coursesToShift)
        {
            neighbor.CourseOrder -= 1;
        }

        if (coursesToShift.Count > 0)
        {
            await _unitOfWork.Courses.UpdateRange(coursesToShift);
        }

        await _unitOfWork.Courses.SoftRemove(course);
        await _unitOfWork.SaveChangesAsync();

        await PublishCurriculumStructureChangedForModuleAsync(course.ModuleId);

        _logger.LogInformation("[DeleteCourseAsync] Course Id {Id} soft-deleted successfully.", courseId);

        return true;
    }
}
