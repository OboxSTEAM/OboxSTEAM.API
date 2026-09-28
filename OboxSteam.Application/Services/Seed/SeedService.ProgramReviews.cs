using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private static readonly (string Code, string FullName)[] SeedReviewerAccounts =
    [
        ("STD-RV01", "Nguyễn Minh Anh"),
        ("STD-RV02", "Trần Gia Bảo"),
        ("STD-RV03", "Lê Khánh Chi"),
        ("STD-RV04", "Phạm Đức Duy"),
        ("STD-RV05", "Hoàng Ngọc Hân"),
        ("STD-RV06", "Vũ Quốc Huy"),
        ("STD-RV07", "Đặng Thảo Linh"),
        ("STD-RV08", "Bùi Tuấn Kiệt"),
        ("STD-RV09", "Đỗ Phương Mai"),
        ("STD-RV10", "Ngô Hoàng Nam"),
        ("STD-RV11", "Dương Bảo Ngọc"),
        ("STD-RV12", "Lý Thành Phát"),
    ];

    private static readonly int[] SeedReviewStarPattern = [5, 4, 5, 4, 3, 5, 4, 5, 2, 4, 5, 3];

    private static readonly Dictionary<int, string[]> SeedReviewCommentsByStars = new()
    {
        [5] =
        [
            "Chương trình rất hay, mentor nhiệt tình và dự án cuối khóa giúp em hiểu bài sâu hơn.",
            "Nội dung thực hành nhiều, bài tập sát với buổi học. Em sẽ học tiếp chương trình khác.",
            "Lộ trình rõ ràng, tài liệu đầy đủ. Buổi trình bày sản phẩm cuối khóa rất đáng nhớ.",
        ],
        [4] =
        [
            "Kiến thức bổ ích, mentor phản hồi nhanh. Một vài buổi hơi dài nhưng nhìn chung rất ổn.",
            "Học xong em tự làm được dự án nhỏ. Mong có thêm bài luyện tập ở học phần cuối.",
            "Lịch học hợp lý, bài giảng dễ hiểu. Phần nghiên cứu hơi khó nhưng được hỗ trợ tốt.",
        ],
        [3] =
        [
            "Nội dung ổn nhưng tốc độ hơi nhanh với người mới bắt đầu.",
            "Chương trình được, tuy nhiên phần tài liệu tự học cần cập nhật thêm ví dụ.",
        ],
        [2] =
        [
            "Lịch học thay đổi vài lần nên em khó theo kịp. Mong chương trình sắp xếp lại lịch ổn định hơn.",
        ],
    };

    /// <summary>
    /// Gives every Active program 5-12 reviews from dedicated reviewer accounts (STD-RV01..12).
    /// Each reviewer gets a real Completed enrollment (module enrollments + Done activity progress)
    /// so reviews satisfy the completion rule; payments and certificates are added by the seed steps
    /// that run afterwards. Other student accounts are left untouched so they can test the review form.
    /// Idempotent per (reviewer, program); always recomputes Program.Rating / TotalReviews.
    /// </summary>
    private async Task SeedProgramReviewsAsync()
    {
        _loggerService.LogInformation("Starting seed program reviews");

        var reviewers = await EnsureSeedReviewerAccountsAsync();

        var programs = (await _unitOfWork.Programs.GetAllAsync(
                p => !p.IsDeleted && p.Status == ProgramStatus.Active))
            .OrderBy(p => p.Code)
            .ToList();

        var reviewerIds = reviewers.Select(r => r.Id).ToList();
        var reviewerEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => reviewerIds.Contains(pe.StudentId) && !pe.IsDeleted);
        var enrollmentByKey = reviewerEnrollments
            .GroupBy(pe => (pe.StudentId, pe.ProgramId))
            .ToDictionary(g => g.Key, g => g.First());
        var reviewerReviews = await _unitOfWork.ProgramReviews.GetAllIncludingDeletedAsync(
            r => reviewerIds.Contains(r.StudentId));
        var reviewedKeys = reviewerReviews.Select(r => (r.StudentId, r.ProgramId)).ToHashSet();

        var createdEnrollments = 0;
        var newReviews = new List<(ProgramReview Review, DateTime CreatedAt)>();

        for (var programIndex = 0; programIndex < programs.Count; programIndex++)
        {
            var program = programs[programIndex];
            if (!await ProgramHasSeedableCurriculumAsync(program.Id))
            {
                _loggerService.LogWarning(
                    "Skipping reviews for {ProgramCode}: program has no activities.",
                    program.Code);
                continue;
            }

            var reviewCount = 5 + (programIndex * 3 % 8);
            for (var slot = 0; slot < reviewCount; slot++)
            {
                var reviewer = reviewers[(programIndex + slot) % reviewers.Count];
                var key = (reviewer.Id, program.Id);
                var completedAt = _seedNow.AddDays(-(7 + ((programIndex * 5 + slot * 13) % 170)));

                if (!enrollmentByKey.TryGetValue(key, out var enrollment))
                {
                    enrollment = new ProgramEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = reviewer.Id,
                        ProgramId = program.Id,
                        Status = EnrollmentStatus.Completed,
                        ProgressPercent = 100m,
                        EnrolledAt = completedAt.AddDays(-75),
                        StartedAt = completedAt.AddDays(-72),
                        CompletedAt = completedAt,
                        CreatedBy = reviewer.Id,
                        IsDeleted = false,
                    };
                    await _unitOfWork.ProgramEnrollments.AddAsync(enrollment);
                    await _unitOfWork.SaveChangesAsync();
                    await EnsureCompletedEnrollmentActivitiesDoneAsync(enrollment);
                    enrollmentByKey[key] = enrollment;
                    createdEnrollments++;
                }

                if (enrollment.Status != EnrollmentStatus.Completed || reviewedKeys.Contains(key))
                {
                    continue;
                }

                var stars = SeedReviewStarPattern[(programIndex * 7 + slot) % SeedReviewStarPattern.Length];
                var comments = SeedReviewCommentsByStars[stars];
                var reviewCreatedAt = (enrollment.CompletedAt ?? completedAt).AddDays(1 + (slot % 6));
                if (reviewCreatedAt > _seedNow)
                {
                    reviewCreatedAt = _seedNow.AddHours(-1);
                }

                newReviews.Add((new ProgramReview
                {
                    Id = Guid.NewGuid(),
                    ProgramId = program.Id,
                    StudentId = reviewer.Id,
                    StarRating = stars,
                    Comment = slot % 5 == 4 ? null : comments[(programIndex + slot) % comments.Length],
                    CreatedBy = reviewer.Id,
                    IsDeleted = false,
                }, reviewCreatedAt));
                reviewedKeys.Add(key);
            }
        }

        if (newReviews.Count > 0)
        {
            await _unitOfWork.ProgramReviews.AddRangeAsync(newReviews.Select(r => r.Review).ToList());

            // AddRangeAsync stamps "now"; restore the spread-out history and keep reviews unedited.
            foreach (var (review, createdAt) in newReviews)
            {
                review.CreatedAt = createdAt;
                review.UpdatedAt = null;
            }

            await _unitOfWork.SaveChangesAsync();
        }

        await RecalculateSeedProgramRatingsAsync();

        _loggerService.LogInformation(
            "Finished seed program reviews — {Enrollments} reviewer enrollment(s), {Reviews} review(s) created.",
            createdEnrollments,
            newReviews.Count);
    }

    private async Task<List<User>> EnsureSeedReviewerAccountsAsync()
    {
        var codes = SeedReviewerAccounts.Select(a => a.Code).ToList();
        var existing = await _unitOfWork.Users.GetAllIncludingDeletedAsync(u => codes.Contains(u.Code));
        var existingByCode = existing.ToDictionary(u => u.Code);

        var toAdd = new List<User>();
        for (var i = 0; i < SeedReviewerAccounts.Length; i++)
        {
            var (code, fullName) = SeedReviewerAccounts[i];
            if (existingByCode.ContainsKey(code))
            {
                continue;
            }

            toAdd.Add(new User
            {
                Id = Guid.NewGuid(),
                Code = code,
                Email = $"reviewer{i + 1:D2}@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Student@123")!,
                FullName = fullName,
                Phone = $"09880000{i + 1:D2}",
                Role = RoleType.Student,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (toAdd.Count > 0)
        {
            await _unitOfWork.Users.AddRangeAsync(toAdd);
            await _unitOfWork.SaveChangesAsync();
        }

        return existing
            .Concat(toAdd)
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Code)
            .ToList();
    }

    private async Task<bool> ProgramHasSeedableCurriculumAsync(Guid programId)
    {
        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == programId && !m.IsDeleted);
        foreach (var module in modules)
        {
            var activityIds = await ActivityProgressCalculationHelper.GetModuleActivityIdsAsync(
                _unitOfWork,
                module.Id);
            if (activityIds.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private async Task RecalculateSeedProgramRatingsAsync()
    {
        var programs = await _unitOfWork.Programs.GetAllAsync(p => !p.IsDeleted);
        var reviews = await _unitOfWork.ProgramReviews.GetAllAsync(r => !r.IsDeleted);
        var reviewsByProgram = reviews
            .GroupBy(r => r.ProgramId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var changed = false;
        foreach (var program in programs)
        {
            reviewsByProgram.TryGetValue(program.Id, out var programReviews);
            var total = programReviews?.Count ?? 0;
            decimal? rating = total > 0
                ? Math.Round((decimal)programReviews!.Average(r => r.StarRating), 1)
                : null;

            if (program.TotalReviews == total && program.Rating == rating)
            {
                continue;
            }

            program.TotalReviews = total;
            program.Rating = rating;
            await _unitOfWork.Programs.Update(program);
            changed = true;
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
