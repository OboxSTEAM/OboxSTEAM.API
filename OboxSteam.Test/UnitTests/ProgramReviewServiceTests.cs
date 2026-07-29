using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ProgramReviewDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ProgramReviewServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherProgramId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _reviewId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _otherReviewId = Guid.Parse("34343434-3434-3434-3434-343434343434");

    private readonly DateTime _now = DateTime.UtcNow;
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private ProgramReviewService CreateSut(Guid? userId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(userId ?? _studentId);
        return new ProgramReviewService(
            _db,
            _claimsService.Object,
            NullLogger<ProgramReviewService>.Instance);
    }

    private void SeedUser(Guid id, RoleType role, string code, string? fullName = null)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = fullName ?? code,
            Role = role,
            IsDeleted = false,
        });
    }

    private void SeedProgram(Guid? id = null)
    {
        var programId = id ?? _programId;
        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = programId == _otherProgramId ? "PRG-002" : "PRG-001",
            Name = "Robotics",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
    }

    private void SeedEnrollment(Guid studentId, Guid? programId = null)
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ProgramId = programId ?? _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
    }

    private ProgramReview SeedReview(
        Guid? id = null,
        Guid? studentId = null,
        Guid? programId = null,
        int starRating = 4,
        DateTime? createdAt = null,
        bool isDeleted = false)
    {
        var review = new ProgramReview
        {
            Id = id ?? _reviewId,
            ProgramId = programId ?? _programId,
            StudentId = studentId ?? _studentId,
            StarRating = starRating,
            Comment = "Good course",
            CreatedAt = createdAt ?? _now.AddDays(-1),
            IsDeleted = isDeleted,
        };
        _db.ProgramReviews.Seed(review);
        return review;
    }

    [Fact]
    public async Task CreateReview_PersistsAndRecalculatesRating()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001", "Alice");
        SeedProgram();
        SeedEnrollment(_studentId);
        var sut = CreateSut();

        var result = await sut.CreateReviewAsync(_programId, new CreateProgramReviewDto
        {
            StarRating = 5,
            Comment = "  Excellent  ",
        });

        Assert.Equal(5, result.StarRating);
        Assert.Equal("Alice", result.StudentName);
        var program = _db.Programs.Items.Single();
        Assert.Equal(1, program.TotalReviews);
        Assert.Equal(5.0m, program.Rating);
    }

    [Fact]
    public async Task CreateReview_Throws_WhenInvalidRatingOrNotEnrolled()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedProgram();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateReviewAsync(_programId, new CreateProgramReviewDto { StarRating = 0 }));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CreateReviewAsync(_programId, new CreateProgramReviewDto { StarRating = 4 }));
    }

    [Fact]
    public async Task CreateReview_Throws_WhenDuplicateOrProgramMissing()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedProgram();
        SeedEnrollment(_studentId);
        SeedReview();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateReviewAsync(_programId, new CreateProgramReviewDto { StarRating = 3 }));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateReviewAsync(_otherProgramId, new CreateProgramReviewDto { StarRating = 3 }));
    }

    [Fact]
    public async Task GetReviewsByProgram_ReturnsPagedSorted()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001", "Alice");
        SeedUser(_otherStudentId, RoleType.Student, "STD-002", "Bob");
        SeedProgram();
        SeedReview(starRating: 3, createdAt: _now.AddDays(-2));
        SeedReview(id: _otherReviewId, studentId: _otherStudentId, starRating: 5, createdAt: _now);
        var sut = CreateSut();

        var byDate = await sut.GetReviewsByProgramAsync(_programId, 1, 10, null, false);
        var byStars = await sut.GetReviewsByProgramAsync(_programId, 1, 10, "starrating", true);

        Assert.Equal(2, byDate.TotalCount);
        Assert.Equal(_reviewId, byDate.Items[0].Id);
        Assert.Equal(5, byStars.Items[0].StarRating);
    }

    [Fact]
    public async Task GetReviewsByProgram_ReturnsEmpty_WhenNone()
    {
        SeedProgram();
        var sut = CreateSut();

        var result = await sut.GetReviewsByProgramAsync(_programId, 1, 10, null, false);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task UpdateReview_UpdatesOwnerOnly()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedProgram();
        SeedReview();
        var sut = CreateSut();

        var result = await sut.UpdateReviewAsync(_programId, _reviewId, new UpdateProgramReviewDto
        {
            StarRating = 2,
            Comment = "Updated",
        });

        Assert.Equal(2, result.StarRating);
        Assert.Equal(2.0m, _db.Programs.Items.Single().Rating);
    }

    [Fact]
    public async Task UpdateReview_Throws_WhenNotOwnerOrWrongProgram()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedUser(_otherStudentId, RoleType.Student, "STD-002");
        SeedProgram();
        SeedReview();
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateReviewAsync(_programId, _reviewId, new UpdateProgramReviewDto { StarRating = 1 }));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateReviewAsync(_otherProgramId, _reviewId, new UpdateProgramReviewDto { StarRating = 1 }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateSut().UpdateReviewAsync(_programId, _reviewId, new UpdateProgramReviewDto { StarRating = 6 }));
    }

    [Fact]
    public async Task DeleteReview_AllowsOwnerAndManager()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedProgram();
        SeedReview();
        SeedReview(id: _otherReviewId, starRating: 5);

        Assert.True(await CreateSut().DeleteReviewAsync(_programId, _reviewId));
        Assert.True(_db.ProgramReviews.Items.Single(r => r.Id == _reviewId).IsDeleted);
        Assert.Equal(1, _db.Programs.Items.Single().TotalReviews);

        Assert.True(await CreateSut(_managerId).DeleteReviewAsync(_programId, _otherReviewId));
    }

    [Fact]
    public async Task DeleteReview_Throws_WhenForbiddenOrMissing()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedUser(_otherStudentId, RoleType.Student, "STD-002");
        SeedProgram();
        SeedReview();
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.DeleteReviewAsync(_programId, _reviewId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.DeleteReviewAsync(_programId, Guid.NewGuid()));
    }
}
