using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class StudentLoadValidatorTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly InMemoryUnitOfWork _db = new();

    [Fact]
    public async Task ValidateUnderPrimaryClassLoad_AllowsWhenUnderCap()
    {
        SeedClassEnrollment(ClassEnrollmentKind.Primary);
        SeedClassEnrollment(ClassEnrollmentKind.Retake); // retake does not count

        await StudentLoadValidator.ValidateUnderPrimaryClassLoadAsync(_db, _studentId);
    }

    [Fact]
    public async Task ValidateUnderPrimaryClassLoad_ThrowsWhenAtCap()
    {
        SeedClassEnrollment(ClassEnrollmentKind.Primary);
        SeedClassEnrollment(ClassEnrollmentKind.Primary);

        await Assert.ThrowsAsync<ConflictException>(() =>
            StudentLoadValidator.ValidateUnderPrimaryClassLoadAsync(_db, _studentId));
    }

    [Fact]
    public async Task ValidateUnderRetakeClassLoad_ThrowsWhenAlreadyHasRetake()
    {
        SeedClassEnrollment(ClassEnrollmentKind.Retake);

        await Assert.ThrowsAsync<ConflictException>(() =>
            StudentLoadValidator.ValidateUnderRetakeClassLoadAsync(_db, _studentId));
    }

    private void SeedClassEnrollment(ClassEnrollmentKind kind)
    {
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ClassId = Guid.NewGuid(),
            ProgramEnrollmentId = Guid.NewGuid(),
            Status = ClassEnrollmentStatus.Active,
            Kind = kind,
            IsDeleted = false,
        });
    }
}
