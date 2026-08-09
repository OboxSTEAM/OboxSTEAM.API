using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.MentorDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class MentorServiceTests
{
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _otherMentorId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _parentId = Guid.Parse("16161616-1616-1616-1616-161616161616");
    private readonly Guid _skillId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _otherSkillId = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _mentorSkillId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _requestId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private MentorService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _mentorId);
        return new MentorService(
            _db,
            _claimsService.Object,
            NullLogger<MentorService>.Instance);
    }

    private void SeedUser(
        Guid id,
        RoleType role,
        string code,
        string? fullName = null,
        string? email = null,
        int? maxConcurrent = null,
        bool isDeleted = false)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = email ?? $"{code.ToLower()}@test.com",
            FullName = fullName ?? code,
            Role = role,
            Status = AccountStatus.Active,
            MaxConcurrentClasses = maxConcurrent,
            IsDeleted = isDeleted,
        });
    }

    private Skill SeedSkill(Guid? id = null, string code = "SKL-001", string name = "Robotics")
    {
        var skill = new Skill
        {
            Id = id ?? _skillId,
            Code = code,
            Name = name,
            Category = SkillCategory.Technology,
            Subcategory = "Hardware",
            IsDeleted = false,
        };
        _db.Skills.Seed(skill);
        return skill;
    }

    private MentorSkill SeedMentorSkill(
        Guid? id = null,
        Guid? mentorId = null,
        Guid? skillId = null,
        bool isDeleted = false,
        bool isPublic = true,
        int years = 3,
        string? description = "Builds robots with teens")
    {
        var entity = new MentorSkill
        {
            Id = id ?? _mentorSkillId,
            MentorId = mentorId ?? _mentorId,
            SkillId = skillId ?? _skillId,
            ProficiencyLevel = SkillProficiencyLevel.Intermediate,
            YearsOfExperience = years,
            Description = description,
            Notes = "Hands-on",
            IsPublic = isPublic,
            IsDeleted = isDeleted,
        };
        _db.MentorSkills.Seed(entity);
        return entity;
    }

    private void SeedProgram()
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
    }

    private void SeedAssignedClass(Guid mentorId, ClassStatus status = ClassStatus.Open)
    {
        SeedProgram();
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = mentorId,
            Status = status,
            MaxCapacity = 20,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(30),
            IsDeleted = false,
        });
    }

    private void SeedPendingRequest(Guid mentorId)
    {
        _db.ClassMentorRequests.Seed(new ClassMentorRequest
        {
            Id = _requestId,
            ClassId = Guid.Parse("48484848-4848-4848-4848-484848484848"),
            MentorId = mentorId,
            Status = ClassMentorRequestStatus.Pending,
            IsDeleted = false,
        });
    }

    // ── GetMySkillsAsync / AddMySkillAsync / RemoveMySkillAsync ───────────────

    [Fact]
    public async Task GetMySkills_ReturnsOrderedSkills()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill(_skillId, "SKL-B", "Zebra");
        SeedSkill(_otherSkillId, "SKL-A", "Alpha");
        SeedMentorSkill(skillId: _skillId);
        SeedMentorSkill(
            id: Guid.Parse("67676767-6767-6767-6767-676767676767"),
            skillId: _otherSkillId);
        var sut = CreateSut();

        var result = await sut.GetMySkillsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].Skill.Name);
        Assert.Equal("Zebra", result[1].Skill.Name);
    }

    [Fact]
    public async Task GetMySkills_Throws_WhenCallerNotMentor()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetMySkillsAsync());
    }

    [Fact]
    public async Task GetMySkills_Throws_WhenUnauthorizedOrUserMissing()
    {
        var sutEmpty = CreateSut(Guid.Empty);
        await Assert.ThrowsAsync<UnauthorizedException>(() => sutEmpty.GetMySkillsAsync());

        var sutMissing = CreateSut(_mentorId);
        await Assert.ThrowsAsync<NotFoundException>(() => sutMissing.GetMySkillsAsync());
    }

    [Fact]
    public async Task AddMySkill_PersistsSkill()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill();
        var sut = CreateSut();

        var result = await sut.AddMySkillAsync(new CreateMentorSkillRequestDto
        {
            SkillId = _skillId,
            ProficiencyLevel = SkillProficiencyLevel.Advanced,
            YearsOfExperience = 5,
            Description = "  Leads robotics labs  ",
            Notes = "  Solid  ",
            IsPublic = true,
            Evidences =
            [
                new MentorSkillEvidenceRequestDto
                {
                    Title = " Robotics Cert ",
                    Issuer = " IEEE ",
                    Url = "https://example.com/cert/1",
                    IssuedAt = DateTime.UtcNow.AddYears(-1),
                    CredentialId = " CERT-1 ",
                },
            ],
        });

        Assert.Equal(_skillId, result.SkillId);
        Assert.Equal("Robotics", result.Skill.Name);
        Assert.Equal(SkillProficiencyLevel.Advanced, result.ProficiencyLevel);
        Assert.Equal(5, result.YearsOfExperience);
        Assert.Equal("Leads robotics labs", result.Description);
        Assert.Equal("Solid", result.Notes);
        Assert.True(result.IsPublic);
        Assert.Single(result.Evidences);
        Assert.Equal("Robotics Cert", result.Evidences[0].Title);
        Assert.Equal("IEEE", result.Evidences[0].Issuer);
        Assert.Equal("https://example.com/cert/1", result.Evidences[0].Url);
        Assert.Equal("CERT-1", result.Evidences[0].CredentialId);
        Assert.Single(_db.MentorSkills.Items);
        Assert.Single(_db.MentorSkillEvidences.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task AddMySkill_StoresNullNotes_WhenBlank()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill();
        var sut = CreateSut();

        var result = await sut.AddMySkillAsync(new CreateMentorSkillRequestDto
        {
            SkillId = _skillId,
            Notes = "   ",
        });

        Assert.Null(result.Notes);
    }

    [Fact]
    public async Task AddMySkill_Throws_WhenSkillMissingOrDuplicate()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill();
        SeedMentorSkill();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.AddMySkillAsync(new CreateMentorSkillRequestDto { SkillId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.AddMySkillAsync(new CreateMentorSkillRequestDto { SkillId = _skillId }));
    }

    [Fact]
    public async Task AddMySkill_Throws_WhenYearsOrEvidenceInvalid()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.AddMySkillAsync(new CreateMentorSkillRequestDto
            {
                SkillId = _skillId,
                YearsOfExperience = 61,
            }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.AddMySkillAsync(new CreateMentorSkillRequestDto
            {
                SkillId = _skillId,
                Evidences =
                [
                    new MentorSkillEvidenceRequestDto
                    {
                        Title = "Cert",
                        Url = "http://insecure.example.com/cert",
                    },
                ],
            }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.AddMySkillAsync(new CreateMentorSkillRequestDto
            {
                SkillId = _skillId,
                Evidences =
                [
                    new MentorSkillEvidenceRequestDto
                    {
                        Title = "Cert",
                        Url = "https://example.com/cert",
                        IssuedAt = DateTime.UtcNow.AddDays(2),
                    },
                ],
            }));
    }

    [Fact]
    public async Task UpdateMySkill_UpdatesFieldsAndReplacesEvidence()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill();
        SeedMentorSkill();
        _db.MentorSkillEvidences.Seed(new MentorSkillEvidence
        {
            Id = Guid.Parse("71717171-7171-7171-7171-717171717171"),
            MentorSkillId = _mentorSkillId,
            Title = "Old",
            Url = "https://example.com/old",
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UpdateMySkillAsync(_mentorSkillId, new UpdateMentorSkillRequestDto
        {
            ProficiencyLevel = SkillProficiencyLevel.Expert,
            YearsOfExperience = 12,
            Description = "Updated description",
            Notes = "  ",
            IsPublic = false,
            Evidences =
            [
                new MentorSkillEvidenceRequestDto
                {
                    Title = "New Cert",
                    Url = "https://example.com/new",
                },
            ],
        });

        Assert.Equal(SkillProficiencyLevel.Expert, result.ProficiencyLevel);
        Assert.Equal(12, result.YearsOfExperience);
        Assert.Equal("Updated description", result.Description);
        Assert.Null(result.Notes);
        Assert.False(result.IsPublic);
        Assert.Single(result.Evidences);
        Assert.Equal("New Cert", result.Evidences[0].Title);
        Assert.True(_db.MentorSkillEvidences.Items.Single(e => e.Title == "Old").IsDeleted);
        Assert.False(_db.MentorSkillEvidences.Items.Single(e => e.Title == "New Cert").IsDeleted);
    }

    [Fact]
    public async Task SetMySkillVisibility_TogglesPublicFlag()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill();
        SeedMentorSkill(isPublic: true);
        var sut = CreateSut();

        var result = await sut.SetMySkillVisibilityAsync(
            _mentorSkillId,
            new UpdateMentorSkillVisibilityRequestDto { IsPublic = false });

        Assert.False(result.IsPublic);
        Assert.False(_db.MentorSkills.Items[0].IsPublic);
    }

    [Fact]
    public async Task RemoveMySkill_SoftDeletesOwnedSkill()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill();
        SeedMentorSkill();
        _db.MentorSkillEvidences.Seed(new MentorSkillEvidence
        {
            Id = Guid.Parse("72727272-7272-7272-7272-727272727272"),
            MentorSkillId = _mentorSkillId,
            Title = "Cert",
            Url = "https://example.com/cert",
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.RemoveMySkillAsync(_mentorSkillId);

        Assert.True(_db.MentorSkills.Items[0].IsDeleted);
        Assert.True(_db.MentorSkillEvidences.Items[0].IsDeleted);
    }

    [Fact]
    public async Task RemoveMySkill_Throws_WhenMissingOrNotOwned()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill();
        SeedMentorSkill(mentorId: _otherMentorId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.RemoveMySkillAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ForbiddenException>(() => sut.RemoveMySkillAsync(_mentorSkillId));
    }

    // ── GetMentorsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetMentors_ReturnsFilteredPage_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Alice Mentor", "alice@test.com");
        SeedUser(_otherMentorId, RoleType.Mentor, "MNT-002", "Bob Mentor");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedSkill();
        SeedMentorSkill();
        SeedAssignedClass(_mentorId);
        SeedPendingRequest(_mentorId);
        var sut = CreateSut(_managerId);

        var result = await sut.GetMentorsAsync("alice", 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Alice Mentor", result.Items[0].FullName);
        Assert.Equal(1, result.Items[0].AssignedClassCount);
        Assert.Equal(1, result.Items[0].PendingRequestCount);
        Assert.Equal(2, result.Items[0].ConcurrentUsage);
        Assert.Equal(MentorRequestConstants.DefaultMaxConcurrentClasses, result.Items[0].EffectiveMaxConcurrentClasses);
        Assert.Single(result.Items[0].Skills);
    }

    [Fact]
    public async Task GetMentors_Throws_WhenPaginationInvalidOrForbidden()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateSut(_managerId).GetMentorsAsync(null, 0, 10));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateSut(_studentId).GetMentorsAsync(null, 1, 10));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateSut(Guid.Empty).GetMentorsAsync(null, 1, 10));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSut(Guid.Parse("99999999-9999-9999-9999-999999999999")).GetMentorsAsync(null, 1, 10));
    }

    [Fact]
    public async Task GetMentors_AllowsAdmin()
    {
        var adminId = Guid.Parse("10101010-1010-1010-1010-101010101010");
        SeedUser(adminId, RoleType.Admin, "ADM-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Mentor");
        var sut = CreateSut(adminId);

        var result = await sut.GetMentorsAsync(null, 1, 10);

        Assert.Equal(1, result.TotalCount);
    }

    // ── GetMentorProfileAsync / GetMyProfileAsync ─────────────────────────────

    [Fact]
    public async Task GetMentorProfile_ReturnsProfile_ForStudent()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Mentor One", maxConcurrent: 5);
        SeedSkill();
        SeedSkill(_otherSkillId, "SKL-002", "Private Soft Skill");
        SeedMentorSkill(isPublic: true);
        SeedMentorSkill(
            id: Guid.Parse("67676767-6767-6767-6767-676767676767"),
            skillId: _otherSkillId,
            isPublic: false);
        _db.MentorProfiles.Seed(new MentorProfile
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            MentorId = _mentorId,
            Title = "Senior Mentor",
            Organization = "Obox",
            IsDeleted = false,
        });
        var sut = CreateSut(_studentId);

        var result = await sut.GetMentorProfileAsync(_mentorId);

        Assert.Equal("Mentor One", result.FullName);
        Assert.Equal(5, result.MaxConcurrentClasses);
        Assert.Equal(5, result.EffectiveMaxConcurrentClasses);
        Assert.Equal("Senior Mentor", result.Title);
        Assert.Single(result.Skills);
        Assert.True(result.Skills[0].IsPublic);
    }

    [Fact]
    public async Task GetMentorProfile_ReturnsPrivateSkills_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Mentor One");
        SeedSkill();
        SeedSkill(_otherSkillId, "SKL-002", "Private Soft Skill");
        SeedMentorSkill(isPublic: true);
        SeedMentorSkill(
            id: Guid.Parse("67676767-6767-6767-6767-676767676767"),
            skillId: _otherSkillId,
            isPublic: false);
        var sut = CreateSut(_managerId);

        var result = await sut.GetMentorProfileAsync(_mentorId);

        Assert.Equal(2, result.Skills.Count);
    }

    [Fact]
    public async Task GetMentorProfile_Throws_WhenTargetNotMentorOrViewerForbidden()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_parentId, RoleType.Parent, "PAR-001");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateSut(_studentId).GetMentorProfileAsync(_managerId));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateSut(_parentId).GetMentorProfileAsync(_managerId));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateSut(Guid.Empty).GetMentorProfileAsync(_managerId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSut(Guid.Parse("99999999-9999-9999-9999-999999999999")).GetMentorProfileAsync(_managerId));
    }

    [Fact]
    public async Task GetMyProfile_ReturnsCurrentMentorProfile()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        var sut = CreateSut();

        var result = await sut.GetMyProfileAsync();

        Assert.Equal(_mentorId, result.Id);
        Assert.Empty(result.Skills);
    }

    // ── UpdateMyProfileAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMyProfile_CreatesProfile_WhenMissing()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        var sut = CreateSut();

        var result = await sut.UpdateMyProfileAsync(new UpdateMentorProfileRequestDto
        {
            Title = "  Lead  ",
            Organization = "Obox",
            Bio = "Bio",
            Achievements = "Awards",
            LinkedInUrl = "https://linkedin.com/in/ada",
        });

        Assert.Equal("Lead", result.Title);
        Assert.Equal("Obox", result.Organization);
        Assert.Single(_db.MentorProfiles.Items);
    }

    [Fact]
    public async Task UpdateMyProfile_UpdatesExistingAndClearsBlankFields()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        _db.MentorProfiles.Seed(new MentorProfile
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            MentorId = _mentorId,
            Title = "Old Title",
            Organization = "Old Org",
            Bio = "Old bio",
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UpdateMyProfileAsync(new UpdateMentorProfileRequestDto
        {
            Title = "New Title",
            Organization = "  ",
            Bio = null,
            Achievements = "  ",
            LinkedInUrl = "  ",
        });

        Assert.Equal("New Title", result.Title);
        Assert.Null(result.Organization);
        Assert.Equal("Old bio", result.Bio);
        Assert.Null(result.Achievements);
        Assert.Null(result.LinkedInUrl);
    }

    // ── SetClassLimitAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SetClassLimit_UpdatesMentorCap_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        var sut = CreateSut(_managerId);

        var result = await sut.SetClassLimitAsync(_mentorId, new UpdateMentorClassLimitRequestDto
        {
            MaxConcurrentClasses = 7,
        });

        Assert.Equal(7, result.MaxConcurrentClasses);
        Assert.Equal(7, result.EffectiveMaxConcurrentClasses);
        Assert.Equal(7, _db.Users.Items.Single(u => u.Id == _mentorId).MaxConcurrentClasses);
    }

    [Fact]
    public async Task SetClassLimit_ClearsOverride_WhenNull()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", maxConcurrent: 5);
        var sut = CreateSut(_managerId);

        var result = await sut.SetClassLimitAsync(_mentorId, new UpdateMentorClassLimitRequestDto
        {
            MaxConcurrentClasses = null,
        });

        Assert.Null(result.MaxConcurrentClasses);
        Assert.Equal(MentorRequestConstants.DefaultMaxConcurrentClasses, result.EffectiveMaxConcurrentClasses);
    }

    [Fact]
    public async Task SetClassLimit_Throws_WhenInvalidLimitOrTargetNotMentor()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SetClassLimitAsync(_mentorId, new UpdateMentorClassLimitRequestDto
            {
                MaxConcurrentClasses = 0,
            }));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.SetClassLimitAsync(_mentorId, new UpdateMentorClassLimitRequestDto
            {
                MaxConcurrentClasses = 2,
            }));
        SeedUser(_studentId, RoleType.Student, "STD-001");
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SetClassLimitAsync(_studentId, new UpdateMentorClassLimitRequestDto
            {
                MaxConcurrentClasses = 2,
            }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SetClassLimitAsync(_managerId, new UpdateMentorClassLimitRequestDto
            {
                MaxConcurrentClasses = 2,
            }));
    }

    [Fact]
    public async Task SetClassLimit_Throws_WhenCallerNotManager()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SetClassLimitAsync(_mentorId, new UpdateMentorClassLimitRequestDto
            {
                MaxConcurrentClasses = 2,
            }));
    }

    [Fact]
    public async Task GetMentors_IgnoresCompletedAssignedClassesInUsage()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Mentor");
        SeedAssignedClass(_mentorId, ClassStatus.Completed);
        var sut = CreateSut(_managerId);

        var result = await sut.GetMentorsAsync(null, 1, 10);

        Assert.Equal(0, result.Items[0].AssignedClassCount);
        Assert.Equal(0, result.Items[0].ConcurrentUsage);
    }
}
