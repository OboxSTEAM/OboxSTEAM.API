using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ActivityDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class ActivityService : IActivityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(IUnitOfWork unitOfWork, ILogger<ActivityService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // =========================================================================
    // GET ALL (PAGINATION + FILTER + SORT)
    // =========================================================================

    public async Task<Pagination<ActivitiesResponseDto>> GetAllActivitiesAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        string? code,
        Guid? courseId,
        ActivityType? activityType)
    {
        _logger.LogInformation(
            "[GetAllActivitiesAsync] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
            page, pageSize, search);

        var query = _unitOfWork.Activities
            .GetQueryable()
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(a =>
                a.Name.ToLower().Contains(lowerSearch) ||
                a.Code.ToLower().Contains(lowerSearch));
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            query = query.Where(a => a.Code.ToLower().Contains(code.ToLower()));
        }

        if (courseId.HasValue)
        {
            query = query.Where(a => a.CourseId == courseId.Value);
        }

        if (activityType.HasValue)
        {
            query = query.Where(a => a.ActivityType == activityType.Value);
        }

        query = sortBy?.ToLower() switch
        {
            "name" => isDescending ? query.OrderByDescending(a => a.Name) : query.OrderBy(a => a.Name),
            "code" => isDescending ? query.OrderByDescending(a => a.Code) : query.OrderBy(a => a.Code),
            "activityorder" => isDescending ? query.OrderByDescending(a => a.ActivityOrder) : query.OrderBy(a => a.ActivityOrder),
            "activitytype" => isDescending ? query.OrderByDescending(a => a.ActivityType) : query.OrderBy(a => a.ActivityType),
            "starttime" => isDescending ? query.OrderByDescending(a => a.StartTime) : query.OrderBy(a => a.StartTime),
            "endtime" => isDescending ? query.OrderByDescending(a => a.EndTime) : query.OrderBy(a => a.EndTime),
            "createdat" => isDescending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
            _ => isDescending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = items.Select(a => new ActivitiesResponseDto
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
        }).ToList();

        _logger.LogInformation("[GetAllActivitiesAsync] Retrieved {Count}/{Total} activities.", dtos.Count, totalCount);

        return new Pagination<ActivitiesResponseDto>(dtos, totalCount, page, pageSize);
    }

    // =========================================================================
    // GET BY ID
    // =========================================================================

    public async Task<ActivitiesResponseDto?> GetActivityByIdAsync(Guid activityId)
    {
        _logger.LogInformation("[GetActivityByIdAsync] Fetching activity with Id: {Id}", activityId);

        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);

        if (activity == null || activity.IsDeleted)
        {
            _logger.LogWarning("[GetActivityByIdAsync] Activity with Id {Id} not found.", activityId);
            return null;
        }

        _logger.LogInformation("[GetActivityByIdAsync] Activity with Id {Id} retrieved successfully.", activityId);
        return new ActivitiesResponseDto
        {
            Id = activity.Id,
            Code = activity.Code,
            CourseId = activity.CourseId,
            Name = activity.Name,
            ActivityType = activity.ActivityType,
            Description = activity.Description,
            ActivityOrder = activity.ActivityOrder,
            Location = activity.Location,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            MaxCapacity = activity.MaxCapacity,
            RequireQrCheckin = activity.RequireQrCheckin,
            RequireMediaEvidence = activity.RequireMediaEvidence,
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
        };
    }

    // =========================================================================
    // GET BY CODE
    // =========================================================================

    public async Task<ActivitiesResponseDto?> GetActivityByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw ErrorHelper.BadRequest("Activity code is required.");
        }

        _logger.LogInformation("[GetActivityByCodeAsync] Fetching activity with code: {Code}", code);

        var activity = await _unitOfWork.Activities.FirstOrDefaultAsync(
            a => a.Code.ToLower() == code.ToLower() && !a.IsDeleted);

        if (activity == null)
        {
            _logger.LogWarning("[GetActivityByCodeAsync] Activity with code '{Code}' not found.", code);
            return null;
        }

        _logger.LogInformation("[GetActivityByCodeAsync] Activity '{Code}' retrieved successfully.", code);
        return new ActivitiesResponseDto
        {
            Id = activity.Id,
            Code = activity.Code,
            CourseId = activity.CourseId,
            Name = activity.Name,
            ActivityType = activity.ActivityType,
            Description = activity.Description,
            ActivityOrder = activity.ActivityOrder,
            Location = activity.Location,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            MaxCapacity = activity.MaxCapacity,
            RequireQrCheckin = activity.RequireQrCheckin,
            RequireMediaEvidence = activity.RequireMediaEvidence,
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
        };
    }

    // =========================================================================
    // CREATE
    // =========================================================================

    public async Task<ActivitiesResponseDto> CreateActivityAsync(CreateActivitiesRequestDto request)
    {
        _logger.LogInformation("[CreateActivityAsync] Start creating activity: {Name} (Code: {Code})",
            request.Name, request.Code);

        var course = await _unitOfWork.Courses.GetByIdAsync(request.CourseId);
        ActivityValidator.ValidateCourseExists(course, request.CourseId);

        var existing = await _unitOfWork.Activities.FirstOrDefaultAsync(
            a => a.Code.ToLower() == request.Code.ToLower() && !a.IsDeleted);

        if (existing != null)
        {
            _logger.LogWarning("[CreateActivityAsync] Activity with code '{Code}' already exists.", request.Code);
            throw ErrorHelper.Conflict($"Activity with code '{request.Code}' already exists.");
        }

        var courseActivities = await _unitOfWork.Activities.GetAllAsync(
            a => a.CourseId == request.CourseId && !a.IsDeleted);
        var currentMaxOrder = courseActivities.Count == 0 ? 0 : courseActivities.Max(a => a.ActivityOrder);

        SequentialOrderValidator.ValidateMustExceedMax(
            request.ActivityOrder,
            currentMaxOrder,
            orderPropertyName: "ActivityOrder",
            scopeDescription: $"course '{request.CourseId}'");
        ActivityValidator.ValidateTypeRules(
            request.ActivityType,
            request.StartTime,
            request.EndTime,
            request.Location,
            request.RequireQrCheckin);

        var activity = new Activity
        {
            Code = request.Code,
            CourseId = request.CourseId,
            Name = request.Name,
            ActivityType = request.ActivityType,
            Description = request.Description,
            ActivityOrder = request.ActivityOrder,
            Location = request.Location,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            MaxCapacity = request.MaxCapacity,
            RequireQrCheckin = request.RequireQrCheckin,
            RequireMediaEvidence = request.RequireMediaEvidence,
        };

        await _unitOfWork.Activities.AddAsync(activity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[CreateActivityAsync] Activity '{Code}' created successfully with Id {Id}.",
            activity.Code, activity.Id);

        return new ActivitiesResponseDto
        {
            Id = activity.Id,
            Code = activity.Code,
            CourseId = activity.CourseId,
            Name = activity.Name,
            ActivityType = activity.ActivityType,
            Description = activity.Description,
            ActivityOrder = activity.ActivityOrder,
            Location = activity.Location,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            MaxCapacity = activity.MaxCapacity,
            RequireQrCheckin = activity.RequireQrCheckin,
            RequireMediaEvidence = activity.RequireMediaEvidence,
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
        };
    }

    // =========================================================================
    // UPDATE
    // =========================================================================

    public async Task<ActivitiesResponseDto?> UpdateActivityAsync(Guid activityId, UpdateActivitiesRequestDto request)
    {
        _logger.LogInformation("[UpdateActivityAsync] Attempting to update activity with Id: {Id}", activityId);

        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);

        if (activity == null || activity.IsDeleted)
        {
            _logger.LogWarning("[UpdateActivityAsync] Activity with Id {Id} not found.", activityId);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Code) &&
            !activity.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _unitOfWork.Activities.FirstOrDefaultAsync(
                a => a.Code.ToLower() == request.Code.ToLower() &&
                     !a.IsDeleted &&
                     a.Id != activityId);

            if (duplicate != null)
            {
                _logger.LogWarning("[UpdateActivityAsync] Code '{Code}' is already in use.", request.Code);
                throw ErrorHelper.Conflict($"Activity with code '{request.Code}' already exists.");
            }

            activity.Code = request.Code;
        }

        var targetCourseId = activity.CourseId;

        if (request.CourseId.HasValue && activity.CourseId != request.CourseId.Value)
        {
            var targetCourse = await _unitOfWork.Courses.GetByIdAsync(request.CourseId.Value);
            ActivityValidator.ValidateCourseExists(targetCourse, request.CourseId.Value);
            targetCourseId = request.CourseId.Value;
            activity.CourseId = request.CourseId.Value;
        }

        if (request.ActivityOrder.HasValue || request.CourseId.HasValue)
        {
            var courseActivities = await _unitOfWork.Activities.GetAllAsync(
                a => a.CourseId == targetCourseId && !a.IsDeleted);
            var activitiesForMax = courseActivities.Where(a => a.Id != activityId).ToList();
            var currentMaxOrder = activitiesForMax.Count == 0 ? 0 : activitiesForMax.Max(a => a.ActivityOrder);
            var orderToValidate = request.ActivityOrder ?? activity.ActivityOrder;

            SequentialOrderValidator.ValidateMustExceedMax(
                orderToValidate,
                currentMaxOrder,
                orderPropertyName: "ActivityOrder",
                scopeDescription: $"course '{targetCourseId}'");

            if (request.ActivityOrder.HasValue)
            {
                activity.ActivityOrder = request.ActivityOrder.Value;
            }
        }

        var resolvedActivityType = request.ActivityType ?? activity.ActivityType;
        var resolvedStartTime = request.StartTime ?? activity.StartTime;
        var resolvedEndTime = request.EndTime ?? activity.EndTime;
        var resolvedLocation = request.Location ?? activity.Location;
        var resolvedRequireQrCheckin = request.RequireQrCheckin ?? activity.RequireQrCheckin;

        ActivityValidator.ValidateTypeRules(
            resolvedActivityType,
            resolvedStartTime,
            resolvedEndTime,
            resolvedLocation,
            resolvedRequireQrCheckin);

        if (request.ActivityType.HasValue)
        {
            activity.ActivityType = request.ActivityType.Value;
        }

        if (request.Name != null && activity.Name != request.Name)
        {
            activity.Name = request.Name;
        }

        if (request.Description != null && activity.Description != request.Description)
        {
            activity.Description = request.Description;
        }

        if (request.Location != null && activity.Location != request.Location)
        {
            activity.Location = request.Location;
        }

        if (request.StartTime.HasValue)
        {
            activity.StartTime = request.StartTime;
        }

        if (request.EndTime.HasValue)
        {
            activity.EndTime = request.EndTime;
        }

        if (request.MaxCapacity.HasValue)
        {
            activity.MaxCapacity = request.MaxCapacity;
        }

        if (request.RequireQrCheckin.HasValue)
        {
            activity.RequireQrCheckin = request.RequireQrCheckin.Value;
        }

        if (request.RequireMediaEvidence.HasValue)
        {
            activity.RequireMediaEvidence = request.RequireMediaEvidence.Value;
        }

        await _unitOfWork.Activities.Update(activity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[UpdateActivityAsync] Activity Id {Id} updated successfully.", activityId);

        return new ActivitiesResponseDto
        {
            Id = activity.Id,
            Code = activity.Code,
            CourseId = activity.CourseId,
            Name = activity.Name,
            ActivityType = activity.ActivityType,
            Description = activity.Description,
            ActivityOrder = activity.ActivityOrder,
            Location = activity.Location,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            MaxCapacity = activity.MaxCapacity,
            RequireQrCheckin = activity.RequireQrCheckin,
            RequireMediaEvidence = activity.RequireMediaEvidence,
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
        };
    }

    // =========================================================================
    // DELETE (Soft Delete)
    // =========================================================================

    public async Task<bool> DeleteActivityAsync(Guid activityId)
    {
        _logger.LogInformation("[DeleteActivityAsync] Attempting to soft-delete activity Id: {Id}", activityId);

        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);

        if (activity == null || activity.IsDeleted)
        {
            _logger.LogWarning("[DeleteActivityAsync] Activity with Id {Id} not found.", activityId);
            return false;
        }

        await _unitOfWork.Activities.SoftRemove(activity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[DeleteActivityAsync] Activity Id {Id} soft-deleted successfully.", activityId);

        return true;
    }

}
