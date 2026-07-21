using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Business rules for mentor class-assignment requests and concurrent-class limits.
/// </summary>
public static class ClassMentorRequestValidator
{
    public static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1)
        {
            throw ErrorHelper.BadRequest("Invalid pagination parameters. Page and pageSize must be at least 1.");
        }
    }

    public static void ValidateRequestExists(ClassMentorRequest? request, Guid id)
    {
        if (request == null || request.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Class mentor request with id '{id}' not found.");
        }
    }

    public static void ValidateClassOpenForRequests(Class classEntity)
    {
        if (classEntity.MentorId.HasValue)
        {
            throw ErrorHelper.Conflict(
                $"Class '{classEntity.Code}' already has an assigned mentor.");
        }

        if (classEntity.Status is not (ClassStatus.Draft or ClassStatus.Open))
        {
            throw ErrorHelper.BadRequest(
                $"Only Draft or Open classes accept mentor requests (status: {classEntity.Status}).");
        }
    }

    public static void ValidateMentorEligible(User? mentor, Guid mentorId)
    {
        ClassValidator.ValidateMentorExists(mentor, mentorId);

        if (mentor!.Role != RoleType.Mentor)
        {
            throw ErrorHelper.Forbidden("Only users with the Mentor role may submit class assignment requests.");
        }
    }

    public static void ValidateNoDuplicatePending(ClassMentorRequest? existingPending)
    {
        if (existingPending != null)
        {
            throw ErrorHelper.Conflict(
                "You already have a pending request for this class.");
        }
    }

    public static void ValidateOwnership(ClassMentorRequest request, Guid mentorId)
    {
        if (request.MentorId != mentorId)
        {
            throw ErrorHelper.Forbidden("You can only manage your own class mentor requests.");
        }
    }

    public static void ValidatePendingForWithdraw(ClassMentorRequest request)
    {
        if (request.Status != ClassMentorRequestStatus.Pending)
        {
            throw ErrorHelper.BadRequest(
                $"Only Pending requests can be withdrawn (status: {request.Status}).");
        }
    }

    public static void ValidatePendingForDecision(ClassMentorRequest request)
    {
        if (request.Status != ClassMentorRequestStatus.Pending)
        {
            throw ErrorHelper.BadRequest(
                $"Only Pending requests can be approved or rejected (status: {request.Status}).");
        }
    }

    public static int ResolveMaxConcurrentClasses(User mentor)
        => mentor.MaxConcurrentClasses ?? MentorRequestConstants.DefaultMaxConcurrentClasses;

    /// <summary>
    /// Ensures the mentor is under their concurrent-class cap.
    /// Counts active assigned classes (not Completed/Cancelled) plus Pending requests.
    /// </summary>
    public static async Task ValidateUnderConcurrentLimitAsync(
        IUnitOfWork unitOfWork,
        User mentor,
        Guid? excludeClassId = null,
        Guid? excludeRequestId = null)
    {
        var max = ResolveMaxConcurrentClasses(mentor);
        var usage = await CountConcurrentUsageAsync(unitOfWork, mentor.Id, excludeClassId, excludeRequestId);

        if (usage >= max)
        {
            throw ErrorHelper.Conflict(
                $"Mentor has reached the concurrent class limit ({usage}/{max}).");
        }
    }

    public static Task<int> CountConcurrentUsageAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId,
        Guid? excludeClassId = null,
        Guid? excludeRequestId = null)
    {
        var assignedCount = unitOfWork.Classes
            .GetQueryable()
            .Count(c => c.MentorId == mentorId
                        && !c.IsDeleted
                        && c.Status != ClassStatus.Completed
                        && c.Status != ClassStatus.Cancelled
                        && (!excludeClassId.HasValue || c.Id != excludeClassId.Value));

        var pendingCount = unitOfWork.ClassMentorRequests
            .GetQueryable()
            .Count(r => r.MentorId == mentorId
                        && !r.IsDeleted
                        && r.Status == ClassMentorRequestStatus.Pending
                        && (!excludeRequestId.HasValue || r.Id != excludeRequestId.Value));

        return Task.FromResult(assignedCount + pendingCount);
    }

    public static Task<(int Assigned, int Pending)> GetUsageBreakdownAsync(
        IUnitOfWork unitOfWork,
        Guid mentorId)
    {
        var assignedCount = unitOfWork.Classes
            .GetQueryable()
            .Count(c => c.MentorId == mentorId
                        && !c.IsDeleted
                        && c.Status != ClassStatus.Completed
                        && c.Status != ClassStatus.Cancelled);

        var pendingCount = unitOfWork.ClassMentorRequests
            .GetQueryable()
            .Count(r => r.MentorId == mentorId
                        && !r.IsDeleted
                        && r.Status == ClassMentorRequestStatus.Pending);

        return Task.FromResult((assignedCount, pendingCount));
    }
}
