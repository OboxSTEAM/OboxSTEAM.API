using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.CertificateDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class CertificateServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _parentId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _activity1Id = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _activity2Id = Guid.Parse("36363636-3636-3636-3636-363636363636");
    private readonly Guid _programEnrollmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _certificateId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IBlobService> _blobService = new();
    private readonly Mock<ICertificatePdfGenerator> _pdfGenerator = new();

    private CertificateService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://cdn.example.com/certificates/cert.pdf");
        _pdfGenerator
            .Setup(p => p.Generate(It.IsAny<CertificatePdfModel>()))
            .Returns([0x25, 0x50, 0x44, 0x46]);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APP_FRONTEND_URL"] = "https://app.test",
            })
            .Build();

        return new CertificateService(
            _db,
            _claimsService.Object,
            _blobService.Object,
            _pdfGenerator.Object,
            configuration,
            NullLogger<CertificateService>.Instance);
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

    private void SeedProgramCurriculum(string skillsGained = "Robotics, Coding")
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "STEAM Program",
            Description = "Full program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            SkillsGained = skillsGained,
            EstimatedDuration = "3 months",
            ThumbnailUrl = "https://cdn.example.com/thumb.png",
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Intro Module",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            LearningOutcomes = ["Build robots", "Teamwork"],
            IsDeleted = false,
        });
        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Intro Course",
            ModuleId = _moduleId,
            IsDeleted = false,
        });
        _db.Activities.Seed(
            new Activity
            {
                Id = _activity1Id,
                Code = "ACT-001",
                Name = "Lesson 1",
                CourseId = _courseId,
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 1,
                IsDeleted = false,
            },
            new Activity
            {
                Id = _activity2Id,
                Code = "ACT-002",
                Name = "Lesson 2",
                CourseId = _courseId,
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 2,
                IsDeleted = false,
            });
    }

    private void SeedEnrollmentChain(bool allActivitiesDone = true)
    {
        SeedUser(_studentId, RoleType.Student, "STD-001", "Alice Student");
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        });

        if (!allActivitiesDone)
        {
            return;
        }

        foreach (var activityId in new[] { _activity1Id, _activity2Id })
        {
            _db.ActivityProgresses.Seed(new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = _studentId,
                ActivityId = activityId,
                ModuleEnrollmentId = _moduleEnrollmentId,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                IsDeleted = false,
            });
        }
    }

    private Certificate SeedCertificate(
        string code = "OBOX-CERT-EXIST1",
        Guid? studentId = null,
        string? pdfUrl = "https://cdn.example.com/old.pdf")
    {
        var certificate = new Certificate
        {
            Id = _certificateId,
            Code = code,
            StudentId = studentId ?? _studentId,
            ProgramId = _programId,
            ModuleId = null,
            IssueDate = DateTime.UtcNow.AddDays(-1),
            PdfUrl = pdfUrl,
            VerificationUrl = "https://app.test/certificates/verify/OBOX-CERT-EXIST1",
            SkillsAcquired = "Robotics, Coding",
            IsDeleted = false,
        };
        _db.Certificates.Seed(certificate);
        return certificate;
    }

    // ── EnsureProgramCertificateAsync (issue rules) ─────────────────────────

    [Fact]
    public async Task Ensure_IssuesCertificate_WhenAllActivitiesDone()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        var sut = CreateSut();

        var result = await sut.EnsureProgramCertificateAsync(_programEnrollmentId);

        Assert.NotNull(result);
        Assert.StartsWith("OBOX-CERT-", result!.Code);
        Assert.Equal("Alice Student", result.Student.FullName);
        Assert.Equal("STEAM Program", result.Program.Name);
        Assert.Single(result.Modules);
        Assert.Contains("Build robots", result.LearningOutcomes);
        Assert.Equal(["Robotics", "Coding"], result.SkillsGained);
        Assert.Equal("https://cdn.example.com/certificates/cert.pdf", result.PdfUrl);
        Assert.Equal($"https://app.test/certificates/verify/{result.Code}", result.VerificationUrl);
        Assert.Single(_db.Certificates.Items);
        _pdfGenerator.Verify(p => p.Generate(It.IsAny<CertificatePdfModel>()), Times.Once);
        _blobService.Verify(
            b => b.UploadFileAsync(
                It.Is<string>(n => n.EndsWith(".pdf")),
                It.IsAny<Stream>(),
                It.Is<string>(f => f.Contains(_programId.ToString()) && f.Contains(_studentId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _blobService.Verify(b => b.GetPreviewUrlAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Ensure_ReturnsNull_WhenActivitiesIncomplete()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain(allActivitiesDone: false);
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = _activity1Id,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.EnsureProgramCertificateAsync(_programEnrollmentId);

        Assert.Null(result);
        Assert.Empty(_db.Certificates.Items);
        _blobService.Verify(
            b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Ensure_ReturnsNull_WhenNoModules()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-EMPTY",
            Name = "Empty",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.EnsureProgramCertificateAsync(_programEnrollmentId);

        Assert.Null(result);
    }

    [Fact]
    public async Task Ensure_IsIdempotent_WhenCertificateExists()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        SeedCertificate();
        var sut = CreateSut();

        var result = await sut.EnsureProgramCertificateAsync(_programEnrollmentId);

        Assert.NotNull(result);
        Assert.Equal("OBOX-CERT-EXIST1", result!.Code);
        Assert.Single(_db.Certificates.Items);
        _blobService.Verify(
            b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Ensure_Continues_WhenPdfUploadFails()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        var sut = CreateSut();
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("S3 down"));

        var result = await sut.EnsureProgramCertificateAsync(_programEnrollmentId);

        Assert.NotNull(result);
        Assert.StartsWith("OBOX-CERT-", result!.Code);
        Assert.Null(result.PdfUrl);
        Assert.NotNull(result.VerificationUrl);
    }

    [Fact]
    public async Task Ensure_Throws_WhenEnrollmentIdEmpty()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.EnsureProgramCertificateAsync(Guid.Empty));
    }

    [Fact]
    public async Task Ensure_Throws_WhenEnrollmentMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.EnsureProgramCertificateAsync(_programEnrollmentId));
    }

    [Fact]
    public async Task Ensure_Throws_Forbidden_WhenOtherStudentIssues()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        SeedUser(_otherStudentId, RoleType.Student, "STD-002");
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.EnsureProgramCertificateAsync(_programEnrollmentId));
    }

    [Fact]
    public async Task Ensure_AllowsManager_ToIssueForStudent()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        var sut = CreateSut(_managerId);

        var result = await sut.EnsureProgramCertificateAsync(_programEnrollmentId);

        Assert.NotNull(result);
        Assert.Equal(_studentId, _db.Certificates.Items[0].StudentId);
    }

    [Fact]
    public async Task Ensure_AllowsStudent_ToIssueSelf()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        var sut = CreateSut(_studentId);

        var result = await sut.EnsureProgramCertificateAsync(_programEnrollmentId);

        Assert.NotNull(result);
        Assert.Equal(_studentId, result!.Student.Id);
    }

    [Fact]
    public async Task Ensure_ParsesJsonSkillsGained()
    {
        SeedProgramCurriculum(skillsGained: "[\"AI\",\"IoT\"]");
        SeedEnrollmentChain();
        var sut = CreateSut();

        var result = await sut.EnsureProgramCertificateAsync(_programEnrollmentId);

        Assert.NotNull(result);
        Assert.Equal(["AI", "IoT"], result!.SkillsGained);
    }

    // ── GetMyCertificatesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetMy_ReturnsStudentCertificates()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        SeedCertificate();
        var sut = CreateSut();

        var result = await sut.GetMyCertificatesAsync();

        Assert.Single(result);
        Assert.Equal("OBOX-CERT-EXIST1", result[0].Code);
        Assert.Equal("STEAM Program", result[0].ProgramName);
    }

    [Fact]
    public async Task GetMy_ReturnsLinkedStudentCertificates_ForParent()
    {
        SeedUser(_parentId, RoleType.Parent, "PAR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedProgramCurriculum();
        SeedCertificate();
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentId,
            IsDeleted = false,
        });
        var sut = CreateSut(_parentId);

        var result = await sut.GetMyCertificatesAsync();

        Assert.Single(result);
    }

    [Fact]
    public async Task GetMy_Throws_Forbidden_WhenMentor()
    {
        SeedUser(_managerId, RoleType.Mentor, "MNT-001");
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetMyCertificatesAsync());
    }

    // ── GetCertificateByIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsCertificate_ForStudent()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        SeedCertificate();
        var sut = CreateSut();

        var result = await sut.GetCertificateByIdAsync(_certificateId);

        Assert.Equal(_certificateId, result.Id);
        Assert.Equal("OBOX-CERT-EXIST1", result.Code);
        Assert.Equal(CertificateBranding.IssuerName, result.IssuerName);
    }

    [Fact]
    public async Task GetById_Throws_WhenMissingOrModuleCertificate()
    {
        SeedProgramCurriculum();
        SeedCertificate();
        _db.Certificates.Items[0].ModuleId = _moduleId;
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetCertificateByIdAsync(_certificateId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetCertificateByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetById_Throws_Forbidden_WhenOtherStudent()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        SeedCertificate();
        SeedUser(_otherStudentId, RoleType.Student, "STD-002");
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetCertificateByIdAsync(_certificateId));
    }

    // ── GetCertificateByEnrollmentAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetByEnrollment_ReturnsCertificate()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        SeedCertificate();
        var sut = CreateSut();

        var result = await sut.GetCertificateByEnrollmentAsync(_programEnrollmentId);

        Assert.NotNull(result);
        Assert.Equal(_certificateId, result!.Id);
    }

    [Fact]
    public async Task GetByEnrollment_ReturnsNull_WhenNotIssued()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        var sut = CreateSut();

        var result = await sut.GetCertificateByEnrollmentAsync(_programEnrollmentId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEnrollment_Throws_WhenEnrollmentMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetCertificateByEnrollmentAsync(_programEnrollmentId));
    }

    // ── GetCertificateByCodeAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetByCode_ReturnsPublicCertificate()
    {
        SeedProgramCurriculum();
        SeedEnrollmentChain();
        SeedCertificate();
        var sut = CreateSut();

        var result = await sut.GetCertificateByCodeAsync("OBOX-CERT-EXIST1");

        Assert.Equal(_certificateId, result.Id);
        Assert.Equal("Alice Student", result.Student.FullName);
    }

    [Fact]
    public async Task GetByCode_Throws_WhenMissingOrBlank()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.GetCertificateByCodeAsync("  "));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetCertificateByCodeAsync("OBOX-CERT-MISSING"));
    }
}
