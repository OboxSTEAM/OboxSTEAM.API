using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.PortfolioDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class PortfolioServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _portfolioId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _itemId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _autoItemId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _sectionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _builtInSectionId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _mediaId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<IBlobService> _blobService = new();
    private readonly Mock<IPortfolioHtmlSanitizer> _htmlSanitizer = new();

    public PortfolioServiceTests()
    {
        _htmlSanitizer
            .Setup(s => s.Sanitize(It.IsAny<string?>()))
            .Returns((string? x) => x);
    }

    private PortfolioService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _blobService.Setup(b => b.BucketName).Returns("obox-bucket");
        _blobService
            .Setup(b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.GetPreviewUrlAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => $"https://cdn.example.com/{key}");
        _blobService
            .Setup(b => b.DeleteByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobService
            .Setup(b => b.CopyObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new PortfolioService(
            _db,
            _claimsService.Object,
            _blobService.Object,
            _htmlSanitizer.Object,
            NullLogger<PortfolioService>.Instance);
    }

    private User SeedStudent(Guid? id = null, string code = "STU-001")
    {
        var student = new User
        {
            Id = id ?? _studentId,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = "Test Student",
            Role = RoleType.Student,
            Status = AccountStatus.Active,
            IsDeleted = false,
        };
        _db.Users.Seed(student);
        return student;
    }

    private Portfolio SeedPortfolio(
        Guid? id = null,
        Guid? studentId = null,
        string? subdomain = null,
        bool isPublic = false,
        string? publishedSnapshot = null,
        User? student = null)
    {
        var ownerId = studentId ?? _studentId;
        var owner = student ?? _db.Users.Items.FirstOrDefault(u => u.Id == ownerId);
        var portfolio = new Portfolio
        {
            Id = id ?? _portfolioId,
            Code = "OBOX-PF-SEED01",
            StudentId = ownerId,
            Student = owner!,
            Subdomain = subdomain,
            IsPublic = isPublic,
            PublishedSnapshot = publishedSnapshot,
            IsDeleted = false,
        };
        _db.Portfolios.Seed(portfolio);
        return portfolio;
    }

    private void SeedBuiltInSections(Guid portfolioId)
    {
        _db.PortfolioSections.Seed(
            new PortfolioSection
            {
                Id = _builtInSectionId,
                PortfolioId = portfolioId,
                Kind = PortfolioSectionKind.ProjectsGroup,
                Title = "Projects",
                DisplayOrder = 0,
                IsVisible = true,
                IsDeleted = false,
            },
            new PortfolioSection
            {
                Id = Guid.Parse("46464646-4646-4646-4646-464646464646"),
                PortfolioId = portfolioId,
                Kind = PortfolioSectionKind.ActivitiesGroup,
                Title = "Activities",
                DisplayOrder = 1,
                IsVisible = true,
                IsDeleted = false,
            },
            new PortfolioSection
            {
                Id = Guid.Parse("47474747-4747-4747-4747-474747474747"),
                PortfolioId = portfolioId,
                Kind = PortfolioSectionKind.LinksGroup,
                Title = "Links",
                DisplayOrder = 2,
                IsVisible = true,
                IsDeleted = false,
            });
    }

    private static Mock<IFormFile> CreateImageFile(
        string fileName = "photo.jpg",
        long length = 1024,
        string contentType = "image/jpeg")
        => CreateMediaFile(fileName, length, contentType);

    private static Mock<IFormFile> CreateMediaFile(
        string fileName,
        long length,
        string contentType)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([0x00, 0x00, 0x00, 0x18]));
        return file;
    }

    // ── CreateMyPortfolioAsync / GetMyPortfolioAsync ───────────────────────────

    [Fact]
    public async Task CreateMyPortfolioAsync_CreatesPortfolioWithBuiltInSections()
    {
        SeedStudent();
        var sut = CreateSut();

        var result = await sut.CreateMyPortfolioAsync();

        Assert.Single(_db.Portfolios.Items);
        Assert.StartsWith("OBOX-PF-", result.Code);
        Assert.Equal(_studentId, result.StudentId);
        Assert.False(result.IsPublic);
        Assert.Equal(3, result.Sections.Count);
        Assert.Contains(result.Sections, s => s.Kind == PortfolioSectionKind.ProjectsGroup);
        Assert.Contains(result.Sections, s => s.Kind == PortfolioSectionKind.ActivitiesGroup);
        Assert.Contains(result.Sections, s => s.Kind == PortfolioSectionKind.LinksGroup);
    }

    [Fact]
    public async Task CreateMyPortfolioAsync_ThrowsConflict_WhenPortfolioAlreadyExists()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() => sut.CreateMyPortfolioAsync());
    }

    [Fact]
    public async Task GetMyPortfolioAsync_ReturnsExistingPortfolio()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        SeedBuiltInSections(_portfolioId);
        var sut = CreateSut();

        var result = await sut.GetMyPortfolioAsync();

        Assert.Equal(_portfolioId, result.Id);
        Assert.Equal("Test Student", result.StudentName);
        Assert.Equal(3, result.Sections.Count);
    }

    [Fact]
    public async Task GetMyPortfolioAsync_ThrowsForbidden_ForNonStudent()
    {
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-001",
            Email = "mentor@test.com",
            FullName = "Mentor",
            Role = RoleType.Mentor,
            Status = AccountStatus.Active,
            IsDeleted = false,
        });
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetMyPortfolioAsync());
    }

    // ── UpdateMyPortfolioAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMyPortfolioAsync_UpdatesDisplayFieldsAndTheme()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        SeedBuiltInSections(_portfolioId);
        var sut = CreateSut();

        var result = await sut.UpdateMyPortfolioAsync(new UpdatePortfolioRequestDto
        {
            DisplayName = "Jane Doe",
            Headline = "Future Engineer",
            Tagline = "STEAM enthusiast",
            Summary = "<p>About me</p>",
            Theme = new ThemeConfigDto
            {
                TemplateId = "modern",
                PrimaryColor = "#1A2B3C",
            },
            Links =
            [
                new PortfolioLinkDto { Label = "GitHub", Url = "https://github.com/jane" },
            ],
        });

        Assert.Equal("Jane Doe", result.DisplayName);
        Assert.Equal("Future Engineer", result.Headline);
        Assert.Equal("STEAM enthusiast", result.Tagline);
        Assert.Equal("<p>About me</p>", result.Summary);
        Assert.Equal("modern", result.Theme?.TemplateId);
        Assert.Equal("#1A2B3C", result.Theme?.PrimaryColor);
        Assert.Single(result.Links);
        Assert.Equal("GitHub", result.Links[0].Label);
    }

    // ── Subdomain ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckSubdomainAvailabilityAsync_ReturnsAvailable_WhenNotTaken()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        var result = await sut.CheckSubdomainAvailabilityAsync("jane-portfolio");

        Assert.True(result.Available);
        Assert.Equal("jane-portfolio", result.Subdomain);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task CheckSubdomainAvailabilityAsync_ReturnsUnavailable_WhenTakenByAnotherPortfolio()
    {
        SeedStudent();
        SeedPortfolio(subdomain: "taken-name");
        SeedStudent(_otherStudentId, "STU-002");
        SeedPortfolio(
            id: Guid.Parse("23232323-2323-2323-2323-232323232323"),
            studentId: _otherStudentId);
        var sut = CreateSut(_otherStudentId);

        var result = await sut.CheckSubdomainAvailabilityAsync("taken-name");

        Assert.False(result.Available);
        Assert.Equal("This subdomain is already taken.", result.Reason);
    }

    [Fact]
    public async Task UpdateMySubdomainAsync_SetsNormalizedSubdomain()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        var result = await sut.UpdateMySubdomainAsync(new UpdatePortfolioSubdomainRequestDto
        {
            Subdomain = "Jane-Portfolio",
        });

        Assert.Equal("jane-portfolio", result.Subdomain);
    }

    // ── Publication ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMyPublicationAsync_ThrowsBadRequest_WhenPublishWithoutSubdomain()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateMyPublicationAsync(new UpdatePortfolioPublicationRequestDto
            {
                IsPublished = true,
            }));
    }

    [Fact]
    public async Task UpdateMyPublicationAsync_PublishesAndStoresSnapshot()
    {
        var student = SeedStudent();
        var portfolio = SeedPortfolio(student: student, subdomain: "jane-portfolio");
        SeedBuiltInSections(portfolio.Id);
        var sut = CreateSut();

        var result = await sut.UpdateMyPublicationAsync(new UpdatePortfolioPublicationRequestDto
        {
            IsPublished = true,
        });

        Assert.True(result.IsPublic);
        Assert.NotNull(result.LastPublishedAt);
        Assert.False(result.HasUnpublishedChanges);

        var stored = _db.Portfolios.Items.Single(p => p.Id == portfolio.Id);
        Assert.False(string.IsNullOrWhiteSpace(stored.PublishedSnapshot));
    }

    [Fact]
    public async Task UpdateMyPublicationAsync_UnpublishesPortfolio()
    {
        var student = SeedStudent();
        SeedPortfolio(
            student: student,
            subdomain: "jane-portfolio",
            isPublic: true,
            publishedSnapshot: """{"subdomain":"jane-portfolio"}""");
        var sut = CreateSut();

        var result = await sut.UpdateMyPublicationAsync(new UpdatePortfolioPublicationRequestDto
        {
            IsPublished = false,
        });

        Assert.False(result.IsPublic);
    }

    // ── Items ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddItemAsync_CreatesManualProjectItem()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        var result = await sut.AddItemAsync(new CreatePortfolioItemRequestDto
        {
            ItemType = PortfolioItemType.Project,
            Title = "Solar Robot",
            Description = "Built a solar-powered robot",
            AccentColor = "#AABBCC",
        });

        Assert.Equal("Solar Robot", result.Title);
        Assert.Equal(PortfolioItemType.Project, result.ItemType);
        Assert.Equal(PortfolioItemSource.StudentEdited, result.Source);
        Assert.Equal("#AABBCC", result.AccentColor);
        Assert.Single(_db.PortfolioCustomItems.Items);
    }

    [Fact]
    public async Task AddItemAsync_ThrowsBadRequest_ForAutoImportedType()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.AddItemAsync(new CreatePortfolioItemRequestDto
            {
                ItemType = PortfolioItemType.CapstoneProject,
                Title = "Capstone",
            }));
    }

    [Fact]
    public async Task UpdateItemAsync_UpdatesManualItem()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioCustomItems.Seed(new PortfolioCustomItem
        {
            Id = _itemId,
            PortfolioId = _portfolioId,
            ItemType = PortfolioItemType.Hobby,
            Title = "Old Title",
            DisplayOrder = 0,
            IsVisible = true,
            Source = PortfolioItemSource.StudentEdited,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UpdateItemAsync(_itemId, new UpdatePortfolioItemRequestDto
        {
            Title = "Chess Club",
            IsVisible = false,
        });

        Assert.Equal("Chess Club", result.Title);
        Assert.False(result.IsVisible);
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesManualItem()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioCustomItems.Seed(new PortfolioCustomItem
        {
            Id = _itemId,
            PortfolioId = _portfolioId,
            ItemType = PortfolioItemType.Extracurricular,
            Title = "Volunteering",
            DisplayOrder = 0,
            IsVisible = true,
            Source = PortfolioItemSource.StudentEdited,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.RemoveItemAsync(_itemId);

        Assert.True(_db.PortfolioCustomItems.Items.Single(i => i.Id == _itemId).IsDeleted);
    }

    [Fact]
    public async Task RemoveItemAsync_ThrowsBadRequest_ForAutoImportedItem()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioCustomItems.Seed(new PortfolioCustomItem
        {
            Id = _autoItemId,
            PortfolioId = _portfolioId,
            ItemType = PortfolioItemType.InternalCertificate,
            Title = "Certificate",
            DisplayOrder = 0,
            IsVisible = true,
            Source = PortfolioItemSource.AutoImported,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.RemoveItemAsync(_autoItemId));
    }

    [Fact]
    public async Task ReorderItemsAsync_UpdatesDisplayOrder()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var secondItemId = Guid.Parse("35353535-3535-3535-3535-353535353535");
        _db.PortfolioCustomItems.Seed(
            new PortfolioCustomItem
            {
                Id = _itemId,
                PortfolioId = _portfolioId,
                ItemType = PortfolioItemType.Project,
                Title = "First",
                DisplayOrder = 0,
                IsVisible = true,
                Source = PortfolioItemSource.StudentEdited,
                IsDeleted = false,
            },
            new PortfolioCustomItem
            {
                Id = secondItemId,
                PortfolioId = _portfolioId,
                ItemType = PortfolioItemType.Hobby,
                Title = "Second",
                DisplayOrder = 1,
                IsVisible = true,
                Source = PortfolioItemSource.StudentEdited,
                IsDeleted = false,
            });
        var sut = CreateSut();

        var result = await sut.ReorderItemsAsync(new ReorderPortfolioItemsRequestDto
        {
            Items =
            [
                new ReorderPortfolioItemEntryDto { Id = _itemId, DisplayOrder = 1 },
                new ReorderPortfolioItemEntryDto { Id = secondItemId, DisplayOrder = 0 },
            ],
        });

        Assert.Equal("Second", result.Items[0].Title);
        Assert.Equal("First", result.Items[1].Title);
    }

    // ── Public portfolio ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPublicPortfolioBySubdomainAsync_ReturnsPublishedSnapshot()
    {
        SeedStudent();
        const string snapshot = """
            {
              "subdomain": "jane-portfolio",
              "displayName": "Jane Doe",
              "items": [],
              "sections": []
            }
            """;
        SeedPortfolio(
            subdomain: "jane-portfolio",
            isPublic: true,
            publishedSnapshot: snapshot);
        var sut = CreateSut();

        var result = await sut.GetPublicPortfolioBySubdomainAsync("jane-portfolio");

        Assert.Equal("jane-portfolio", result.Subdomain);
        Assert.Equal("Jane Doe", result.DisplayName);
    }

    // ── Media ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadMediaAsync_UploadsValidJpg()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();
        var file = CreateImageFile();

        var result = await sut.UploadMediaAsync(file.Object);

        Assert.Equal("photo.jpg", result.FileName);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(PortfolioMediaType.Image, result.Type);
        Assert.StartsWith("https://cdn.example.com/portfolio/", result.Url);
        _blobService.Verify(
            b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                $"portfolio/{_studentId}/{_portfolioId}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadMediaAsync_UploadsValidMp4()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();
        var file = CreateMediaFile("clip.mp4", 10 * 1024 * 1024, "video/mp4");

        var result = await sut.UploadMediaAsync(file.Object);

        Assert.Equal("clip.mp4", result.FileName);
        Assert.Equal("video/mp4", result.ContentType);
        Assert.Equal(PortfolioMediaType.Video, result.Type);
        Assert.Equal(10 * 1024 * 1024, result.SizeBytes);
        _blobService.Verify(
            b => b.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                $"portfolio/{_studentId}/{_portfolioId}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadMediaAsync_ThrowsBadRequest_ForInvalidExtension()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();
        var file = CreateImageFile("document.pdf", 512, "application/pdf");

        await Assert.ThrowsAsync<BadRequestException>(() => sut.UploadMediaAsync(file.Object));
    }

    [Fact]
    public async Task UploadMediaAsync_ThrowsBadRequest_WhenImageTooLarge()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();
        var file = CreateImageFile(length: 6 * 1024 * 1024);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.UploadMediaAsync(file.Object));
    }

    [Fact]
    public async Task UploadMediaAsync_ThrowsBadRequest_WhenVideoTooLarge()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();
        var file = CreateMediaFile(
            "huge.mp4",
            (2L * 1024 * 1024 * 1024) + 1,
            "video/mp4");

        await Assert.ThrowsAsync<BadRequestException>(() => sut.UploadMediaAsync(file.Object));
    }

    [Fact]
    public async Task UploadMediaAsync_ThrowsBadRequest_ForWrongContentType()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();
        var file = CreateImageFile(contentType: "image/png");

        await Assert.ThrowsAsync<BadRequestException>(() => sut.UploadMediaAsync(file.Object));
    }

    [Fact]
    public async Task ListMediaAsync_ReturnsPortfolioAssets()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioMediaAssets.Seed(new PortfolioMediaAsset
        {
            Id = _mediaId,
            PortfolioId = _portfolioId,
            Type = PortfolioMediaType.Image,
            Url = "https://cdn.example.com/photo.jpg",
            S3Key = "portfolio/photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 2048,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.ListMediaAsync();

        Assert.Single(result);
        Assert.Equal(_mediaId, result[0].Id);
    }

    [Fact]
    public async Task DeleteMediaAsync_DeletesUnusedAsset()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioMediaAssets.Seed(new PortfolioMediaAsset
        {
            Id = _mediaId,
            PortfolioId = _portfolioId,
            Type = PortfolioMediaType.Image,
            Url = "https://cdn.example.com/photo.jpg",
            S3Key = "portfolio/photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 2048,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.DeleteMediaAsync(_mediaId);

        Assert.True(_db.PortfolioMediaAssets.Items.Single(a => a.Id == _mediaId).IsDeleted);
        _blobService.Verify(
            b => b.DeleteByKeyAsync("portfolio/photo.jpg", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Sections ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSectionAsync_CreatesCustomRichTextSection()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        var result = await sut.CreateSectionAsync(new CreatePortfolioSectionRequestDto
        {
            Kind = PortfolioSectionKind.RichText,
            Title = "About",
            ContentHtml = "<p>Hello</p>",
        });

        Assert.Equal(PortfolioSectionKind.RichText, result.Kind);
        Assert.Equal("About", result.Title);
        Assert.Equal("<p>Hello</p>", result.ContentHtml);
    }

    [Fact]
    public async Task UpdateSectionAsync_UpdatesBuiltInSectionWithoutContentHtml()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioSections.Seed(new PortfolioSection
        {
            Id = _builtInSectionId,
            PortfolioId = _portfolioId,
            Kind = PortfolioSectionKind.ProjectsGroup,
            Title = "Projects",
            DisplayOrder = 0,
            IsVisible = true,
            ContentHtml = null,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UpdateSectionAsync(_builtInSectionId, new UpdatePortfolioSectionRequestDto
        {
            Title = "My Projects",
            IsVisible = false,
            ContentHtml = "<p>Should be ignored</p>",
        });

        Assert.Equal("My Projects", result.Title);
        Assert.False(result.IsVisible);
        Assert.Null(result.ContentHtml);
    }

    [Fact]
    public async Task DeleteSectionAsync_ThrowsBadRequest_ForBuiltInSection()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioSections.Seed(new PortfolioSection
        {
            Id = _builtInSectionId,
            PortfolioId = _portfolioId,
            Kind = PortfolioSectionKind.LinksGroup,
            Title = "Links",
            DisplayOrder = 2,
            IsVisible = true,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.DeleteSectionAsync(_builtInSectionId));
    }

    [Fact]
    public async Task DeleteSectionAsync_DeletesCustomSection()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioSections.Seed(new PortfolioSection
        {
            Id = _sectionId,
            PortfolioId = _portfolioId,
            Kind = PortfolioSectionKind.Gallery,
            Title = "Gallery",
            DisplayOrder = 3,
            IsVisible = true,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.DeleteSectionAsync(_sectionId);

        Assert.True(_db.PortfolioSections.Items.Single(s => s.Id == _sectionId).IsDeleted);
    }

    [Fact]
    public async Task ReorderSectionsAsync_UpdatesDisplayOrder()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var secondSectionId = Guid.Parse("48484848-4848-4848-4848-484848484848");
        _db.PortfolioSections.Seed(
            new PortfolioSection
            {
                Id = _sectionId,
                PortfolioId = _portfolioId,
                Kind = PortfolioSectionKind.RichText,
                Title = "About",
                DisplayOrder = 0,
                IsVisible = true,
                IsDeleted = false,
            },
            new PortfolioSection
            {
                Id = secondSectionId,
                PortfolioId = _portfolioId,
                Kind = PortfolioSectionKind.Embed,
                Title = "Video",
                DisplayOrder = 1,
                IsVisible = true,
                IsDeleted = false,
            });
        var sut = CreateSut();

        var result = await sut.ReorderSectionsAsync(new ReorderPortfolioSectionsRequestDto
        {
            Sections =
            [
                new ReorderPortfolioSectionEntryDto { Id = _sectionId, DisplayOrder = 1 },
                new ReorderPortfolioSectionEntryDto { Id = secondSectionId, DisplayOrder = 0 },
            ],
        });

        Assert.Equal("Video", result.Sections[0].Title);
        Assert.Equal("About", result.Sections[1].Title);
    }

    // ── EnsureBuiltInSectionsForAllPortfoliosAsync ───────────────────────────────

    [Fact]
    public async Task EnsureBuiltInSectionsForAllPortfoliosAsync_CreatesMissingBuiltInSections()
    {
        SeedStudent();
        SeedPortfolio();
        SeedStudent(_otherStudentId, "STU-002");
        SeedPortfolio(
            id: Guid.Parse("23232323-2323-2323-2323-232323232323"),
            studentId: _otherStudentId);
        var sut = CreateSut();

        var created = await sut.EnsureBuiltInSectionsForAllPortfoliosAsync();

        Assert.Equal(6, created);
        Assert.Equal(6, _db.PortfolioSections.Items.Count(s => !s.IsDeleted));
    }

    // ── SyncMyPortfolioAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SyncMyPortfolioAsync_ImportsCertificatesAndCapstone_NotHighlightReels()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var programId = Guid.Parse("61616161-6161-6161-6161-616161616161");
        var moduleId = Guid.Parse("62626262-6262-6262-6262-626262626262");
        var classId = Guid.Parse("63636363-6363-6363-6363-636363636363");
        var stackId = Guid.Parse("64646464-6464-6464-6464-646464646464");
        var assignmentId = Guid.Parse("65656565-6565-6565-6565-656565656565");
        var milestoneId = Guid.Parse("66666666-6666-6666-6666-666666666667");
        var submissionId = Guid.Parse("67676767-6767-6767-6767-676767676767");
        var certificateId = Guid.Parse("68686868-6868-6868-6868-686868686868");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-SYNC",
            Name = "Sync Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = moduleId,
            Code = "MOD-SYNC",
            Name = "Capstone Module",
            ProgramId = programId,
            ModuleType = ModuleType.Research,
            ModuleOrder = 1,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-SYNC",
            Name = "Class A",
            ProgramId = programId,
            MaxCapacity = 20,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            Status = ClassStatus.Open,
            IsDeleted = false,
        });
        _db.Certificates.Seed(new Certificate
        {
            Id = certificateId,
            Code = "CERT-SYNC",
            StudentId = _studentId,
            ProgramId = programId,
            PdfUrl = "https://cdn.example.com/cert.pdf",
            IsDeleted = false,
        });
        _db.HighlightVideoStacks.Seed(new HighlightVideoStack
        {
            Id = stackId,
            ClassId = classId,
            StudentId = _studentId,
            StrengthDescription = "Teamwork highlights",
            IsDeleted = false,
        });
        _db.HighlightVideoItems.Seed(new HighlightVideoItem
        {
            Id = Guid.Parse("69696969-6969-6969-6969-696969696969"),
            StackId = stackId,
            Status = HighlightVideoStatus.Completed,
            VideoUrl = "https://cdn.example.com/highlight.mp4",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        _db.Assignments.Seed(new Assignment
        {
            Id = assignmentId,
            Code = "ASG-CAP",
            ModuleId = moduleId,
            Title = "Capstone Deliverable",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsDeleted = false,
        });
        _db.ResearchMilestones.Seed(new ResearchMilestone
        {
            Id = milestoneId,
            Code = "MS-CAP",
            ModuleId = moduleId,
            Title = "Final Capstone",
            MilestoneOrder = 1,
            IsCapstone = true,
            AssignmentId = assignmentId,
            IsDeleted = false,
        });
        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-CAP",
            AssignmentId = assignmentId,
            StudentId = _studentId,
            Status = SubmissionStatus.Graded,
            ResearchMilestoneId = milestoneId,
            ContentText = "Capstone write-up",
            MentorFeedback = "Excellent",
            FileUrl = "https://cdn.example.com/capstone.pdf",
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.SyncMyPortfolioAsync();

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, i => i.ItemType == PortfolioItemType.InternalCertificate);
        Assert.Contains(result.Items, i => i.ItemType == PortfolioItemType.CapstoneProject);
        Assert.DoesNotContain(result.Items, i => i.ItemType == PortfolioItemType.HighlightReel);
    }

    // ── Additional validation / edge paths ───────────────────────────────────────

    [Fact]
    public async Task DeleteMediaAsync_ThrowsBadRequest_WhenMediaInUse()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioMediaAssets.Seed(new PortfolioMediaAsset
        {
            Id = _mediaId,
            PortfolioId = _portfolioId,
            Type = PortfolioMediaType.Image,
            Url = "https://cdn.example.com/photo.jpg",
            S3Key = "portfolio/photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 2048,
            IsDeleted = false,
        });
        _db.PortfolioMediaPlacements.Seed(new PortfolioMediaPlacement
        {
            Id = Guid.Parse("59595959-5959-5959-5959-595959595959"),
            PortfolioMediaAssetId = _mediaId,
            PortfolioSectionId = _sectionId,
            DisplayOrder = 0,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.DeleteMediaAsync(_mediaId));
    }

    [Fact]
    public async Task GetPublicPortfolioBySubdomainAsync_ThrowsNotFound_WhenNotPublished()
    {
        SeedStudent();
        SeedPortfolio(subdomain: "draft-only");
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetPublicPortfolioBySubdomainAsync("draft-only"));
    }

    [Fact]
    public async Task UploadMediaAsync_ThrowsBadRequest_WhenFileMissing()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.UploadMediaAsync(null!));
    }

    [Fact]
    public async Task UpdateSectionAsync_UpdatesCustomSectionContentHtml()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioSections.Seed(new PortfolioSection
        {
            Id = _sectionId,
            PortfolioId = _portfolioId,
            Kind = PortfolioSectionKind.RichText,
            Title = "Bio",
            DisplayOrder = 0,
            IsVisible = true,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UpdateSectionAsync(_sectionId, new UpdatePortfolioSectionRequestDto
        {
            ContentHtml = "<p>Updated bio</p>",
        });

        Assert.Equal("<p>Updated bio</p>", result.ContentHtml);
    }

    [Fact]
    public async Task UpdateMySubdomainAsync_ThrowsBadRequest_ForInvalidFormat()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateMySubdomainAsync(new UpdatePortfolioSubdomainRequestDto
            {
                Subdomain = "ab",
            }));
    }

    [Fact]
    public async Task CreateSectionAsync_ThrowsBadRequest_ForBuiltInKind()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateSectionAsync(new CreatePortfolioSectionRequestDto
            {
                Kind = PortfolioSectionKind.ProjectsGroup,
            }));
    }

    [Fact]
    public async Task AddItemAsync_AttachesMediaPlacements()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioMediaAssets.Seed(new PortfolioMediaAsset
        {
            Id = _mediaId,
            PortfolioId = _portfolioId,
            Type = PortfolioMediaType.Image,
            Url = "https://cdn.example.com/photo.jpg",
            S3Key = "portfolio/photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 2048,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.AddItemAsync(new CreatePortfolioItemRequestDto
        {
            ItemType = PortfolioItemType.Project,
            Title = "Gallery Project",
            MediaAssets =
            [
                new PortfolioMediaAssetInputDto { Id = _mediaId, DisplayOrder = 0, Caption = "Main" },
            ],
        });

        Assert.Single(result.MediaAssets);
        Assert.Equal("Main", result.MediaAssets[0].Caption);
    }

    [Fact]
    public async Task UpdateMyPublicationAsync_SetsHasUnpublishedChangesAfterDraftEdit()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student, subdomain: "jane-portfolio");
        SeedBuiltInSections(_portfolioId);
        var sut = CreateSut();

        await sut.UpdateMyPublicationAsync(new UpdatePortfolioPublicationRequestDto { IsPublished = true });
        await sut.UpdateMyPortfolioAsync(new UpdatePortfolioRequestDto { Headline = "Updated headline" });

        var portfolio = _db.Portfolios.Items.Single(p => p.Id == _portfolioId);
        Assert.True(portfolio.HasUnpublishedChanges);
        Assert.True(portfolio.IsPublic);
    }

    [Fact]
    public async Task UpdateItemAsync_UpdatesAutoImportedItemAndMarksStudentEdited()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioCustomItems.Seed(new PortfolioCustomItem
        {
            Id = _autoItemId,
            PortfolioId = _portfolioId,
            ItemType = PortfolioItemType.HighlightReel,
            Title = "Imported Reel",
            DisplayOrder = 0,
            IsVisible = true,
            Source = PortfolioItemSource.AutoImported,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UpdateItemAsync(_autoItemId, new UpdatePortfolioItemRequestDto
        {
            Title = "Edited Reel",
            StudentEditedBody = "<p>My narrative</p>",
            IsFeatured = true,
        });

        Assert.Equal("Edited Reel", result.Title);
        Assert.Equal(PortfolioItemSource.StudentEdited, result.Source);
        Assert.True(result.IsFeatured);
    }

    [Fact]
    public async Task UpdateMySubdomainAsync_ClearsSubdomainWhenNullAndNotPublic()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student, subdomain: "old-name");
        var sut = CreateSut();

        var result = await sut.UpdateMySubdomainAsync(new UpdatePortfolioSubdomainRequestDto
        {
            Subdomain = null,
        });

        Assert.Null(result.Subdomain);
    }

    [Fact]
    public async Task CheckSubdomainAvailabilityAsync_ReturnsUnavailable_ForInvalidFormat()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        var result = await sut.CheckSubdomainAvailabilityAsync("ab");

        Assert.False(result.Available);
        Assert.Contains("3 and 63", result.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMyPortfolioAsync_ThrowsNotFound_WhenNoPortfolio()
    {
        SeedStudent();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetMyPortfolioAsync());
    }

    [Fact]
    public async Task SyncMyPortfolioAsync_SyncsCapstoneAppendixFromPriorMilestones()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var programId = Guid.Parse("71717171-7171-7171-7171-717171717171");
        var moduleId = Guid.Parse("72727272-7272-7272-7272-727272727272");
        var moduleEnrollmentId = Guid.Parse("73737373-7373-7373-7373-737373737373");
        var capstoneAssignmentId = Guid.Parse("74747474-7474-7474-7474-747474747474");
        var priorAssignmentId = Guid.Parse("75757575-7575-7575-7575-757575757575");
        var capstoneMilestoneId = Guid.Parse("76767676-7676-7676-7676-767676767676");
        var priorMilestoneId = Guid.Parse("77777777-7777-7777-7777-777777777778");
        var capstoneSubmissionId = Guid.Parse("78787878-7878-7878-7878-787878787878");
        var priorSubmissionId = Guid.Parse("79797979-7979-7979-7979-797979797979");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-APP",
            Name = "Appendix Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Intermediate,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = moduleId,
            Code = "MOD-APP",
            Name = "Research Module",
            ProgramId = programId,
            ModuleType = ModuleType.Research,
            ModuleOrder = 1,
            IsDeleted = false,
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = moduleEnrollmentId,
            ModuleId = moduleId,
            StudentId = _studentId,
            IsDeleted = false,
        });
        _db.Assignments.Seed(
            new Assignment
            {
                Id = capstoneAssignmentId,
                Code = "ASG-CAP2",
                ModuleId = moduleId,
                Title = "Capstone",
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 50,
                IsDeleted = false,
            },
            new Assignment
            {
                Id = priorAssignmentId,
                Code = "ASG-PRIOR",
                ModuleId = moduleId,
                Title = "Prior Milestone",
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 50,
                IsDeleted = false,
            });
        _db.ResearchMilestones.Seed(
            new ResearchMilestone
            {
                Id = capstoneMilestoneId,
                Code = "MS-CAP2",
                ModuleId = moduleId,
                Title = "Capstone Final",
                MilestoneOrder = 2,
                IsCapstone = true,
                AssignmentId = capstoneAssignmentId,
                IsDeleted = false,
            },
            new ResearchMilestone
            {
                Id = priorMilestoneId,
                Code = "MS-PRIOR",
                ModuleId = moduleId,
                Title = "Draft Report",
                MilestoneOrder = 1,
                IsCapstone = false,
                AssignmentId = priorAssignmentId,
                IsDeleted = false,
            });
        _db.Submissions.Seed(
            new Submission
            {
                Id = priorSubmissionId,
                Code = "SUB-PRIOR",
                AssignmentId = priorAssignmentId,
                StudentId = _studentId,
                ModuleEnrollmentId = moduleEnrollmentId,
                Status = SubmissionStatus.Graded,
                ResearchMilestoneId = priorMilestoneId,
                ContentText = "Prior draft",
                FileUrl = "https://cdn.example.com/prior.pdf",
                AssignedGrade = 85,
                IsDeleted = false,
            },
            new Submission
            {
                Id = capstoneSubmissionId,
                Code = "SUB-CAP2",
                AssignmentId = capstoneAssignmentId,
                StudentId = _studentId,
                ModuleEnrollmentId = moduleEnrollmentId,
                Status = SubmissionStatus.Graded,
                ResearchMilestoneId = capstoneMilestoneId,
                ContentText = "Final capstone",
                MentorFeedback = "Strong finish",
                FileUrl = "https://cdn.example.com/capstone2.pdf",
                IsDeleted = false,
            });

        var sut = CreateSut();
        var result = await sut.SyncMyPortfolioAsync();

        var capstoneItem = result.Items.Single(i => i.ItemType == PortfolioItemType.CapstoneProject);
        Assert.Single(capstoneItem.AppendixSections);
        Assert.Equal("Draft Report", capstoneItem.AppendixSections[0].SectionTitle);
        Assert.Equal(priorSubmissionId, capstoneItem.AppendixSections[0].SubmissionId);
    }

    [Fact]
    public async Task AddItemAsync_ThrowsNotFound_WhenMediaAssetMissing()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.AddItemAsync(new CreatePortfolioItemRequestDto
            {
                ItemType = PortfolioItemType.Project,
                Title = "Broken Gallery",
                MediaAssets =
                [
                    new PortfolioMediaAssetInputDto
                    {
                        Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                        DisplayOrder = 0,
                    },
                ],
            }));
    }

    [Fact]
    public async Task UpdateItemAsync_UpdatesAllMutableFields()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioMediaAssets.Seed(new PortfolioMediaAsset
        {
            Id = _mediaId,
            PortfolioId = _portfolioId,
            Type = PortfolioMediaType.Image,
            Url = "https://cdn.example.com/photo.jpg",
            S3Key = "portfolio/photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 2048,
            IsDeleted = false,
        });
        _db.PortfolioCustomItems.Seed(new PortfolioCustomItem
        {
            Id = _itemId,
            PortfolioId = _portfolioId,
            ItemType = PortfolioItemType.ExternalCert,
            Title = "Old",
            DisplayOrder = 0,
            IsVisible = true,
            Source = PortfolioItemSource.StudentEdited,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UpdateItemAsync(_itemId, new UpdatePortfolioItemRequestDto
        {
            Title = "AWS Cert",
            Subtitle = "Cloud Practitioner",
            Organization = "Amazon",
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Description = "<p>Passed exam</p>",
            StudentEditedBody = "<p>Study notes</p>",
            MediaUrl = "https://cdn.example.com/cert.png",
            ExternalUrl = "https://credly.com/badge/1",
            DisplayOrder = 2,
            IsVisible = false,
            AccentColor = "#112233",
            IsFeatured = true,
            Span = PortfolioItemSpan.Wide,
            MediaAssets =
            [
                new PortfolioMediaAssetInputDto { Id = _mediaId, DisplayOrder = 0 },
            ],
        });

        Assert.Equal("AWS Cert", result.Title);
        Assert.Equal("Cloud Practitioner", result.Subtitle);
        Assert.Equal(PortfolioItemSpan.Wide, result.Span);
        Assert.Single(result.MediaAssets);
    }

    [Fact]
    public async Task UpdateMyPortfolioAsync_UpdatesAvatarCoverAndClearsWhitespaceFields()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        var result = await sut.UpdateMyPortfolioAsync(new UpdatePortfolioRequestDto
        {
            DisplayName = "   ",
            AvatarUrl = "https://cdn.example.com/avatar.png",
            CoverImageUrl = "https://cdn.example.com/cover.png",
            Links = [],
        });

        Assert.Null(result.DisplayName);
        Assert.Equal("https://cdn.example.com/avatar.png", result.AvatarUrl);
        Assert.Equal("https://cdn.example.com/cover.png", result.CoverImageUrl);
        Assert.Empty(result.Links);
    }

    [Fact]
    public async Task RemoveItemAsync_SoftRemovesLinkedMediaPlacements()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioCustomItems.Seed(new PortfolioCustomItem
        {
            Id = _itemId,
            PortfolioId = _portfolioId,
            ItemType = PortfolioItemType.Project,
            Title = "With Media",
            DisplayOrder = 0,
            IsVisible = true,
            Source = PortfolioItemSource.StudentEdited,
            IsDeleted = false,
        });
        _db.PortfolioMediaPlacements.Seed(new PortfolioMediaPlacement
        {
            Id = Guid.Parse("59595959-5959-5959-5959-595959595959"),
            PortfolioMediaAssetId = _mediaId,
            PortfolioCustomItemId = _itemId,
            DisplayOrder = 0,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.RemoveItemAsync(_itemId);

        Assert.True(_db.PortfolioMediaPlacements.Items.Single(p => p.PortfolioCustomItemId == _itemId).IsDeleted);
    }

    [Fact]
    public async Task DeleteSectionAsync_SoftRemovesLinkedMediaPlacements()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioSections.Seed(new PortfolioSection
        {
            Id = _sectionId,
            PortfolioId = _portfolioId,
            Kind = PortfolioSectionKind.Gallery,
            Title = "Photos",
            DisplayOrder = 0,
            IsVisible = true,
            IsDeleted = false,
        });
        _db.PortfolioMediaPlacements.Seed(new PortfolioMediaPlacement
        {
            Id = Guid.Parse("59595959-5959-5959-5959-595959595959"),
            PortfolioMediaAssetId = _mediaId,
            PortfolioSectionId = _sectionId,
            DisplayOrder = 0,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await sut.DeleteSectionAsync(_sectionId);

        Assert.True(_db.PortfolioMediaPlacements.Items.Single(p => p.PortfolioSectionId == _sectionId).IsDeleted);
    }

    [Fact]
    public async Task UpdateMySubdomainAsync_ThrowsBadRequest_WhenClearingSubdomainWhilePublic()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student, subdomain: "live-name", isPublic: true);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateMySubdomainAsync(new UpdatePortfolioSubdomainRequestDto { Subdomain = null }));
    }

    [Fact]
    public async Task GetMyPortfolioAsync_SeedsBuiltInSectionsUsingThemeSectionOrder()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var portfolio = _db.Portfolios.Items.Single();
        portfolio.ThemeConfig = """{"sectionOrder":["links","projects","certificates"]}""";
        var sut = CreateSut();

        var result = await sut.GetMyPortfolioAsync();

        Assert.Equal(3, result.Sections.Count);
        Assert.Equal(PortfolioSectionKind.LinksGroup, result.Sections[0].Kind);
        Assert.Equal(PortfolioSectionKind.ProjectsGroup, result.Sections[1].Kind);
    }

    [Fact]
    public async Task SyncMyPortfolioAsync_UpdatesExistingAutoImportedCertificate()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var programId = Guid.Parse("81818181-8181-8181-8181-818181818181");
        var certificateId = Guid.Parse("82828282-8282-8282-8282-828282828282");
        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-UPD",
            Name = "Updated Program Name",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Certificates.Seed(new Certificate
        {
            Id = certificateId,
            Code = "CERT-UPD",
            StudentId = _studentId,
            ProgramId = programId,
            PdfUrl = "https://cdn.example.com/new-cert.pdf",
            IsDeleted = false,
        });
        _db.PortfolioCustomItems.Seed(new PortfolioCustomItem
        {
            Id = _autoItemId,
            PortfolioId = _portfolioId,
            ItemType = PortfolioItemType.InternalCertificate,
            ReferenceId = certificateId,
            ProgramId = programId,
            Title = "Old Program Name",
            MediaUrl = "https://cdn.example.com/old-cert.pdf",
            DisplayOrder = 0,
            IsVisible = true,
            Source = PortfolioItemSource.AutoImported,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.SyncMyPortfolioAsync();

        var certItem = result.Items.Single(i => i.ItemType == PortfolioItemType.InternalCertificate);
        Assert.Equal("Updated Program Name", certItem.Title);
        Assert.Equal("https://cdn.example.com/new-cert.pdf", certItem.MediaUrl);
    }

    [Fact]
    public async Task ReorderItemsAsync_ThrowsNotFound_WhenItemMissing()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.ReorderItemsAsync(new ReorderPortfolioItemsRequestDto
            {
                Items = [new ReorderPortfolioItemEntryDto { Id = _itemId, DisplayOrder = 0 }],
            }));
    }

    [Fact]
    public async Task UpdateMyPublicationAsync_ThrowsConflict_WhenSubdomainTakenOnPublish()
    {
        SeedStudent();
        SeedPortfolio(subdomain: "publish-me", isPublic: true);
        SeedStudent(_otherStudentId, "STU-002");
        SeedPortfolio(
            id: Guid.Parse("23232323-2323-2323-2323-232323232323"),
            studentId: _otherStudentId,
            subdomain: "publish-me");
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateMyPublicationAsync(new UpdatePortfolioPublicationRequestDto { IsPublished = true }));
    }

    [Fact]
    public async Task GetPublicPortfolioBySubdomainAsync_ThrowsNotFound_ForInvalidSubdomain()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetPublicPortfolioBySubdomainAsync("ab"));
    }

    [Fact]
    public async Task UploadMediaAsync_ThrowsBadRequest_WhenFileEmpty()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var file = CreateImageFile(length: 0);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() => sut.UploadMediaAsync(file.Object));
    }

    [Fact]
    public async Task AddItemAsync_ThrowsBadRequest_WhenDuplicateMediaIds()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        _db.PortfolioMediaAssets.Seed(new PortfolioMediaAsset
        {
            Id = _mediaId,
            PortfolioId = _portfolioId,
            Type = PortfolioMediaType.Image,
            Url = "https://cdn.example.com/photo.jpg",
            S3Key = "portfolio/photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 2048,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.AddItemAsync(new CreatePortfolioItemRequestDto
            {
                ItemType = PortfolioItemType.Project,
                Title = "Dup Media",
                MediaAssets =
                [
                    new PortfolioMediaAssetInputDto { Id = _mediaId, DisplayOrder = 0 },
                    new PortfolioMediaAssetInputDto { Id = _mediaId, DisplayOrder = 1 },
                ],
            }));
    }

    [Fact]
    public async Task UpdateMySubdomainAsync_ThrowsConflict_WhenSubdomainTaken()
    {
        SeedStudent();
        SeedPortfolio(subdomain: "taken-slug");
        SeedStudent(_otherStudentId, "STU-002");
        SeedPortfolio(
            id: Guid.Parse("23232323-2323-2323-2323-232323232323"),
            studentId: _otherStudentId);
        var sut = CreateSut(_otherStudentId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateMySubdomainAsync(new UpdatePortfolioSubdomainRequestDto
            {
                Subdomain = "taken-slug",
            }));
    }

    [Fact]
    public async Task CreateMyPortfolioAsync_ThrowsUnauthorized_WhenNotLoggedIn()
    {
        var sut = CreateSut(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() => sut.CreateMyPortfolioAsync());
    }

    [Fact]
    public async Task SyncMyPortfolioAsync_UpdatesExistingCapstoneAndHighlightItems()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var programId = Guid.Parse("91919191-9191-9191-9191-919191919191");
        var moduleId = Guid.Parse("92929292-9292-9292-9292-929292929292");
        var classId = Guid.Parse("93939393-9393-9393-9393-939393939393");
        var stackId = Guid.Parse("94949494-9494-9494-9494-949494949494");
        var assignmentId = Guid.Parse("95959595-9595-9595-9595-959595959595");
        var milestoneId = Guid.Parse("96969696-9696-9696-9696-969696969696");
        var submissionId = Guid.Parse("97979797-9797-9797-9797-979797979797");
        var capstoneItemId = Guid.Parse("98989898-9898-9898-9898-989898989898");
        var reelItemId = Guid.Parse("99999999-9999-9999-9999-999999999990");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-UPD2",
            Name = "Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = moduleId,
            Code = "MOD-UPD2",
            Name = "New Module Title",
            ProgramId = programId,
            ModuleType = ModuleType.Research,
            ModuleOrder = 1,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-UPD2",
            Name = "Class",
            ProgramId = programId,
            MaxCapacity = 20,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            Status = ClassStatus.Open,
            IsDeleted = false,
        });
        _db.Assignments.Seed(new Assignment
        {
            Id = assignmentId,
            Code = "ASG-UPD2",
            ModuleId = moduleId,
            Title = "Capstone",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsDeleted = false,
        });
        _db.ResearchMilestones.Seed(new ResearchMilestone
        {
            Id = milestoneId,
            Code = "MS-UPD2",
            ModuleId = moduleId,
            Title = "Capstone",
            MilestoneOrder = 1,
            IsCapstone = true,
            AssignmentId = assignmentId,
            IsDeleted = false,
        });
        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-UPD2",
            AssignmentId = assignmentId,
            StudentId = _studentId,
            Status = SubmissionStatus.Graded,
            ResearchMilestoneId = milestoneId,
            ContentText = "Updated capstone body",
            MentorFeedback = "Updated feedback",
            FileUrl = "https://cdn.example.com/updated.pdf",
            IsDeleted = false,
        });
        _db.HighlightVideoStacks.Seed(new HighlightVideoStack
        {
            Id = stackId,
            ClassId = classId,
            StudentId = _studentId,
            StrengthDescription = "Updated highlight title",
            IsDeleted = false,
        });
        _db.HighlightVideoItems.Seed(new HighlightVideoItem
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999991"),
            StackId = stackId,
            Status = HighlightVideoStatus.Completed,
            VideoUrl = "https://cdn.example.com/new-highlight.mp4",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        _db.PortfolioCustomItems.Seed(
            new PortfolioCustomItem
            {
                Id = capstoneItemId,
                PortfolioId = _portfolioId,
                ItemType = PortfolioItemType.CapstoneProject,
                SubmissionId = submissionId,
                ModuleId = moduleId,
                Title = "Old Module Title",
                Description = "Old body",
                MediaUrl = "https://cdn.example.com/old.pdf",
                DisplayOrder = 0,
                IsVisible = true,
                Source = PortfolioItemSource.AutoImported,
                IsDeleted = false,
            },
            new PortfolioCustomItem
            {
                Id = reelItemId,
                PortfolioId = _portfolioId,
                ItemType = PortfolioItemType.HighlightReel,
                ReferenceId = stackId,
                Title = "Old highlight",
                MediaUrl = "https://cdn.example.com/old.mp4",
                DisplayOrder = 1,
                IsVisible = true,
                Source = PortfolioItemSource.AutoImported,
                IsDeleted = false,
            });
        var sut = CreateSut();

        var result = await sut.SyncMyPortfolioAsync();

        var capstone = result.Items.Single(i => i.Id == capstoneItemId);
        var reel = result.Items.Single(i => i.Id == reelItemId);
        Assert.Equal("Updated capstone body", capstone.Description);
        Assert.Equal("Updated feedback", capstone.MentorEndorsement);
        Assert.Equal("https://cdn.example.com/updated.pdf", capstone.MediaUrl);
        // Sync no longer refreshes HighlightReel items; existing rows are left as-is.
        Assert.Equal("Old highlight", reel.Title);
        Assert.Equal("https://cdn.example.com/old.mp4", reel.MediaUrl);
    }

    [Fact]
    public async Task ImportClassGalleryMediaAsync_CopiesReadyImageAndVideo()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);

        var programId = Guid.Parse("61616161-6161-6161-6161-616161616161");
        var classId = Guid.Parse("62626262-6262-6262-6262-626262626262");
        var imageMediaId = Guid.Parse("63636363-6363-6363-6363-636363636363");
        var videoMediaId = Guid.Parse("64646464-6464-6464-6464-646464646464");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-IMP",
            Name = "Import Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-IMP",
            Name = "Import Class",
            ProgramId = programId,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(30),
            MaxCapacity = 20,
            Status = ClassStatus.InProgress,
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = _studentId,
            ProgramEnrollmentId = Guid.NewGuid(),
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false,
        });
        _db.MediaAssets.Seed(
            new MediaAsset
            {
                Id = imageMediaId,
                UploaderId = _studentId,
                ClassId = classId,
                FileType = "image",
                FileUrl = "https://obox-bucket.s3.ap-southeast-1.amazonaws.com/media/photo.jpg",
                VideoStatus = VideoProcessingStatus.None,
                UploadedAt = DateTime.UtcNow,
                IsDeleted = false,
            },
            new MediaAsset
            {
                Id = videoMediaId,
                UploaderId = _studentId,
                ClassId = classId,
                FileType = "video",
                FileUrl = "https://obox-bucket.s3.ap-southeast-1.amazonaws.com/media/clip.mp4",
                VideoStatus = VideoProcessingStatus.TaggingComplete,
                UploadedAt = DateTime.UtcNow,
                IsDeleted = false,
            });

        var sut = CreateSut();
        var result = await sut.ImportClassGalleryMediaAsync(new ImportClassGalleryMediaRequestDto
        {
            MediaAssetIds = [imageMediaId, videoMediaId],
        });

        Assert.Equal(2, result.Assets.Count);
        Assert.Contains(result.Assets, a => a.Type == PortfolioMediaType.Image);
        Assert.Contains(result.Assets, a => a.Type == PortfolioMediaType.Video);
        Assert.Equal(2, _db.PortfolioMediaAssets.Items.Count(a => !a.IsDeleted));
        Assert.All(
            _db.MediaAssets.Items.Where(m => m.Id == imageMediaId || m.Id == videoMediaId),
            m => Assert.False(m.IsDeleted));
        _blobService.Verify(
            b => b.CopyObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ImportClassGalleryMediaAsync_IsIdempotent_BySourceMediaAssetId()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);

        var programId = Guid.Parse("65656565-6565-6565-6565-656565656565");
        var classId = Guid.Parse("66666666-6666-6666-6666-666666666661");
        var imageMediaId = Guid.Parse("67676767-6767-6767-6767-676767676767");
        var existingAssetId = Guid.Parse("68686868-6868-6868-6868-686868686868");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-IDEM",
            Name = "Idem Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-IDEM",
            Name = "Idem Class",
            ProgramId = programId,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(30),
            MaxCapacity = 20,
            Status = ClassStatus.InProgress,
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = _studentId,
            ProgramEnrollmentId = Guid.NewGuid(),
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false,
        });
        _db.MediaAssets.Seed(new MediaAsset
        {
            Id = imageMediaId,
            UploaderId = _studentId,
            ClassId = classId,
            FileType = "image",
            FileUrl = "https://obox-bucket.s3.ap-southeast-1.amazonaws.com/media/photo.jpg",
            VideoStatus = VideoProcessingStatus.None,
            UploadedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        _db.PortfolioMediaAssets.Seed(new PortfolioMediaAsset
        {
            Id = existingAssetId,
            PortfolioId = _portfolioId,
            Type = PortfolioMediaType.Image,
            Url = "https://cdn.example.com/already.jpg",
            S3Key = "portfolio/already.jpg",
            FileName = "already.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            SourceMediaAssetId = imageMediaId,
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.ImportClassGalleryMediaAsync(new ImportClassGalleryMediaRequestDto
        {
            MediaAssetIds = [imageMediaId],
        });

        Assert.Single(result.Assets);
        Assert.Equal(existingAssetId, result.Assets[0].Id);
        _blobService.Verify(
            b => b.CopyObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportClassGalleryMediaAsync_AppendsToItem()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);

        var programId = Guid.Parse("69696969-6969-6969-6969-696969696969");
        var classId = Guid.Parse("6a6a6a6a-6a6a-6a6a-6a6a-6a6a6a6a6a6a");
        var imageMediaId = Guid.Parse("6b6b6b6b-6b6b-6b6b-6b6b-6b6b6b6b6b6b");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-ATT",
            Name = "Attach Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-ATT",
            Name = "Attach Class",
            ProgramId = programId,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(30),
            MaxCapacity = 20,
            Status = ClassStatus.InProgress,
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = _studentId,
            ProgramEnrollmentId = Guid.NewGuid(),
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false,
        });
        _db.MediaAssets.Seed(new MediaAsset
        {
            Id = imageMediaId,
            UploaderId = _studentId,
            ClassId = classId,
            FileType = "image",
            FileUrl = "https://obox-bucket.s3.ap-southeast-1.amazonaws.com/media/photo.jpg",
            VideoStatus = VideoProcessingStatus.None,
            UploadedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        _db.PortfolioCustomItems.Seed(new PortfolioCustomItem
        {
            Id = _itemId,
            PortfolioId = _portfolioId,
            ItemType = PortfolioItemType.Project,
            Title = "My Project",
            DisplayOrder = 0,
            IsVisible = true,
            Source = PortfolioItemSource.StudentEdited,
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.ImportClassGalleryMediaAsync(new ImportClassGalleryMediaRequestDto
        {
            MediaAssetIds = [imageMediaId],
            PortfolioCustomItemId = _itemId,
        });

        Assert.NotNull(result.Item);
        Assert.Single(result.Item!.MediaAssets);
        Assert.Single(_db.PortfolioMediaPlacements.Items, p => !p.IsDeleted);
    }

    [Fact]
    public async Task ImportClassGalleryMediaAsync_Throws_WhenNotReadyVideo()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);

        var programId = Guid.Parse("6c6c6c6c-6c6c-6c6c-6c6c-6c6c6c6c6c6c");
        var classId = Guid.Parse("6d6d6d6d-6d6d-6d6d-6d6d-6d6d6d6d6d6d");
        var videoMediaId = Guid.Parse("6e6e6e6e-6e6e-6e6e-6e6e-6e6e6e6e6e6e");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-NR",
            Name = "NotReady Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-NR",
            Name = "NotReady Class",
            ProgramId = programId,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(30),
            MaxCapacity = 20,
            Status = ClassStatus.InProgress,
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = _studentId,
            ProgramEnrollmentId = Guid.NewGuid(),
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false,
        });
        _db.MediaAssets.Seed(new MediaAsset
        {
            Id = videoMediaId,
            UploaderId = _studentId,
            ClassId = classId,
            FileType = "video",
            FileUrl = "https://obox-bucket.s3.ap-southeast-1.amazonaws.com/media/clip.mp4",
            VideoStatus = VideoProcessingStatus.Transcoding,
            UploadedAt = DateTime.UtcNow,
            IsDeleted = false,
        });

        var sut = CreateSut();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ImportClassGalleryMediaAsync(new ImportClassGalleryMediaRequestDto
            {
                MediaAssetIds = [videoMediaId],
            }));
    }

    [Fact]
    public async Task ImportHighlightReelMediaAsync_CopiesCompletedVideoAndAppendsToGallery()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var gallerySectionId = Guid.Parse("47474747-4747-4747-4747-474747474747");
        var stackId = Guid.Parse("48484848-4848-4848-4848-484848484848");
        var highlightItemId = Guid.Parse("49494949-4949-4949-4949-494949494949");
        var classId = Guid.Parse("4a4a4a4a-4a4a-4a4a-4a4a-4a4a4a4a4a4a");
        var programId = Guid.Parse("4b4b4b4b-4b4b-4b4b-4b4b-4b4b4b4b4b4b");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-HL",
            Name = "Highlight Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-HL",
            Name = "Highlight Class",
            ProgramId = programId,
            MaxCapacity = 20,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            Status = ClassStatus.Open,
            IsDeleted = false,
        });
        _db.PortfolioSections.Seed(new PortfolioSection
        {
            Id = gallerySectionId,
            PortfolioId = _portfolioId,
            Kind = PortfolioSectionKind.Gallery,
            Title = "Gallery",
            DisplayOrder = 0,
            IsVisible = true,
            IsDeleted = false,
        });
        _db.HighlightVideoStacks.Seed(new HighlightVideoStack
        {
            Id = stackId,
            ClassId = classId,
            StudentId = _studentId,
            StrengthDescription = "Teamwork highlights",
            IsDeleted = false,
        });
        _db.HighlightVideoItems.Seed(new HighlightVideoItem
        {
            Id = highlightItemId,
            StackId = stackId,
            Status = HighlightVideoStatus.Completed,
            VideoUrl = "https://obox-bucket.s3.ap-southeast-1.amazonaws.com/personal-videos/reel.mp4",
            OutputS3Key = "personal-videos/reel.mp4",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.ImportHighlightReelMediaAsync(new ImportHighlightReelMediaRequestDto
        {
            HighlightVideoItemId = highlightItemId,
            PortfolioSectionId = gallerySectionId,
        });

        Assert.Single(result.Assets);
        Assert.Equal(PortfolioMediaType.Video, result.Assets[0].Type);
        Assert.Equal("video/mp4", result.Assets[0].ContentType);
        Assert.Null(result.Item);
        Assert.NotNull(result.Section);
        Assert.Equal(gallerySectionId, result.Section!.Id);
        Assert.Single(result.Section.MediaAssets);
        Assert.Equal("Teamwork highlights", result.Section.MediaAssets[0].Caption);
        Assert.Equal(PortfolioMediaType.Video, result.Section.MediaAssets[0].Type);

        var stored = _db.PortfolioMediaAssets.Items.Single(a => !a.IsDeleted);
        Assert.Equal(highlightItemId, stored.SourceHighlightVideoItemId);
        _blobService.Verify(
            b => b.CopyObjectAsync(
                "personal-videos/reel.mp4",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportHighlightReelMediaAsync_IsIdempotent_AndAppendsPlacementWhenMissing()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var gallerySectionId = Guid.Parse("4c4c4c4c-4c4c-4c4c-4c4c-4c4c4c4c4c4c");
        var stackId = Guid.Parse("4d4d4d4d-4d4d-4d4d-4d4d-4d4d4d4d4d4d");
        var highlightItemId = Guid.Parse("4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e");
        var existingAssetId = Guid.Parse("4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f");
        var classId = Guid.Parse("50505050-5050-5050-5050-505050505050");
        var programId = Guid.Parse("51515151-5151-5151-5151-515151515151");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-HL2",
            Name = "Highlight Program 2",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-HL2",
            Name = "Highlight Class 2",
            ProgramId = programId,
            MaxCapacity = 20,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            Status = ClassStatus.Open,
            IsDeleted = false,
        });
        _db.PortfolioSections.Seed(new PortfolioSection
        {
            Id = gallerySectionId,
            PortfolioId = _portfolioId,
            Kind = PortfolioSectionKind.Gallery,
            Title = "Gallery",
            DisplayOrder = 0,
            IsVisible = true,
            IsDeleted = false,
        });
        _db.HighlightVideoStacks.Seed(new HighlightVideoStack
        {
            Id = stackId,
            ClassId = classId,
            StudentId = _studentId,
            StrengthDescription = "",
            IsDeleted = false,
        });
        _db.HighlightVideoItems.Seed(new HighlightVideoItem
        {
            Id = highlightItemId,
            StackId = stackId,
            Status = HighlightVideoStatus.Completed,
            VideoUrl = "https://cdn.example.com/reel.mp4",
            OutputS3Key = "personal-videos/reel.mp4",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        _db.PortfolioMediaAssets.Seed(new PortfolioMediaAsset
        {
            Id = existingAssetId,
            PortfolioId = _portfolioId,
            Type = PortfolioMediaType.Video,
            Url = "https://cdn.example.com/portfolio/existing.mp4",
            S3Key = "portfolio/existing.mp4",
            FileName = "existing.mp4",
            ContentType = "video/mp4",
            SizeBytes = 0,
            SourceHighlightVideoItemId = highlightItemId,
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.ImportHighlightReelMediaAsync(new ImportHighlightReelMediaRequestDto
        {
            HighlightVideoItemId = highlightItemId,
            PortfolioSectionId = gallerySectionId,
            Caption = "Custom caption",
        });

        Assert.Single(result.Assets);
        Assert.Equal(existingAssetId, result.Assets[0].Id);
        Assert.Single(_db.PortfolioMediaAssets.Items, a => !a.IsDeleted);
        Assert.Single(_db.PortfolioMediaPlacements.Items, p => !p.IsDeleted);
        Assert.Equal("Custom caption", result.Section!.MediaAssets[0].Caption);
        _blobService.Verify(
            b => b.CopyObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Second call: no duplicate placement
        var again = await sut.ImportHighlightReelMediaAsync(new ImportHighlightReelMediaRequestDto
        {
            HighlightVideoItemId = highlightItemId,
            PortfolioSectionId = gallerySectionId,
        });
        Assert.Equal(existingAssetId, again.Assets[0].Id);
        Assert.Single(_db.PortfolioMediaPlacements.Items, p => !p.IsDeleted);
    }

    [Fact]
    public async Task ImportHighlightReelMediaAsync_Throws_WhenSectionNotGallery()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        SeedBuiltInSections(_portfolioId);
        var stackId = Guid.Parse("52525252-5252-5252-5252-525252525252");
        var highlightItemId = Guid.Parse("53535353-5353-5353-5353-535353535353");
        var classId = Guid.Parse("54545454-5454-5454-5454-545454545454");
        var programId = Guid.Parse("55555555-5555-5555-5555-555555555556");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-HL3",
            Name = "Highlight Program 3",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-HL3",
            Name = "Highlight Class 3",
            ProgramId = programId,
            MaxCapacity = 20,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            Status = ClassStatus.Open,
            IsDeleted = false,
        });
        _db.HighlightVideoStacks.Seed(new HighlightVideoStack
        {
            Id = stackId,
            ClassId = classId,
            StudentId = _studentId,
            StrengthDescription = "Strength",
            IsDeleted = false,
        });
        _db.HighlightVideoItems.Seed(new HighlightVideoItem
        {
            Id = highlightItemId,
            StackId = stackId,
            Status = HighlightVideoStatus.Completed,
            VideoUrl = "https://cdn.example.com/reel.mp4",
            OutputS3Key = "personal-videos/reel.mp4",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });

        var sut = CreateSut();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ImportHighlightReelMediaAsync(new ImportHighlightReelMediaRequestDto
            {
                HighlightVideoItemId = highlightItemId,
                PortfolioSectionId = _builtInSectionId,
            }));
    }

    [Fact]
    public async Task ImportHighlightReelMediaAsync_Throws_WhenItemNotCompleted()
    {
        var student = SeedStudent();
        SeedPortfolio(student: student);
        var gallerySectionId = Guid.Parse("56565656-5656-5656-5656-565656565656");
        var stackId = Guid.Parse("57575757-5757-5757-5757-575757575757");
        var highlightItemId = Guid.Parse("58585858-5858-5858-5858-585858585858");
        var classId = Guid.Parse("59595959-5959-5959-5959-595959595960");
        var programId = Guid.Parse("5a5a5a5a-5a5a-5a5a-5a5a-5a5a5a5a5a5a");

        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = "PRG-HL4",
            Name = "Highlight Program 4",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = classId,
            Code = "CLS-HL4",
            Name = "Highlight Class 4",
            ProgramId = programId,
            MaxCapacity = 20,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            Status = ClassStatus.Open,
            IsDeleted = false,
        });
        _db.PortfolioSections.Seed(new PortfolioSection
        {
            Id = gallerySectionId,
            PortfolioId = _portfolioId,
            Kind = PortfolioSectionKind.Gallery,
            Title = "Gallery",
            DisplayOrder = 0,
            IsVisible = true,
            IsDeleted = false,
        });
        _db.HighlightVideoStacks.Seed(new HighlightVideoStack
        {
            Id = stackId,
            ClassId = classId,
            StudentId = _studentId,
            StrengthDescription = "Strength",
            IsDeleted = false,
        });
        _db.HighlightVideoItems.Seed(new HighlightVideoItem
        {
            Id = highlightItemId,
            StackId = stackId,
            Status = HighlightVideoStatus.Processing,
            VideoUrl = null,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });

        var sut = CreateSut();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ImportHighlightReelMediaAsync(new ImportHighlightReelMediaRequestDto
            {
                HighlightVideoItemId = highlightItemId,
                PortfolioSectionId = gallerySectionId,
            }));
    }
}
