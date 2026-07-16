using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.PortfolioDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class PortfolioService : IPortfolioService
{
    private static readonly HashSet<PortfolioItemType> ManualCreatableTypes =
    [
        PortfolioItemType.ExternalCert,
        PortfolioItemType.Hobby,
        PortfolioItemType.Extracurricular,
        PortfolioItemType.Project,
    ];

    private static readonly HashSet<PortfolioItemType> AutoImportedTypes =
    [
        PortfolioItemType.CapstoneProject,
        PortfolioItemType.InternalCertificate,
        PortfolioItemType.HighlightReel,
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<PortfolioService> _logger;

    public PortfolioService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<PortfolioService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<PortfolioResponseDto> GetMyPortfolioAsync()
    {
        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PortfolioResponseDto> CreateMyPortfolioAsync()
    {
        var student = await GetCurrentStudentAsync();

        var existing = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.StudentId == student.Id && p.ParentPortfolioId == null && !p.IsDeleted);
        if (existing != null)
        {
            throw ErrorHelper.Conflict("You already have a portfolio.");
        }

        var portfolio = new Portfolio
        {
            Code = await GenerateUniquePortfolioCodeAsync(),
            StudentId = student.Id,
            IsPublic = false,
            Subdomain = null,
        };

        await _unitOfWork.Portfolios.AddAsync(portfolio);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[CreateMyPortfolioAsync] Portfolio {PortfolioId} created for student {StudentId}.",
            portfolio.Id,
            student.Id);

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PortfolioResponseDto> UpdateMyPortfolioAsync(UpdatePortfolioRequestDto dto)
    {
        if (dto == null)
        {
            throw ErrorHelper.BadRequest("Portfolio update data is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);

        if (dto.DisplayName != null)
        {
            portfolio.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName)
                ? null
                : dto.DisplayName.Trim();
        }

        if (dto.Headline != null)
        {
            portfolio.Headline = string.IsNullOrWhiteSpace(dto.Headline)
                ? null
                : dto.Headline.Trim();
        }

        if (dto.Tagline != null)
        {
            portfolio.Tagline = string.IsNullOrWhiteSpace(dto.Tagline)
                ? null
                : dto.Tagline.Trim();
        }

        if (dto.Summary != null)
        {
            portfolio.Summary = string.IsNullOrWhiteSpace(dto.Summary)
                ? null
                : dto.Summary.Trim();
        }

        if (dto.Subdomain != null)
        {
            await ApplySubdomainAsync(portfolio, dto.Subdomain);
        }

        if (dto.Theme != null)
        {
            ApplyTheme(portfolio, dto.Theme);
        }

        if (dto.Links != null)
        {
            portfolio.Links = dto.Links.Count == 0
                ? null
                : JsonSerializer.Serialize(dto.Links, JsonOptions);
        }

        if (dto.IsPublic.HasValue)
        {
            if (dto.IsPublic.Value)
            {
                EnsureCanPublish(portfolio);
                portfolio.IsPublic = true;
            }
            else
            {
                portfolio.IsPublic = false;
            }
        }

        await _unitOfWork.Portfolios.Update(portfolio);
        await _unitOfWork.SaveChangesAsync();

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<SubdomainAvailabilityResponseDto> CheckSubdomainAvailabilityAsync(string subdomain)
    {
        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        return await BuildSubdomainAvailabilityAsync(subdomain, portfolio.Id);
    }

    public async Task<PortfolioCustomItemResponseDto> AddItemAsync(CreatePortfolioItemRequestDto dto)
    {
        if (dto == null)
        {
            throw ErrorHelper.BadRequest("Portfolio item data is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw ErrorHelper.BadRequest("Title is required.");
        }

        if (!ManualCreatableTypes.Contains(dto.ItemType))
        {
            throw ErrorHelper.BadRequest("This item type cannot be created manually.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var items = await GetPortfolioItemsAsync(portfolio.Id);

        var nextOrder = dto.DisplayOrder ?? (items.Count == 0 ? 0 : items.Max(i => i.DisplayOrder) + 1);
        if (nextOrder < 0)
        {
            throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
        }

        var item = new PortfolioCustomItem
        {
            PortfolioId = portfolio.Id,
            ItemType = dto.ItemType,
            Title = dto.Title.Trim(),
            Subtitle = NormalizeOptional(dto.Subtitle),
            Organization = NormalizeOptional(dto.Organization),
            Description = NormalizeOptional(dto.Description),
            StudentEditedBody = NormalizeOptional(dto.StudentEditedBody),
            MediaUrl = NormalizeOptional(dto.MediaUrl),
            ExternalUrl = NormalizeOptional(dto.ExternalUrl),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            DisplayOrder = nextOrder,
            IsVisible = dto.IsVisible ?? true,
            Source = PortfolioItemSource.StudentEdited,
        };

        await _unitOfWork.PortfolioCustomItems.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        return MapItemResponse(item);
    }

    public async Task<PortfolioCustomItemResponseDto> UpdateItemAsync(
        Guid itemId,
        UpdatePortfolioItemRequestDto dto)
    {
        if (itemId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Portfolio item id is required.");
        }

        if (dto == null)
        {
            throw ErrorHelper.BadRequest("Portfolio item update data is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var item = await GetOwnedItemOrThrowAsync(portfolio.Id, itemId);

        if (dto.Title != null)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                throw ErrorHelper.BadRequest("Title cannot be empty.");
            }

            item.Title = dto.Title.Trim();
        }

        if (dto.Subtitle != null)
        {
            item.Subtitle = NormalizeOptional(dto.Subtitle);
        }

        if (dto.Organization != null)
        {
            item.Organization = NormalizeOptional(dto.Organization);
        }

        if (dto.StartDate.HasValue)
        {
            item.StartDate = dto.StartDate;
        }

        if (dto.EndDate.HasValue)
        {
            item.EndDate = dto.EndDate;
        }

        if (dto.Description != null)
        {
            item.Description = NormalizeOptional(dto.Description);
        }

        if (dto.StudentEditedBody != null)
        {
            item.StudentEditedBody = NormalizeOptional(dto.StudentEditedBody);
        }

        if (dto.MediaUrl != null)
        {
            item.MediaUrl = NormalizeOptional(dto.MediaUrl);
        }

        if (dto.ExternalUrl != null)
        {
            item.ExternalUrl = NormalizeOptional(dto.ExternalUrl);
        }

        if (dto.DisplayOrder.HasValue)
        {
            if (dto.DisplayOrder.Value < 0)
            {
                throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
            }

            item.DisplayOrder = dto.DisplayOrder.Value;
        }

        if (dto.IsVisible.HasValue)
        {
            item.IsVisible = dto.IsVisible.Value;
        }

        if (AutoImportedTypes.Contains(item.ItemType))
        {
            item.Source = PortfolioItemSource.StudentEdited;
        }

        await _unitOfWork.PortfolioCustomItems.Update(item);
        await _unitOfWork.SaveChangesAsync();

        var appendixByItemId = await LoadAppendixByItemIdAsync([item.Id]);
        return MapItemResponse(item, appendixByItemId.GetValueOrDefault(item.Id));
    }

    public async Task RemoveItemAsync(Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("Portfolio item id is required.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var item = await GetOwnedItemOrThrowAsync(portfolio.Id, itemId);

        if (AutoImportedTypes.Contains(item.ItemType))
        {
            throw ErrorHelper.BadRequest("Auto-imported items cannot be deleted. Hide them instead.");
        }

        await _unitOfWork.PortfolioCustomItems.SoftRemove(item);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PortfolioResponseDto> ReorderItemsAsync(ReorderPortfolioItemsRequestDto dto)
    {
        if (dto?.Items == null || dto.Items.Count == 0)
        {
            throw ErrorHelper.BadRequest("At least one item is required to reorder.");
        }

        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var items = await GetPortfolioItemsAsync(portfolio.Id);
        var itemsById = items.ToDictionary(i => i.Id);

        var toUpdate = new List<PortfolioCustomItem>();

        foreach (var entry in dto.Items)
        {
            if (!itemsById.TryGetValue(entry.Id, out var item))
            {
                throw ErrorHelper.NotFound($"Portfolio item with id '{entry.Id}' not found.");
            }

            if (entry.DisplayOrder < 0)
            {
                throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
            }

            item.DisplayOrder = entry.DisplayOrder;
            toUpdate.Add(item);
        }

        await _unitOfWork.PortfolioCustomItems.UpdateRange(toUpdate);
        await _unitOfWork.SaveChangesAsync();

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PortfolioResponseDto> SyncMyPortfolioAsync()
    {
        var student = await GetCurrentStudentAsync();
        var portfolio = await GetRootPortfolioForStudentOrThrowAsync(student.Id);
        var items = await GetPortfolioItemsAsync(portfolio.Id);

        await SyncCertificatesAsync(portfolio, items);
        await SyncCapstoneProjectsAsync(portfolio, items);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[SyncMyPortfolioAsync] Portfolio {PortfolioId} synced for student {StudentId}.",
            portfolio.Id,
            student.Id);

        return await MapPortfolioResponseAsync(portfolio);
    }

    public async Task<PublicPortfolioResponseDto> GetPublicPortfolioBySubdomainAsync(string subdomain)
    {
        var normalized = PortfolioSubdomainValidator.Normalize(subdomain);
        if (normalized == null || !PortfolioSubdomainValidator.TryValidateFormat(normalized, out _))
        {
            throw ErrorHelper.NotFound("Portfolio not found.");
        }

        var portfolio = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.Subdomain == normalized && p.IsPublic && !p.IsDeleted,
            p => p.Student);

        if (portfolio == null)
        {
            throw ErrorHelper.NotFound("Portfolio not found.");
        }

        var items = (await GetPortfolioItemsAsync(portfolio.Id))
            .Where(i => i.IsVisible)
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.CreatedAt)
            .ToList();

        var appendixByItemId = await LoadAppendixByItemIdAsync(items.Select(i => i.Id).ToList());

        return new PublicPortfolioResponseDto
        {
            Subdomain = portfolio.Subdomain,
            DisplayName = portfolio.DisplayName,
            Headline = portfolio.Headline,
            Tagline = portfolio.Tagline,
            Summary = portfolio.Summary,
            StudentName = portfolio.Student.FullName,
            AvatarUrl = portfolio.Student.AvatarUrl,
            Theme = DeserializeTheme(portfolio.ThemeConfig),
            Links = DeserializeLinks(portfolio.Links),
            Items = items
                .Select(i => MapItemResponse(i, appendixByItemId.GetValueOrDefault(i.Id)))
                .ToList(),
        };
    }

    private async Task SyncCertificatesAsync(Portfolio portfolio, List<PortfolioCustomItem> items)
    {
        var certificates = await _unitOfWork.Certificates.GetAllAsync(
            c => c.StudentId == portfolio.StudentId && !c.IsDeleted);

        if (certificates.Count == 0)
        {
            return;
        }

        var programIds = certificates
            .Where(c => c.ProgramId.HasValue)
            .Select(c => c.ProgramId!.Value)
            .Distinct()
            .ToList();
        var moduleIds = certificates
            .Where(c => c.ModuleId.HasValue)
            .Select(c => c.ModuleId!.Value)
            .Distinct()
            .ToList();

        var programs = programIds.Count == 0
            ? new Dictionary<Guid, Program>()
            : (await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id) && !p.IsDeleted))
                .ToDictionary(p => p.Id);
        var modules = moduleIds.Count == 0
            ? new Dictionary<Guid, Module>()
            : (await _unitOfWork.Modules.GetAllAsync(m => moduleIds.Contains(m.Id) && !m.IsDeleted))
                .ToDictionary(m => m.Id);

        var nextOrder = items.Count == 0 ? 0 : items.Max(i => i.DisplayOrder) + 1;

        foreach (var certificate in certificates)
        {
            var existing = items.FirstOrDefault(
                i => i.ItemType == PortfolioItemType.InternalCertificate
                     && i.ReferenceId == certificate.Id
                     && !i.IsDeleted);

            programs.TryGetValue(certificate.ProgramId ?? Guid.Empty, out var program);
            modules.TryGetValue(certificate.ModuleId ?? Guid.Empty, out var module);

            var title = certificate.ModuleId.HasValue
                ? module?.Name ?? "Module Certificate"
                : program?.Name ?? "Program Certificate";

            if (existing == null)
            {
                var item = new PortfolioCustomItem
                {
                    PortfolioId = portfolio.Id,
                    ItemType = PortfolioItemType.InternalCertificate,
                    ReferenceId = certificate.Id,
                    ProgramId = certificate.ProgramId,
                    ModuleId = certificate.ModuleId,
                    Title = title,
                    MediaUrl = certificate.PdfUrl,
                    DisplayOrder = nextOrder++,
                    IsVisible = true,
                    Source = PortfolioItemSource.AutoImported,
                };

                await _unitOfWork.PortfolioCustomItems.AddAsync(item);
                items.Add(item);
                continue;
            }

            if (existing.Source == PortfolioItemSource.StudentEdited)
            {
                continue;
            }

            existing.ProgramId = certificate.ProgramId;
            existing.ModuleId = certificate.ModuleId;
            existing.Title = title;
            existing.MediaUrl = certificate.PdfUrl;
            await _unitOfWork.PortfolioCustomItems.Update(existing);
        }
    }

    private async Task SyncCapstoneProjectsAsync(Portfolio portfolio, List<PortfolioCustomItem> items)
    {
        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.StudentId == portfolio.StudentId
                 && !s.IsDeleted
                 && s.Status == SubmissionStatus.Graded
                 && s.ResearchMilestoneId != null);

        if (submissions.Count == 0)
        {
            return;
        }

        var milestoneIds = submissions
            .Select(s => s.ResearchMilestoneId!.Value)
            .Distinct()
            .ToList();

        var milestones = (await _unitOfWork.ResearchMilestones.GetAllAsync(
                m => milestoneIds.Contains(m.Id) && m.IsCapstone && !m.IsDeleted))
            .ToDictionary(m => m.Id);

        var capstoneSubmissions = submissions
            .Where(s => milestones.ContainsKey(s.ResearchMilestoneId!.Value))
            .ToList();

        if (capstoneSubmissions.Count == 0)
        {
            return;
        }

        var moduleEnrollmentIds = capstoneSubmissions
            .Where(s => s.ModuleEnrollmentId.HasValue)
            .Select(s => s.ModuleEnrollmentId!.Value)
            .Distinct()
            .ToList();

        var moduleEnrollments = moduleEnrollmentIds.Count == 0
            ? new Dictionary<Guid, ModuleEnrollment>()
            : (await _unitOfWork.ModuleEnrollments.GetAllAsync(
                    me => moduleEnrollmentIds.Contains(me.Id) && !me.IsDeleted))
                .ToDictionary(me => me.Id);

        var moduleIds = moduleEnrollments.Values
            .Select(me => me.ModuleId)
            .Distinct()
            .ToList();

        var modules = moduleIds.Count == 0
            ? new Dictionary<Guid, Module>()
            : (await _unitOfWork.Modules.GetAllAsync(m => moduleIds.Contains(m.Id) && !m.IsDeleted))
                .ToDictionary(m => m.Id);

        var nextOrder = items.Count == 0 ? 0 : items.Max(i => i.DisplayOrder) + 1;

        foreach (var submission in capstoneSubmissions)
        {
            var milestone = milestones[submission.ResearchMilestoneId!.Value];
            moduleEnrollments.TryGetValue(submission.ModuleEnrollmentId ?? Guid.Empty, out var moduleEnrollment);
            modules.TryGetValue(moduleEnrollment?.ModuleId ?? Guid.Empty, out var module);

            var existing = items.FirstOrDefault(
                i => i.ItemType == PortfolioItemType.CapstoneProject
                     && i.SubmissionId == submission.Id
                     && !i.IsDeleted);

            var title = module?.Name ?? milestone.Title;

            if (existing == null)
            {
                var item = new PortfolioCustomItem
                {
                    PortfolioId = portfolio.Id,
                    ItemType = PortfolioItemType.CapstoneProject,
                    SubmissionId = submission.Id,
                    ModuleEnrollmentId = submission.ModuleEnrollmentId,
                    ModuleId = moduleEnrollment?.ModuleId,
                    ProgramEnrollmentId = moduleEnrollment?.ProgramEnrollmentId,
                    ProgramId = module?.ProgramId,
                    Title = title,
                    Description = submission.ContentText,
                    MentorEndorsement = submission.MentorFeedback,
                    MediaUrl = submission.FileUrl,
                    DisplayOrder = nextOrder++,
                    IsVisible = true,
                    Source = PortfolioItemSource.AutoImported,
                };

                await _unitOfWork.PortfolioCustomItems.AddAsync(item);
                items.Add(item);
                await SyncCapstoneAppendixAsync(item, submission, portfolio.StudentId);
                continue;
            }

            if (existing.Source != PortfolioItemSource.StudentEdited)
            {
                existing.ModuleEnrollmentId = submission.ModuleEnrollmentId;
                existing.ModuleId = moduleEnrollment?.ModuleId;
                existing.ProgramEnrollmentId = moduleEnrollment?.ProgramEnrollmentId;
                existing.ProgramId = module?.ProgramId;
                existing.Title = title;
                existing.Description = submission.ContentText;
                existing.MentorEndorsement = submission.MentorFeedback;
                existing.MediaUrl = submission.FileUrl;
                await _unitOfWork.PortfolioCustomItems.Update(existing);
            }

            await SyncCapstoneAppendixAsync(existing, submission, portfolio.StudentId);
        }
    }

    private async Task SyncCapstoneAppendixAsync(
        PortfolioCustomItem capstoneItem,
        Submission capstoneSubmission,
        Guid studentId)
    {
        if (!capstoneSubmission.ModuleEnrollmentId.HasValue)
        {
            return;
        }

        var moduleEnrollmentId = capstoneSubmission.ModuleEnrollmentId.Value;
        var priorSubmissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.StudentId == studentId
                 && !s.IsDeleted
                 && s.ModuleEnrollmentId == moduleEnrollmentId
                 && s.Status == SubmissionStatus.Graded
                 && s.ResearchMilestoneId != null
                 && s.Id != capstoneSubmission.Id);

        if (priorSubmissions.Count == 0)
        {
            return;
        }

        var milestoneIds = priorSubmissions
            .Select(s => s.ResearchMilestoneId!.Value)
            .Distinct()
            .ToList();

        var milestones = (await _unitOfWork.ResearchMilestones.GetAllAsync(
                m => milestoneIds.Contains(m.Id) && !m.IsCapstone && !m.IsDeleted))
            .ToDictionary(m => m.Id);

        var existingAppendix = await _unitOfWork.PortfolioItemSubmissions.GetAllAsync(
            a => a.PortfolioCustomItemId == capstoneItem.Id && !a.IsDeleted);

        var order = existingAppendix.Count == 0 ? 0 : existingAppendix.Max(a => a.DisplayOrder) + 1;

        foreach (var priorSubmission in priorSubmissions)
        {
            if (!milestones.TryGetValue(priorSubmission.ResearchMilestoneId!.Value, out var milestone))
            {
                continue;
            }

            if (existingAppendix.Any(a => a.SubmissionId == priorSubmission.Id))
            {
                continue;
            }

            var appendix = new PortfolioItemSubmission
            {
                PortfolioCustomItemId = capstoneItem.Id,
                SubmissionId = priorSubmission.Id,
                SectionTitle = milestone.Title,
                DisplayOrder = order++,
            };

            await _unitOfWork.PortfolioItemSubmissions.AddAsync(appendix);
            existingAppendix.Add(appendix);
        }
    }

    private async Task ApplySubdomainAsync(Portfolio portfolio, string subdomainInput)
    {
        var normalized = PortfolioSubdomainValidator.Normalize(subdomainInput);
        if (normalized == null)
        {
            if (portfolio.IsPublic)
            {
                throw ErrorHelper.BadRequest("A subdomain is required while the portfolio is public.");
            }

            portfolio.Subdomain = null;
            return;
        }

        if (!PortfolioSubdomainValidator.TryValidateFormat(normalized, out var reason))
        {
            throw ErrorHelper.BadRequest(reason ?? "Invalid subdomain.");
        }

        var availability = await BuildSubdomainAvailabilityAsync(normalized, portfolio.Id);
        if (!availability.Available)
        {
            throw ErrorHelper.Conflict(availability.Reason ?? "Subdomain is already taken.");
        }

        portfolio.Subdomain = normalized;
    }

    private static void EnsureCanPublish(Portfolio portfolio)
    {
        if (string.IsNullOrWhiteSpace(portfolio.Subdomain))
        {
            throw ErrorHelper.BadRequest(
                "Set and verify a unique subdomain before publishing your portfolio.");
        }

        if (!PortfolioSubdomainValidator.TryValidateFormat(portfolio.Subdomain, out var reason))
        {
            throw ErrorHelper.BadRequest(reason ?? "Invalid subdomain.");
        }
    }

    private static void ApplyTheme(Portfolio portfolio, ThemeConfigDto theme)
    {
        portfolio.ThemeConfig = JsonSerializer.Serialize(theme, JsonOptions);
        portfolio.TemplateId = theme.TemplateId;
        portfolio.PrimaryColor = theme.PrimaryColor;
    }

    private async Task<SubdomainAvailabilityResponseDto> BuildSubdomainAvailabilityAsync(
        string subdomainInput,
        Guid excludePortfolioId)
    {
        var normalized = PortfolioSubdomainValidator.Normalize(subdomainInput);
        if (normalized == null)
        {
            return new SubdomainAvailabilityResponseDto
            {
                Subdomain = string.Empty,
                Available = false,
                Reason = "Subdomain is required.",
            };
        }

        if (!PortfolioSubdomainValidator.TryValidateFormat(normalized, out var reason))
        {
            return new SubdomainAvailabilityResponseDto
            {
                Subdomain = normalized,
                Available = false,
                Reason = reason,
            };
        }

        var taken = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.Subdomain == normalized && p.Id != excludePortfolioId && !p.IsDeleted);

        return new SubdomainAvailabilityResponseDto
        {
            Subdomain = normalized,
            Available = taken == null,
            Reason = taken == null ? null : "This subdomain is already taken.",
        };
    }

    private async Task<User> GetCurrentStudentAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        if (user.Role != RoleType.Student)
        {
            throw ErrorHelper.Forbidden("Only students can manage portfolios.");
        }

        return user;
    }

    private async Task<Portfolio> GetRootPortfolioForStudentOrThrowAsync(Guid studentId)
    {
        var portfolio = await _unitOfWork.Portfolios.FirstOrDefaultAsync(
            p => p.StudentId == studentId && p.ParentPortfolioId == null && !p.IsDeleted,
            p => p.Student);

        if (portfolio == null)
        {
            throw ErrorHelper.NotFound("Portfolio not found.");
        }

        return portfolio;
    }

    private async Task<PortfolioCustomItem> GetOwnedItemOrThrowAsync(Guid portfolioId, Guid itemId)
    {
        var item = await _unitOfWork.PortfolioCustomItems.FirstOrDefaultAsync(
            i => i.Id == itemId && i.PortfolioId == portfolioId && !i.IsDeleted,
            i => i.Program!,
            i => i.Module!);

        if (item == null)
        {
            throw ErrorHelper.NotFound($"Portfolio item with id '{itemId}' not found.");
        }

        return item;
    }

    private async Task<List<PortfolioCustomItem>> GetPortfolioItemsAsync(Guid portfolioId)
    {
        return (await _unitOfWork.PortfolioCustomItems.GetAllAsync(
                i => i.PortfolioId == portfolioId && !i.IsDeleted,
                i => i.Program!,
                i => i.Module!))
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.CreatedAt)
            .ToList();
    }

    private async Task<Dictionary<Guid, List<PortfolioAppendixItemDto>>> LoadAppendixByItemIdAsync(
        List<Guid> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<Guid, List<PortfolioAppendixItemDto>>();
        }

        var appendixRows = await _unitOfWork.PortfolioItemSubmissions.GetAllAsync(
            a => itemIds.Contains(a.PortfolioCustomItemId) && !a.IsDeleted,
            a => a.Submission);

        var milestoneIds = appendixRows
            .Where(a => a.Submission.ResearchMilestoneId.HasValue)
            .Select(a => a.Submission.ResearchMilestoneId!.Value)
            .Distinct()
            .ToList();

        var milestones = milestoneIds.Count == 0
            ? new Dictionary<Guid, ResearchMilestone>()
            : (await _unitOfWork.ResearchMilestones.GetAllAsync(
                    m => milestoneIds.Contains(m.Id) && !m.IsDeleted))
                .ToDictionary(m => m.Id);

        return appendixRows
            .GroupBy(a => a.PortfolioCustomItemId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(a => a.DisplayOrder)
                    .Select(a =>
                    {
                        milestones.TryGetValue(
                            a.Submission.ResearchMilestoneId ?? Guid.Empty,
                            out var milestone);

                        return new PortfolioAppendixItemDto
                        {
                            Id = a.Id,
                            SubmissionId = a.SubmissionId,
                            SectionTitle = a.SectionTitle,
                            DisplayOrder = a.DisplayOrder,
                            ContentText = a.Submission.ContentText,
                            FileUrl = a.Submission.FileUrl,
                            AssignedGrade = a.Submission.AssignedGrade,
                            MilestoneTitle = milestone?.Title,
                        };
                    })
                    .ToList());
    }

    private async Task<PortfolioResponseDto> MapPortfolioResponseAsync(Portfolio portfolio)
    {
        var student = portfolio.Student
            ?? await _unitOfWork.Users.GetByIdAsync(portfolio.StudentId);

        var items = await GetPortfolioItemsAsync(portfolio.Id);
        var appendixByItemId = await LoadAppendixByItemIdAsync(items.Select(i => i.Id).ToList());

        return new PortfolioResponseDto
        {
            Id = portfolio.Id,
            Code = portfolio.Code,
            StudentId = portfolio.StudentId,
            StudentName = student?.FullName,
            AvatarUrl = student?.AvatarUrl,
            Subdomain = portfolio.Subdomain,
            DisplayName = portfolio.DisplayName,
            Headline = portfolio.Headline,
            Tagline = portfolio.Tagline,
            Summary = portfolio.Summary,
            PlanType = portfolio.PlanType,
            IsPublic = portfolio.IsPublic,
            Theme = DeserializeTheme(portfolio.ThemeConfig),
            Links = DeserializeLinks(portfolio.Links),
            Items = items
                .Select(i => MapItemResponse(i, appendixByItemId.GetValueOrDefault(i.Id)))
                .ToList(),
            CreatedAt = portfolio.CreatedAt,
            UpdatedAt = portfolio.UpdatedAt,
        };
    }

    private static PortfolioCustomItemResponseDto MapItemResponse(
        PortfolioCustomItem item,
        List<PortfolioAppendixItemDto>? appendixSections = null)
    {
        return new PortfolioCustomItemResponseDto
        {
            Id = item.Id,
            ItemType = item.ItemType,
            Title = item.Title,
            Subtitle = item.Subtitle,
            Organization = item.Organization,
            StartDate = item.StartDate,
            EndDate = item.EndDate,
            Description = item.Description,
            MentorEndorsement = item.MentorEndorsement,
            StudentEditedBody = item.StudentEditedBody,
            MediaUrl = item.MediaUrl,
            ExternalUrl = item.ExternalUrl,
            DisplayOrder = item.DisplayOrder,
            IsVisible = item.IsVisible,
            Source = item.Source,
            ProgramId = item.ProgramId,
            ProgramName = item.Program?.Name,
            ModuleId = item.ModuleId,
            ModuleName = item.Module?.Name,
            ModuleEnrollmentId = item.ModuleEnrollmentId,
            SubmissionId = item.SubmissionId,
            AppendixSections = appendixSections ?? [],
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };
    }

    private static ThemeConfigDto? DeserializeTheme(string? themeConfig)
    {
        if (string.IsNullOrWhiteSpace(themeConfig))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ThemeConfigDto>(themeConfig, JsonOptions);
    }

    private static List<PortfolioLinkDto> DeserializeLinks(string? linksJson)
    {
        if (string.IsNullOrWhiteSpace(linksJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<PortfolioLinkDto>>(linksJson, JsonOptions) ?? [];
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<string> GenerateUniquePortfolioCodeAsync()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
            var code = $"OBOX-PF-{suffix}";
            var collision = await _unitOfWork.Portfolios.FirstOrDefaultAsync(p => p.Code == code);
            if (collision == null)
            {
                return code;
            }
        }

        throw ErrorHelper.Internal("Failed to generate a unique portfolio code.");
    }
}
