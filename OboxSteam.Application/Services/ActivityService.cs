using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ActivityDTO;
using OboxSteam.Application.DTOs.MaterialDTO;
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

        MaterialResponseDto? materialDto = null;
        var material = await _unitOfWork.Materials.FirstOrDefaultAsync(
            m => m.ActivityId == activityId && !m.IsDeleted);

        if (material != null)
        {
            materialDto = MaterialService.MapToDto(material);
        }

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
            Material = materialDto,
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

        SequentialOrderValidator.ValidateWithinRange(
            request.ActivityOrder,
            minOrder: 1,
            maxOrder: courseActivities.Count + 1,
            orderPropertyName: "ActivityOrder",
            scopeDescription: $"course '{request.CourseId}'");

        await ActivityValidator.ValidateActivityTypeForCourseAsync(
            _unitOfWork,
            request.CourseId,
            request.ActivityType);

        ActivityValidator.ValidateTypeRules(
            request.ActivityType,
            request.StartTime,
            request.EndTime,
            request.Location,
            request.RequireQrCheckin);

        // Insert-in-the-middle: shift existing activities at or after the requested slot.
        var activitiesToShift = courseActivities
            .Where(a => a.ActivityOrder >= request.ActivityOrder)
            .ToList();

        foreach (var existingActivity in activitiesToShift)
        {
            existingActivity.ActivityOrder += 1;
        }

        if (activitiesToShift.Count > 0)
        {
            await _unitOfWork.Activities.UpdateRange(activitiesToShift);
        }

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

        var oldCourseId = activity.CourseId;
        var oldOrder = activity.ActivityOrder;
        var courseChanged = request.CourseId.HasValue && request.CourseId.Value != oldCourseId;
        var targetCourseId = courseChanged ? request.CourseId!.Value : oldCourseId;

        if (courseChanged)
        {
            var targetCourse = await _unitOfWork.Courses.GetByIdAsync(targetCourseId);
            ActivityValidator.ValidateCourseExists(targetCourse, targetCourseId);
        }

        // When an activity is SelfPaced it has no schedule; clear location/times/capacity
        // (and QR check-in, which the domain only permits for Offline) so a stale schedule is
        // never persisted or returned after the type switches.
        var resolvedActivityType = request.ActivityType ?? activity.ActivityType;
        var isSelfPaced = resolvedActivityType == ActivityType.SelfPaced;

        var resolvedStartTime = isSelfPaced ? null : request.StartTime ?? activity.StartTime;
        var resolvedEndTime = isSelfPaced ? null : request.EndTime ?? activity.EndTime;
        var resolvedLocation = isSelfPaced ? null : request.Location ?? activity.Location;
        var resolvedMaxCapacity = isSelfPaced ? null : request.MaxCapacity ?? activity.MaxCapacity;
        var resolvedRequireQrCheckin = !isSelfPaced && (request.RequireQrCheckin ?? activity.RequireQrCheckin);

        await ActivityValidator.ValidateActivityTypeForCourseAsync(
            _unitOfWork,
            targetCourseId,
            resolvedActivityType);

        ActivityValidator.ValidateTypeRules(
            resolvedActivityType,
            resolvedStartTime,
            resolvedEndTime,
            resolvedLocation,
            resolvedRequireQrCheckin);

        if (courseChanged)
        {
            await MoveActivityToCourseAsync(activity, oldCourseId, oldOrder, targetCourseId, request.ActivityOrder);
        }
        else if (request.ActivityOrder.HasValue && request.ActivityOrder.Value != oldOrder)
        {
            await ReorderWithinCourseAsync(activity, targetCourseId, oldOrder, request.ActivityOrder.Value);
        }

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

        activity.Location = resolvedLocation;
        activity.StartTime = resolvedStartTime;
        activity.EndTime = resolvedEndTime;
        activity.MaxCapacity = resolvedMaxCapacity;
        activity.RequireQrCheckin = resolvedRequireQrCheckin;

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
    // REORDER HELPERS
    // =========================================================================

    /// <summary>
    /// Moves <paramref name="activity"/> to a new position within the same course, shifting the
    /// activities in between by one slot (drag-and-drop semantics). Runs in the caller's unit of work.
    /// </summary>
    private async Task ReorderWithinCourseAsync(Activity activity, Guid courseId, int oldOrder, int newOrder)
    {
        var courseActivities = await _unitOfWork.Activities.GetAllAsync(
            a => a.CourseId == courseId && !a.IsDeleted);

        SequentialOrderValidator.ValidateWithinRange(
            newOrder,
            minOrder: 1,
            maxOrder: courseActivities.Count,
            orderPropertyName: "ActivityOrder",
            scopeDescription: $"course '{courseId}'");

        var others = courseActivities.Where(a => a.Id != activity.Id).ToList();

        var shifted = newOrder < oldOrder
            ? others.Where(a => a.ActivityOrder >= newOrder && a.ActivityOrder < oldOrder).ToList()
            : others.Where(a => a.ActivityOrder > oldOrder && a.ActivityOrder <= newOrder).ToList();

        var delta = newOrder < oldOrder ? 1 : -1;

        foreach (var neighbor in shifted)
        {
            neighbor.ActivityOrder += delta;
        }

        if (shifted.Count > 0)
        {
            await _unitOfWork.Activities.UpdateRange(shifted);
        }

        activity.ActivityOrder = newOrder;
    }

    /// <summary>
    /// Moves <paramref name="activity"/> to a different course: closes the gap it leaves in the old
    /// course and inserts it at the requested slot (or appends) in the new course, shifting neighbors.
    /// </summary>
    private async Task MoveActivityToCourseAsync(
        Activity activity,
        Guid oldCourseId,
        int oldOrder,
        Guid newCourseId,
        int? requestedOrder)
    {
        var oldCourseActivities = await _unitOfWork.Activities.GetAllAsync(
            a => a.CourseId == oldCourseId && !a.IsDeleted && a.Id != activity.Id);

        var newCourseActivities = await _unitOfWork.Activities.GetAllAsync(
            a => a.CourseId == newCourseId && !a.IsDeleted);

        var targetOrder = requestedOrder ?? newCourseActivities.Count + 1;

        SequentialOrderValidator.ValidateWithinRange(
            targetOrder,
            minOrder: 1,
            maxOrder: newCourseActivities.Count + 1,
            orderPropertyName: "ActivityOrder",
            scopeDescription: $"course '{newCourseId}'");

        var gapShifted = oldCourseActivities
            .Where(a => a.ActivityOrder > oldOrder)
            .ToList();

        foreach (var neighbor in gapShifted)
        {
            neighbor.ActivityOrder -= 1;
        }

        var insertShifted = newCourseActivities
            .Where(a => a.ActivityOrder >= targetOrder)
            .ToList();

        foreach (var neighbor in insertShifted)
        {
            neighbor.ActivityOrder += 1;
        }

        var toUpdate = gapShifted.Concat(insertShifted).ToList();

        if (toUpdate.Count > 0)
        {
            await _unitOfWork.Activities.UpdateRange(toUpdate);
        }

        activity.CourseId = newCourseId;
        activity.ActivityOrder = targetOrder;
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
