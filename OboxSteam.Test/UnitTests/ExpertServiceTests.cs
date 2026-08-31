using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ExpertDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ExpertServiceTests
{
    private readonly Guid _expertId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _otherExpertId = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherProgramId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _boardId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IBlobService> _blobService = new();

    private ExpertService CreateSut() =>
        new(_db, _blobService.Object, NullLogger<ExpertService>.Instance);

    private static IFormFile CreateAvatarFile(string fileName = "avatar.png", long length = 1024)
    {
        var stream = new MemoryStream(new byte[length]);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        return file.Object;
    }

    private void SeedUser(
        Guid? id = null,
        string code = "USR-001",
        RoleType role = RoleType.Expert,
        string? email = null)
    {
        _db.Users.Seed(new User
        {
            Id = id ?? _userId,
            Code = code,
            Email = email ?? $"{code.ToLower()}@test.com",
            FullName = code,
            Role = role,
            Status = AccountStatus.Active,
            IsEmailVerified = true,
            PasswordHash = new PasswordHasher().HashPassword("Secret1"),
            IsDeleted = false,
        });
    }

    private static CreateExpertRequest NewCreateDto(
        string code = "EXP-NEW",
        string fullName = "External Expert",
        string email = "new.expert@test.com",
        string password = "Secret1",
        List<ExpertProgramAssignmentDto>? programs = null)
        => new()
        {
            Code = code,
            FullName = fullName,
            Email = email,
            Password = password,
            Programs = programs,
        };

    private Program SeedProgram(Guid? id = null, string code = "PRG-001", string name = "STEAM Program")
    {
        var program = new Program
        {
            Id = id ?? _programId,
            Code = code,
            Name = name,
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        };
        _db.Programs.Seed(program);
        return program;
    }

    private Expert SeedExpert(
        Guid? id = null,
        string code = "EXP-001",
        string fullName = "Dr. Ada",
        Guid? userId = null,
        List<ProgramBoard>? boards = null,
        bool isDeleted = false)
    {
        var expert = new Expert
        {
            Id = id ?? _expertId,
            Code = code,
            FullName = fullName,
            Title = "Lead Mentor",
            Organization = "Obox",
            UserId = userId,
            ProgramBoards = boards ?? [],
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            IsDeleted = isDeleted,
        };
        _db.Experts.Seed(expert);
        if (boards is { Count: > 0 })
            _db.ProgramBoards.Seed(boards.ToArray());
        return expert;
    }

    private ProgramBoard SeedBoard(
        Guid expertId,
        Guid programId,
        string? role = "Advisor",
        Guid? boardId = null)
    {
        var board = new ProgramBoard
        {
            Id = boardId ?? _boardId,
            ExpertId = expertId,
            ProgramId = programId,
            RoleInBoard = role,
            IsDeleted = false,
        };
        _db.ProgramBoards.Seed(board);
        return board;
    }

    // ── GetExpertByIdAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsExpertWithPrograms()
    {
        SeedProgram();
        var board = new ProgramBoard
        {
            Id = _boardId,
            ExpertId = _expertId,
            ProgramId = _programId,
            RoleInBoard = "Advisor",
            IsDeleted = false,
        };
        SeedExpert(boards: [board]);
        var sut = CreateSut();

        var result = await sut.GetExpertByIdAsync(_expertId);

        Assert.Equal("Dr. Ada", result.FullName);
        Assert.Single(result.Programs);
        Assert.Equal("STEAM Program", result.Programs[0].Name);
        Assert.Equal("Advisor", result.Programs[0].RoleInBoard);
        Assert.Empty(result.Degrees);
        Assert.Empty(result.Publications);
    }

    [Fact]
    public async Task GetById_IncludesLinkedUserEmail()
    {
        SeedUser(email: "ada@test.com");
        SeedProgram();
        SeedExpert(userId: _userId, boards:
        [
            new ProgramBoard
            {
                Id = _boardId,
                ExpertId = _expertId,
                ProgramId = _programId,
                RoleInBoard = "Advisor",
                IsDeleted = false,
            },
        ]);
        var sut = CreateSut();

        var result = await sut.GetExpertByIdAsync(_expertId);

        Assert.Equal("ada@test.com", result.Email);
        Assert.Equal(_userId, result.UserId);
    }

    [Fact]
    public async Task GetById_Throws_WhenMissingOrDeleted()
    {
        SeedExpert(isDeleted: true);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetExpertByIdAsync(_expertId));
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetExpertByIdAsync(Guid.NewGuid()));
    }

    // ── GetAllExpertsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsFilteredSortedPageWithPrograms()
    {
        SeedProgram();
        SeedExpert(boards:
        [
            new ProgramBoard
            {
                Id = _boardId,
                ExpertId = _expertId,
                ProgramId = _programId,
                RoleInBoard = "Chair",
                IsDeleted = false,
            },
        ]);
        SeedExpert(id: _otherExpertId, code: "EXP-002", fullName: "Other Expert");
        var sut = CreateSut();

        var result = await sut.GetAllExpertsAsync(
            "ada", "fullname", false, 1, 10, code: "exp");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Dr. Ada", result.Items[0].FullName);
        Assert.Single(result.Items[0].Programs);
    }

    [Fact]
    public async Task GetAll_AppliesAlternateSortColumns()
    {
        var older = SeedExpert(code: "EXP-A", fullName: "Alpha");
        older.CreatedAt = DateTime.UtcNow.AddDays(-10);
        var newer = SeedExpert(id: _otherExpertId, code: "EXP-B", fullName: "Beta");
        newer.CreatedAt = DateTime.UtcNow.AddDays(-1);
        var sut = CreateSut();

        var byCode = await sut.GetAllExpertsAsync(null, "code", true, 1, 10);
        var byFullName = await sut.GetAllExpertsAsync(null, "fullname", false, 1, 10);
        var byCreatedAt = await sut.GetAllExpertsAsync(null, "createdat", true, 1, 10);
        var byDefault = await sut.GetAllExpertsAsync(null, null, false, 1, 10);

        Assert.Equal("EXP-B", byCode.Items[0].Code);
        Assert.Equal("Beta", byCreatedAt.Items[0].FullName);
        Assert.Equal("Alpha", byDefault.Items[0].FullName);
    }

    // ── AddExpertAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_ProvisionsExpertLoginWithoutPrograms()
    {
        var sut = CreateSut();

        var result = await sut.AddExpertAsync(new CreateExpertRequest
        {
            Code = "EXP-NEW",
            FullName = "External Expert",
            Title = "PhD",
            Email = "new.expert@test.com",
            Password = "Secret1",
        });

        Assert.Equal("EXP-NEW", result.Code);
        Assert.NotNull(result.UserId);
        Assert.Equal("new.expert@test.com", result.Email);
        Assert.Empty(result.Programs);
        Assert.Single(_db.Experts.Items);
        Assert.Single(_db.Users.Items);
        var user = _db.Users.Items[0];
        Assert.Equal(RoleType.Expert, user.Role);
        Assert.True(user.IsEmailVerified);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.True(new PasswordHasher().VerifyPassword("Secret1", user.PasswordHash));
    }

    [Fact]
    public async Task Add_PersistsExpertWithProgramsAndProvisionedUser()
    {
        SeedProgram();
        SeedProgram(_otherProgramId, "PRG-002", "Advanced");
        var sut = CreateSut();

        var result = await sut.AddExpertAsync(new CreateExpertRequest
        {
            Code = "EXP-LINKED",
            FullName = "Linked Expert",
            Email = "linked.expert@test.com",
            Password = "Secret1",
            Programs =
            [
                new ExpertProgramAssignmentDto { ProgramId = _programId, RoleInBoard = "Advisor" },
                new ExpertProgramAssignmentDto { ProgramId = _programId, RoleInBoard = "Dup ignored" },
                new ExpertProgramAssignmentDto { ProgramId = _otherProgramId, RoleInBoard = "Member" },
            ],
        });

        Assert.NotNull(result.UserId);
        Assert.Equal("linked.expert@test.com", result.Email);
        Assert.Equal(2, result.Programs.Count);
        Assert.Equal(2, _db.ProgramBoards.Items.Count);
        Assert.Equal(RoleType.Expert, _db.Users.Items[0].Role);
    }

    [Fact]
    public async Task Add_Throws_WhenCodeDuplicate()
    {
        SeedExpert();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.AddExpertAsync(NewCreateDto(code: "exp-001", fullName: "Dup")));
    }

    [Fact]
    public async Task Add_Throws_WhenEmailTakenOrCredentialsMissing()
    {
        SeedUser(email: "taken@test.com");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.AddExpertAsync(NewCreateDto(email: "taken@test.com")));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.AddExpertAsync(NewCreateDto(email: " ")));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.AddExpertAsync(NewCreateDto(password: "123")));
    }

    [Fact]
    public async Task Add_Throws_WhenProgramMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.AddExpertAsync(NewCreateDto(
                fullName: "Expert",
                programs:
                [
                    new ExpertProgramAssignmentDto { ProgramId = _programId, RoleInBoard = "Advisor" },
                ])));
    }

    // ── AddProgramToExpertAsync ───────────────────────────────────────────────

    [Fact]
    public async Task AddProgram_AssignsProgramToExpert()
    {
        SeedExpert();
        SeedProgram();
        var sut = CreateSut();

        var result = await sut.AddProgramToExpertAsync(
            _expertId,
            _programId,
            new AddProgramToExpertDto { RoleInBoard = "Chair" });

        Assert.Equal(_programId, result.ProgramId);
        Assert.Equal("Chair", result.RoleInBoard);
        Assert.Single(_db.ProgramBoards.Items);
    }

    [Fact]
    public async Task AddProgram_Throws_WhenExpertOrProgramMissingOrAlreadyAssigned()
    {
        SeedExpert();
        SeedProgram();
        SeedBoard(_expertId, _programId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.AddProgramToExpertAsync(Guid.NewGuid(), _programId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.AddProgramToExpertAsync(_expertId, Guid.NewGuid()));
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.AddProgramToExpertAsync(_expertId, _programId));
    }

    // ── UpdateExpertAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Update_AppliesProfileChanges()
    {
        SeedUser();
        SeedExpert(userId: _userId);
        var sut = CreateSut();

        var result = await sut.UpdateExpertAsync(_expertId, new UpdateExpertRequest
        {
            FullName = "Updated Ada",
            Title = "Director",
            Bio = "Updated bio",
        });

        Assert.Equal("Updated Ada", result.FullName);
        Assert.Equal("Director", _db.Experts.Items[0].Title);
        Assert.Equal("Updated Ada", _db.Users.Items[0].FullName);
    }

    [Fact]
    public async Task Update_ReturnsUnchanged_WhenNoChanges()
    {
        SeedExpert();
        var before = _db.SaveChangesCallCount;
        var sut = CreateSut();

        var result = await sut.UpdateExpertAsync(_expertId, new UpdateExpertRequest());

        Assert.Equal("Dr. Ada", result.FullName);
        Assert.Equal(before, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_ReplacesProgramAssignments()
    {
        SeedProgram();
        SeedProgram(_otherProgramId, "PRG-002", "Advanced");
        SeedExpert(boards:
        [
            new ProgramBoard
            {
                Id = _boardId,
                ExpertId = _expertId,
                ProgramId = _programId,
                RoleInBoard = "Old",
                IsDeleted = false,
            },
        ]);
        var sut = CreateSut();

        var result = await sut.UpdateExpertAsync(_expertId, new UpdateExpertRequest
        {
            Programs =
            [
                new ExpertProgramAssignmentDto
                {
                    ProgramId = _otherProgramId,
                    RoleInBoard = "New Role",
                },
            ],
        });

        Assert.Single(result.Programs);
        Assert.Equal(_otherProgramId, result.Programs[0].ProgramId);
        Assert.Equal("New Role", result.Programs[0].RoleInBoard);
        Assert.DoesNotContain(_db.ProgramBoards.Items, b => b.Id == _boardId);
    }

    [Fact]
    public async Task Update_ClearsPrograms_WhenEmptyListProvided()
    {
        SeedProgram();
        SeedExpert(boards:
        [
            new ProgramBoard
            {
                Id = _boardId,
                ExpertId = _expertId,
                ProgramId = _programId,
                RoleInBoard = "Advisor",
                IsDeleted = false,
            },
        ]);
        var sut = CreateSut();

        var result = await sut.UpdateExpertAsync(_expertId, new UpdateExpertRequest
        {
            Programs = [],
        });

        Assert.Empty(result.Programs);
        Assert.Empty(_db.ProgramBoards.Items);
    }

    [Fact]
    public async Task Update_Throws_WhenMissingOrDuplicateCode()
    {
        SeedExpert();
        SeedExpert(id: _otherExpertId, code: "EXP-002", fullName: "Other");
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateExpertAsync(Guid.NewGuid(), new UpdateExpertRequest { FullName = "X" }));
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateExpertAsync(_expertId, new UpdateExpertRequest { Code = "EXP-002" }));
    }

    [Fact]
    public async Task Update_Throws_WhenProgramAssignmentMissing()
    {
        SeedExpert();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateExpertAsync(_expertId, new UpdateExpertRequest
            {
                Programs =
                [
                    new ExpertProgramAssignmentDto { ProgramId = _programId },
                ],
            }));
    }

    [Fact]
    public async Task Update_DoesNotChangeLoginCredentials()
    {
        SeedUser();
        SeedExpert(userId: _userId);
        var sut = CreateSut();

        var result = await sut.UpdateExpertAsync(_expertId, new UpdateExpertRequest
        {
            Title = "Director",
        });

        Assert.Equal(_userId, result.UserId);
        Assert.Equal("usr-001@test.com", result.Email);
        Assert.Equal(AccountStatus.Active, _db.Users.Items[0].Status);
    }

    // ── UpdateProgramOfExpertAsync ────────────────────────────────────────────

    [Fact]
    public async Task UpdateProgram_ReturnsAssignmentSummary()
    {
        SeedExpert();
        SeedProgram();
        SeedBoard(_expertId, _programId, "Advisor");
        var sut = CreateSut();

        var result = await sut.UpdateProgramOfExpertAsync(_expertId, _programId);

        Assert.Equal(_programId, result.ProgramId);
        Assert.Equal("Advisor", result.RoleInBoard);
        Assert.Equal("STEAM Program", result.Name);
    }

    [Fact]
    public async Task UpdateProgram_Throws_WhenExpertProgramOrAssignmentMissing()
    {
        SeedExpert();
        SeedProgram();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateProgramOfExpertAsync(Guid.NewGuid(), _programId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateProgramOfExpertAsync(_expertId, Guid.NewGuid()));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateProgramOfExpertAsync(_expertId, _programId));
    }

    // ── RemoveProgramFromExpertAsync ──────────────────────────────────────────

    [Fact]
    public async Task RemoveProgram_HardDeletesAssignment()
    {
        SeedExpert();
        SeedProgram();
        SeedBoard(_expertId, _programId);
        var sut = CreateSut();

        var removed = await sut.RemoveProgramFromExpertAsync(_expertId, _programId);

        Assert.True(removed);
        Assert.Empty(_db.ProgramBoards.Items);
    }

    [Fact]
    public async Task RemoveProgram_Throws_WhenExpertProgramOrAssignmentMissing()
    {
        SeedExpert();
        SeedProgram();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.RemoveProgramFromExpertAsync(Guid.NewGuid(), _programId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.RemoveProgramFromExpertAsync(_expertId, Guid.NewGuid()));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.RemoveProgramFromExpertAsync(_expertId, _programId));
    }

    // ── DeleteExpertAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SoftDeletesExpert()
    {
        SeedExpert();
        var sut = CreateSut();

        var deleted = await sut.DeleteExpertAsync(_expertId);

        Assert.True(deleted);
        Assert.True(_db.Experts.Items[0].IsDeleted);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task Delete_LocksLinkedUser()
    {
        SeedUser();
        SeedExpert(userId: _userId);
        var sut = CreateSut();

        await sut.DeleteExpertAsync(_expertId);

        Assert.True(_db.Experts.Items[0].IsDeleted);
        Assert.Equal(AccountStatus.Locked, _db.Users.Items[0].Status);
        Assert.False(_db.Users.Items[0].IsDeleted);
    }

    [Fact]
    public async Task Delete_Throws_WhenMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteExpertAsync(_expertId));
    }

    [Fact]
    public async Task Add_PersistsSpecializationTags()
    {
        var sut = CreateSut();

        var result = await sut.AddExpertAsync(new CreateExpertRequest
        {
            Code = "EXP-SPEC",
            FullName = "Spec Expert",
            Email = "spec.expert@test.com",
            Password = "Secret1",
            Specialization = ["Robotics", "AI"],
        });

        Assert.Equal(["Robotics", "AI"], result.Specialization);
    }

    [Fact]
    public async Task DegreeAndPublication_Crud_AndPublicProfileIncludesThem()
    {
        SeedExpert();
        var sut = CreateSut();

        var degree = await sut.AddDegreeAsync(_expertId, new ExpertDegreeRequestDto
        {
            Title = "PhD",
            Institution = "MIT",
            Year = 2018,
        });
        var publication = await sut.AddPublicationAsync(_expertId, new ExpertPublicationRequestDto
        {
            Title = "Kids and robots",
            Venue = "STEAM Conf",
            Year = 2022,
            Url = "https://example.com/paper",
        });

        var profile = await sut.GetExpertByIdAsync(_expertId);
        Assert.Single(profile.Degrees);
        Assert.Equal("PhD", profile.Degrees[0].Title);
        Assert.Single(profile.Publications);
        Assert.Equal("Kids and robots", profile.Publications[0].Title);

        var updatedDegree = await sut.UpdateDegreeAsync(_expertId, degree.Id, new ExpertDegreeRequestDto
        {
            Title = "Ph.D. Robotics",
            Institution = "MIT",
            Year = 2019,
        });
        Assert.Equal("Ph.D. Robotics", updatedDegree.Title);

        var updatedPub = await sut.UpdatePublicationAsync(_expertId, publication.Id, new ExpertPublicationRequestDto
        {
            Title = "Kids and robots (revised)",
            Venue = "STEAM Conf",
            Year = 2023,
        });
        Assert.Equal(2023, updatedPub.Year);

        await sut.DeleteDegreeAsync(_expertId, degree.Id);
        await sut.DeletePublicationAsync(_expertId, publication.Id);

        var afterDelete = await sut.GetExpertByIdAsync(_expertId);
        Assert.Empty(afterDelete.Degrees);
        Assert.Empty(afterDelete.Publications);
    }

    [Fact]
    public async Task AddDegree_Throws_WhenYearOutOfRange()
    {
        SeedExpert();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.AddDegreeAsync(_expertId, new ExpertDegreeRequestDto
            {
                Title = "BSc",
                Institution = "Uni",
                Year = 1900,
            }));
    }

    [Fact]
    public async Task UploadAvatar_UploadsAndReplacesOldFile()
    {
        SeedExpert();
        _db.Experts.Items[0].AvatarUrl = "https://cdn.test/old.png";
        _blobService
            .Setup(b => b.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://cdn.test/avatars/new.png");

        var sut = CreateSut();

        var result = await sut.UploadAvatarAsync(_expertId, CreateAvatarFile());

        Assert.Equal("https://cdn.test/avatars/new.png", result.AvatarUrl);
        _blobService.Verify(b => b.DeleteFileAsync("https://cdn.test/old.png", It.IsAny<CancellationToken>()), Times.Once);
        _blobService.Verify(
            b => b.UploadFileAsync(
                It.Is<string>(n => n.StartsWith($"{_expertId}_")),
                It.IsAny<Stream>(),
                "avatars",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAvatar_Throws_WhenInvalidFileOrMissingExpert()
    {
        SeedExpert();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadAvatarAsync(_expertId, CreateAvatarFile("doc.pdf")));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UploadAvatarAsync(_expertId, CreateAvatarFile(length: 6 * 1024 * 1024)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UploadAvatarAsync(Guid.NewGuid(), CreateAvatarFile()));
    }
}
