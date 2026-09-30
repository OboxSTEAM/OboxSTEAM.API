using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class CurriculumEditGuardTests
{
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Guid _programId = Guid.NewGuid();

    private void SeedProgram(ProgramStatus status)
        => _db.Programs.Seed(new Program { Id = _programId, Code = "PRG", Name = "Program", Status = status });

    [Theory]
    [InlineData(ProgramStatus.Draft)]
    [InlineData(ProgramStatus.Approved)]
    [InlineData(ProgramStatus.Active)]
    [InlineData(ProgramStatus.Inactive)]
    public async Task Curriculum_Allows_WhenNoCohortIsLive(ProgramStatus status)
    {
        SeedProgram(status);

        await CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_db, _programId);
    }

    [Fact]
    public async Task Curriculum_Throws_WhenPendingReview()
    {
        SeedProgram(ProgramStatus.PendingReview);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_db, _programId));
    }

    [Fact]
    public async Task Curriculum_Throws_WhenClassInProgress()
    {
        SeedProgram(ProgramStatus.Draft);
        _db.Classes.Seed(new Class
        {
            Id = Guid.NewGuid(), Code = "CLS", Name = "Cohort", ProgramId = _programId, Status = ClassStatus.InProgress,
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_db, _programId));
    }

    [Fact]
    public async Task Curriculum_Throws_WhenOpenClassHasActiveEnrollment()
    {
        SeedProgram(ProgramStatus.Active);
        var classId = Guid.NewGuid();
        _db.Classes.Seed(new Class
        {
            Id = classId, Code = "CLS", Name = "Cohort", ProgramId = _programId, Status = ClassStatus.Open,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(), ClassId = classId, StudentId = Guid.NewGuid(), Status = ClassEnrollmentStatus.Active,
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_db, _programId));
    }

    [Fact]
    public async Task Program_Throws_WhenClassInProgress()
    {
        SeedProgram(ProgramStatus.Active);
        _db.Classes.Seed(new Class
        {
            Id = Guid.NewGuid(), Code = "CLS", Name = "Cohort", ProgramId = _programId, Status = ClassStatus.InProgress,
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            CurriculumEditGuard.EnsureProgramEditableAsync(_db, _programId));
    }

    [Theory]
    [InlineData(ProgramStatus.Active)]
    [InlineData(ProgramStatus.Approved)]
    public async Task Program_Allows_ActiveAndApproved(ProgramStatus status)
    {
        SeedProgram(status);

        await CurriculumEditGuard.EnsureProgramEditableAsync(_db, _programId);
    }
}
