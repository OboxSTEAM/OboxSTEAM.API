using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ActivityDTO;
using OboxSteam.Application.DTOs.CourseDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class CourseService : ICourseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CourseService> _logger;

    public CourseService(IUnitOfWork unitOfWork, ILogger<CourseService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
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
        string? moduleName,
        string? mentorName)
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

        if (!string.IsNullOrWhiteSpace(mentorName))
        {
            var lowerMentorName = mentorName.ToLower();
            query = query.Where(c =>
                c.Mentor.FullName != null &&
                c.Mentor.FullName.ToLower().Contains(lowerMentorName));
        }

        query = sortBy?.ToLower() switch
        {
            "name" => isDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            "code" => isDescending ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code),
            "moduleid" => isDescending ? query.OrderByDescending(c => c.ModuleId) : query.OrderBy(c => c.ModuleId),
            "mentorid" => isDescending ? query.OrderByDescending(c => c.MentorId) : query.OrderBy(c => c.MentorId),
            "createdat" => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
            _ => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = items.Select(MapToResponseDto).ToList();

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
            MentorId = course.MentorId,
            Name = course.Name,
            Description = course.Description,
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
                    Location = a.Location,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    MaxCapacity = a.MaxCapacity,
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
            MentorId = course.MentorId,
            Name = course.Name,
            Description = course.Description,
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
                    Location = a.Location,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    MaxCapacity = a.MaxCapacity,
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

        var mentor = await _unitOfWork.Users.GetByIdAsync(request.MentorId);

        if (mentor == null || mentor.IsDeleted)
        {
            _logger.LogWarning("[CreateCourseAsync] Mentor with Id {Id} not found.", request.MentorId);
            throw new NotFoundException($"Mentor with id '{request.MentorId}' not found.");
        }

        var existing = await _unitOfWork.Courses.FirstOrDefaultAsync(
            c => c.Code.ToLower() == request.Code.ToLower() && !c.IsDeleted);

        if (existing != null)
        {
            _logger.LogWarning("[CreateCourseAsync] Course with code '{Code}' already exists.", request.Code);
            throw new ConflictException($"Course with code '{request.Code}' already exists.");
        }

        var course = new Course
        {
            Code = request.Code,
            ModuleId = request.ModuleId,
            MentorId = request.MentorId,
            Name = request.Name,
            Description = request.Description,
        };

        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[CreateCourseAsync] Course '{Code}' created successfully with Id {Id}.",
            course.Code, course.Id);

        return MapToResponseDto(course);
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

        if (request.ModuleId.HasValue && course.ModuleId != request.ModuleId.Value)
        {
            var module = await _unitOfWork.Modules.GetByIdAsync(request.ModuleId.Value);

            if (module == null || module.IsDeleted)
            {
                _logger.LogWarning("[UpdateCourseAsync] Module with Id {Id} not found.", request.ModuleId.Value);
                throw new NotFoundException($"Module with id '{request.ModuleId}' not found.");
            }

            course.ModuleId = request.ModuleId.Value;
        }

        if (request.MentorId.HasValue && course.MentorId != request.MentorId.Value)
        {
            var mentor = await _unitOfWork.Users.GetByIdAsync(request.MentorId.Value);

            if (mentor == null || mentor.IsDeleted)
            {
                _logger.LogWarning("[UpdateCourseAsync] Mentor with Id {Id} not found.", request.MentorId.Value);
                throw new NotFoundException($"Mentor with id '{request.MentorId}' not found.");
            }

            course.MentorId = request.MentorId.Value;
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

        _logger.LogInformation("[UpdateCourseAsync] Course Id {Id} updated successfully.", courseId);

        return MapToResponseDto(course);
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

        await _unitOfWork.Courses.SoftRemove(course);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[DeleteCourseAsync] Course Id {Id} soft-deleted successfully.", courseId);

        return true;
    }

    private static CourseResponseDto MapToResponseDto(Course course) => new()
    {
        Id = course.Id,
        Code = course.Code,
        ModuleId = course.ModuleId,
        MentorId = course.MentorId,
        Name = course.Name,
        Description = course.Description,
        CreatedAt = course.CreatedAt,
        UpdatedAt = course.UpdatedAt,
    };
}
