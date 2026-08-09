using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramReviewDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public class ProgramReviewService : IProgramReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ProgramReviewService> _logger;

    public ProgramReviewService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ProgramReviewService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<ProgramReviewResponseDto> CreateReviewAsync(Guid programId, CreateProgramReviewDto dto)
    {
        var studentId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "[CreateReviewAsync] StudentId={StudentId} creating review for ProgramId={ProgramId}",
            studentId, programId);

        if (dto.StarRating < 1 || dto.StarRating > 5)
            throw ErrorHelper.BadRequest("StarRating must be between 1 and 5.");

        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");

        var enrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            e => e.ProgramId == programId && e.StudentId == studentId && !e.IsDeleted);
        if (enrollment == null)
            throw ErrorHelper.Forbidden("You must be enrolled in this program to leave a review.");

        var existing = await _unitOfWork.ProgramReviews.FirstOrDefaultAsync(
            r => r.ProgramId == programId && r.StudentId == studentId && !r.IsDeleted);
        if (existing != null)
            throw ErrorHelper.Conflict("You have already submitted a review for this program.");

        var review = new ProgramReview
        {
            ProgramId = programId,
            StudentId = studentId,
            StarRating = dto.StarRating,
            Comment = dto.Comment,
        };

        await _unitOfWork.ProgramReviews.AddAsync(review);
        await _unitOfWork.SaveChangesAsync();

        await RecalculateProgramRatingAsync(programId);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[CreateReviewAsync] Review {ReviewId} created for Program {ProgramId}.",
            review.Id, programId);

        var student = await _unitOfWork.Users.GetByIdAsync(studentId);

        return MapToDto(review, student);
    }

    public async Task<Pagination<ProgramReviewResponseDto>> GetReviewsByProgramAsync(
        Guid programId,
        int page,
        int pageSize,
        string? sortBy,
        bool isDescending)
    {
        _logger.LogInformation(
            "[GetReviewsByProgramAsync] ProgramId={ProgramId}, page={Page}, pageSize={PageSize}",
            programId, page, pageSize);

        var query = _unitOfWork.ProgramReviews
            .GetQueryable()
            .Where(r => r.ProgramId == programId && !r.IsDeleted);

        query = sortBy?.ToLower() switch
        {
            "starrating" => isDescending
                ? query.OrderByDescending(r => r.StarRating)
                : query.OrderBy(r => r.StarRating),
            _ => isDescending
                ? query.OrderByDescending(r => r.CreatedAt)
                : query.OrderBy(r => r.CreatedAt),
        };

        var totalCount = query.Count();

        var reviews = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        if (reviews.Count == 0)
            return new Pagination<ProgramReviewResponseDto>(new List<ProgramReviewResponseDto>(), totalCount, page, pageSize);

        var studentIds = reviews.Select(r => r.StudentId).Distinct().ToList();
        var students = await _unitOfWork.Users.GetAllAsync(u => studentIds.Contains(u.Id) && !u.IsDeleted);
        var studentMap = students.ToDictionary(u => u.Id);

        var dtos = reviews.Select(r =>
        {
            studentMap.TryGetValue(r.StudentId, out var student);
            return MapToDto(r, student);
        }).ToList();

        _logger.LogInformation(
            "[GetReviewsByProgramAsync] Returned {Count}/{Total} reviews for Program {ProgramId}.",
            dtos.Count, totalCount, programId);

        return new Pagination<ProgramReviewResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ProgramReviewResponseDto> UpdateReviewAsync(
        Guid programId,
        Guid reviewId,
        UpdateProgramReviewDto dto)
    {
        var currentUserId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "[UpdateReviewAsync] UserId={UserId} updating ReviewId={ReviewId}",
            currentUserId, reviewId);

        var review = await _unitOfWork.ProgramReviews.GetByIdAsync(reviewId);
        if (review == null || review.IsDeleted)
            throw ErrorHelper.NotFound($"Review with id '{reviewId}' not found.");

        if (review.ProgramId != programId)
            throw ErrorHelper.NotFound($"Review '{reviewId}' does not belong to program '{programId}'.");

        if (review.StudentId != currentUserId)
            throw ErrorHelper.Forbidden("You can only edit your own reviews.");

        if (dto.StarRating.HasValue && (dto.StarRating < 1 || dto.StarRating > 5))
            throw ErrorHelper.BadRequest("StarRating must be between 1 and 5.");

        var isUpdated = UpdateHelper.ApplyUpdates(review, dto);

        if (isUpdated)
        {
            await _unitOfWork.ProgramReviews.Update(review);
            await _unitOfWork.SaveChangesAsync();

            await RecalculateProgramRatingAsync(programId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "[UpdateReviewAsync] Review {ReviewId} updated. Recalculated rating for Program {ProgramId}.",
                reviewId, programId);
        }
        else
        {
            _logger.LogInformation("[UpdateReviewAsync] No changes detected for Review {ReviewId}.", reviewId);
        }

        var student = await _unitOfWork.Users.GetByIdAsync(review.StudentId);
        return MapToDto(review, student);
    }

    public async Task<bool> DeleteReviewAsync(Guid programId, Guid reviewId)
    {
        var currentUserId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "[DeleteReviewAsync] UserId={UserId} deleting ReviewId={ReviewId}",
            currentUserId, reviewId);

        var review = await _unitOfWork.ProgramReviews.GetByIdAsync(reviewId);
        if (review == null || review.IsDeleted)
            throw ErrorHelper.NotFound($"Review with id '{reviewId}' not found.");

        if (review.ProgramId != programId)
            throw ErrorHelper.NotFound($"Review '{reviewId}' does not belong to program '{programId}'.");

        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId);
        bool isPrivileged = currentUser != null &&
            (currentUser.Role == RoleType.Admin || currentUser.Role == RoleType.Manager);

        if (review.StudentId != currentUserId && !isPrivileged)
            throw ErrorHelper.Forbidden("You are not authorised to delete this review.");

        await _unitOfWork.ProgramReviews.SoftRemove(review);
        await _unitOfWork.SaveChangesAsync();

        await RecalculateProgramRatingAsync(programId);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[DeleteReviewAsync] Review {ReviewId} soft-deleted. Recalculated rating for Program {ProgramId}.",
            reviewId, programId);

        return true;
    }

    private async Task RecalculateProgramRatingAsync(Guid programId)
    {
        var reviews = await _unitOfWork.ProgramReviews.GetAllAsync(
            r => r.ProgramId == programId && !r.IsDeleted);

        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null) return;

        program.TotalReviews = reviews.Count;
        program.Rating = reviews.Count > 0
            ? Math.Round((decimal)reviews.Average(r => r.StarRating), 1)
            : null;

        await _unitOfWork.Programs.Update(program);
    }

    private static ProgramReviewResponseDto MapToDto(ProgramReview review, User? student) => new()
    {
        Id = review.Id,
        ProgramId = review.ProgramId,
        StudentId = review.StudentId,
        StudentName = student?.FullName,
        StudentAvatarUrl = student?.AvatarUrl,
        StarRating = review.StarRating,
        Comment = review.Comment,
        CreatedAt = review.CreatedAt,
        UpdatedAt = review.UpdatedAt,
    };
}
