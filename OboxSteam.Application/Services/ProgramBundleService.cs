using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramBundleDTO;
using OboxSteam.Application.DTOs.VoucherDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ProgramBundleService : IProgramBundleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IVoucherService _voucherService;
    private readonly ILogger<ProgramBundleService> _logger;

    public ProgramBundleService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IVoucherService voucherService,
        ILogger<ProgramBundleService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _voucherService = voucherService;
        _logger = logger;
    }

    public async Task<BundlePriceQuoteDto> GetPriceQuote(Guid bundleId, Guid studentId, string? voucherCode)
    {
        await VoucherValidator.EnsureCallerCanActForStudent(_unitOfWork, _claimsService, studentId);

        var quote = await BundlePricingHelper.ComputeOwnershipQuote(_unitOfWork, studentId, bundleId);

        VoucherPreviewDto? voucher = null;
        var finalPrice = quote.PriceAfterOwnership;
        if (!string.IsNullOrWhiteSpace(voucherCode))
        {
            voucher = await _voucherService.PreviewVoucher(studentId, new PreviewVoucherRequestDto
            {
                Code = voucherCode,
                BundleId = bundleId,
            });
            if (voucher.IsValid)
                finalPrice = voucher.FinalAmount;
        }

        _logger.LogInformation(
            "[GetPriceQuote] Bundle {BundleId} student {StudentId} afterOwnership={AfterOwnership} final={FinalPrice}.",
            bundleId,
            studentId,
            quote.PriceAfterOwnership,
            finalPrice);

        return new BundlePriceQuoteDto
        {
            BundleId = quote.BundleId,
            BundleName = quote.BundleName,
            BundlePrice = quote.BundlePrice,
            OwnedPrograms = quote.OwnedPrograms
                .Select(line => new OwnedProgramDeductionDto
                {
                    ProgramId = line.ProgramId,
                    Name = line.Name,
                    DeductedPrice = line.DeductedPrice,
                })
                .ToList(),
            OwnershipDeduction = quote.OwnershipDeduction,
            PriceAfterOwnership = quote.PriceAfterOwnership,
            Voucher = voucher,
            FinalPrice = finalPrice,
        };
    }

    public async Task<Pagination<ProgramBundleResponseDto>> GetAllBundles(
        string? search,
        ProgramBundleStatus? status,
        ProgramCategory? category,
        int page,
        int pageSize)
    {
        var query = _unitOfWork.ProgramBundles.GetQueryable().Where(b => !b.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(b =>
                b.Code.ToUpper().Contains(term) || b.Name.ToUpper().Contains(term));
        }

        if (await CallerSeesActiveCatalogOnly())
            query = query.Where(b => b.Status == ProgramBundleStatus.Active);
        else if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        if (category.HasValue)
            query = query.Where(b => b.Category == category.Value);

        query = query.OrderByDescending(b => b.CreatedAt);

        var totalCount = query.Count();
        var bundles = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = bundles.Select(MapToListItem).ToList();
        return new Pagination<ProgramBundleResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ProgramBundleResponseDto> GetBundleById(Guid bundleId)
    {
        var bundle = ProgramBundleValidator.RequireExisting(
            await _unitOfWork.ProgramBundles.GetByIdAsync(bundleId),
            bundleId);
        return await MapToResponse(bundle);
    }

    public async Task<Pagination<MyBundlePathwayDto>> GetMyPathways(int page, int pageSize)
    {
        var currentUser = await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ViewPathwayForbiddenMessage);

        var enrollments = (await LoadScopedPurchasedEnrollmentsAsync(currentUser))
            .OrderBy(e => e.Status == BundleEnrollmentStatus.Active ? 0 : 1)
            .ThenByDescending(e => e.CreatedAt)
            .ToList();

        var totalCount = enrollments.Count;
        var pageRows = enrollments
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = await MapPathwaysAsync(pageRows);
        _logger.LogInformation(
            "[GetMyPathways] User {UserId} retrieved {Count}/{Total} pathways.",
            currentUser.Id,
            dtos.Count,
            totalCount);
        return new Pagination<MyBundlePathwayDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<MyBundlePathwayDto> GetMyPathwayByEnrollmentId(Guid bundleEnrollmentId)
    {
        var currentUser = await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ViewPathwayForbiddenMessage);

        var enrollment = await _unitOfWork.BundleEnrollments.GetByIdAsync(bundleEnrollmentId);
        if (enrollment == null
            || enrollment.IsDeleted
            || enrollment.Status == BundleEnrollmentStatus.PendingPayment)
        {
            throw ErrorHelper.NotFound($"Bundle enrollment '{bundleEnrollmentId}' not found.");
        }

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            enrollment.StudentId,
            ViewPathwayForbiddenMessage);

        var dtos = await MapPathwaysAsync([enrollment]);
        _logger.LogInformation(
            "[GetMyPathwayByEnrollmentId] User {UserId} loaded bundle enrollment {EnrollmentId}.",
            currentUser.Id,
            bundleEnrollmentId);
        return dtos[0];
    }

    public async Task<ProgramBundleResponseDto> CreateBundle(CreateProgramBundleRequestDto request)
    {
        var code = ProgramBundleValidator.NormalizeCode(request.Code);
        var name = ProgramBundleValidator.ValidateName(request.Name);
        var pricePercent = ProgramBundleValidator.ValidatePricePercent(request.PricePercent);
        ProgramBundleValidator.ValidateCategory(request.Category);
        await ProgramBundleValidator.EnsureCodeIsUnique(_unitOfWork, code);

        if (request.FrameworkId.HasValue && request.FrameworkId.Value != Guid.Empty)
            await ProgramBundleValidator.EnsureFrameworkExists(_unitOfWork, request.FrameworkId.Value);

        var itemRequests = request.Items ?? [];
        ProgramBundleValidator.ValidateItemPayload(itemRequests);
        await ProgramBundleValidator.LoadProgramsForItems(_unitOfWork, itemRequests);

        var bundle = new ProgramBundle
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ThumbnailUrl = string.IsNullOrWhiteSpace(request.ThumbnailUrl) ? null : request.ThumbnailUrl.Trim(),
            Category = request.Category,
            FrameworkId = HasId(request.FrameworkId) ? request.FrameworkId : null,
            PricePercent = pricePercent,
            Price = 0,
            Status = ProgramBundleStatus.Draft,
        };

        await _unitOfWork.ProgramBundles.AddAsync(bundle);

        var order = 1;
        foreach (var item in itemRequests)
        {
            await _unitOfWork.ProgramBundleItems.AddAsync(new ProgramBundleItem
            {
                Id = Guid.NewGuid(),
                BundleId = bundle.Id,
                ProgramId = item.ProgramId,
                SortOrder = item.SortOrder ?? order,
                RequiresPreviousCompletion = item.RequiresPreviousCompletion,
            });
            order++;
        }

        await RecalculatePersistedPrice(bundle);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("[CreateBundle] Draft bundle {Code} ({BundleId}).", code, bundle.Id);
        return await MapToResponse(bundle);
    }

    public async Task<ProgramBundleResponseDto> UpdateBundle(
        Guid bundleId,
        UpdateProgramBundleRequestDto request)
    {
        var bundle = ProgramBundleValidator.RequireExisting(
            await _unitOfWork.ProgramBundles.GetByIdAsync(bundleId),
            bundleId);

        var name = ProgramBundleValidator.ValidateName(request.Name);
        var pricePercent = ProgramBundleValidator.ValidatePricePercent(request.PricePercent);
        ProgramBundleValidator.ValidateCategory(request.Category);

        if (request.FrameworkId.HasValue && request.FrameworkId.Value != Guid.Empty)
            await ProgramBundleValidator.EnsureFrameworkExists(_unitOfWork, request.FrameworkId.Value);

        bundle.Name = name;
        bundle.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        bundle.ThumbnailUrl = string.IsNullOrWhiteSpace(request.ThumbnailUrl) ? null : request.ThumbnailUrl.Trim();
        bundle.Category = request.Category;
        bundle.FrameworkId = HasId(request.FrameworkId) ? request.FrameworkId : null;
        bundle.PricePercent = pricePercent;

        await RecalculatePersistedPrice(bundle);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("[UpdateBundle] Bundle {Code} ({BundleId}) updated.", bundle.Code, bundle.Id);
        return await MapToResponse(bundle);
    }

    public async Task<ProgramBundleResponseDto> AddBundleItem(
        Guid bundleId,
        CreateProgramBundleItemRequestDto request)
    {
        var bundle = ProgramBundleValidator.RequireExisting(
            await _unitOfWork.ProgramBundles.GetByIdAsync(bundleId),
            bundleId);
        ProgramBundleValidator.EnsureItemsMutable(bundle);

        var payload = new List<CreateProgramBundleItemRequestDto> { request };
        ProgramBundleValidator.ValidateItemPayload(payload);
        await ProgramBundleValidator.LoadProgramsForItems(_unitOfWork, payload);

        var items = await LoadItems(bundle.Id);
        ProgramBundleValidator.EnsureProgramNotInBundle(items, request.ProgramId);

        var sortOrder = request.SortOrder ?? NextSortOrder(items);
        ProgramBundleValidator.EnsureSortOrderAvailable(items, sortOrder);

        await _unitOfWork.ProgramBundleItems.AddAsync(new ProgramBundleItem
        {
            Id = Guid.NewGuid(),
            BundleId = bundle.Id,
            ProgramId = request.ProgramId,
            SortOrder = sortOrder,
            RequiresPreviousCompletion = request.RequiresPreviousCompletion,
        });

        await RecalculatePersistedPrice(bundle);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation(
            "[AddBundleItem] Program {ProgramId} added to bundle {BundleId}.",
            request.ProgramId,
            bundle.Id);
        return await MapToResponse(bundle);
    }

    public async Task<ProgramBundleResponseDto> UpdateBundleItem(
        Guid bundleId,
        Guid itemId,
        UpdateProgramBundleItemRequestDto request)
    {
        ProgramBundleValidator.ValidateItemUpdate(request);

        var bundle = ProgramBundleValidator.RequireExisting(
            await _unitOfWork.ProgramBundles.GetByIdAsync(bundleId),
            bundleId);
        ProgramBundleValidator.EnsureItemsMutable(bundle);

        var item = ProgramBundleValidator.RequireItem(
            await _unitOfWork.ProgramBundleItems.GetByIdAsync(itemId),
            bundleId,
            itemId);
        var items = await LoadItems(bundle.Id);

        var programChanged = false;
        if (request.ProgramId.HasValue)
        {
            await ProgramBundleValidator.LoadProgramsForItems(
                _unitOfWork,
                [new CreateProgramBundleItemRequestDto { ProgramId = request.ProgramId.Value }]);
            ProgramBundleValidator.EnsureProgramNotInBundle(items, request.ProgramId.Value, item.Id);
            item.ProgramId = request.ProgramId.Value;
            programChanged = true;
        }

        if (request.SortOrder.HasValue)
        {
            ProgramBundleValidator.EnsureSortOrderAvailable(items, request.SortOrder.Value, item.Id);
            item.SortOrder = request.SortOrder.Value;
        }

        if (request.RequiresPreviousCompletion.HasValue)
            item.RequiresPreviousCompletion = request.RequiresPreviousCompletion.Value;

        await _unitOfWork.ProgramBundleItems.Update(item);
        if (programChanged)
            await RecalculatePersistedPrice(bundle);

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("[UpdateBundleItem] Item {ItemId} on bundle {BundleId} updated.", itemId, bundle.Id);
        return await MapToResponse(bundle);
    }

    public async Task<ProgramBundleResponseDto> DeleteBundleItem(Guid bundleId, Guid itemId)
    {
        var bundle = ProgramBundleValidator.RequireExisting(
            await _unitOfWork.ProgramBundles.GetByIdAsync(bundleId),
            bundleId);
        ProgramBundleValidator.EnsureItemsMutable(bundle);

        var item = ProgramBundleValidator.RequireItem(
            await _unitOfWork.ProgramBundleItems.GetByIdAsync(itemId),
            bundleId,
            itemId);

        await _unitOfWork.ProgramBundleItems.SoftRemove(item);
        await RecalculatePersistedPrice(bundle);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("[DeleteBundleItem] Item {ItemId} removed from bundle {BundleId}.", itemId, bundle.Id);
        return await MapToResponse(bundle);
    }

    public async Task<ProgramBundleResponseDto> PublishBundle(Guid bundleId)
    {
        var bundle = ProgramBundleValidator.RequireExisting(
            await _unitOfWork.ProgramBundles.GetByIdAsync(bundleId),
            bundleId);

        var items = await LoadItems(bundle.Id);
        await RecalculatePersistedPrice(bundle);
        var programsById = new Dictionary<Guid, Program>();
        foreach (var programId in items.Select(i => i.ProgramId).Distinct())
        {
            var program = await _unitOfWork.Programs.GetByIdAsync(programId);
            if (program != null)
                programsById[programId] = program;
        }

        ProgramBundleValidator.ValidateForPublish(bundle, items, programsById);

        bundle.Status = ProgramBundleStatus.Active;
        await _unitOfWork.ProgramBundles.Update(bundle);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[PublishBundle] Bundle {Code} ({BundleId}) is Active.", bundle.Code, bundle.Id);
        return await MapToResponse(bundle);
    }

    private async Task<bool> CallerSeesActiveCatalogOnly()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
            return true;

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        return user is null || user.IsDeleted || user.Role is not (RoleType.Admin or RoleType.Manager);
    }

    private static ProgramBundleResponseDto MapToListItem(ProgramBundle bundle) => new()
    {
        Id = bundle.Id,
        Code = bundle.Code,
        Name = bundle.Name,
        Description = bundle.Description,
        ThumbnailUrl = bundle.ThumbnailUrl,
        Category = bundle.Category,
        FrameworkId = bundle.FrameworkId,
        PricePercent = bundle.PricePercent,
        Price = bundle.Price,
        RetailTotal = 0,
        Status = bundle.Status,
        Items = [],
        CreatedAt = bundle.CreatedAt,
        UpdatedAt = bundle.UpdatedAt,
    };

    private async Task<ProgramBundleResponseDto> MapToResponse(ProgramBundle bundle)
    {
        var items = (await _unitOfWork.ProgramBundleItems.GetAllAsync(
                i => i.BundleId == bundle.Id && !i.IsDeleted))
            .OrderBy(i => i.SortOrder)
            .ToList();

        var itemDtos = new List<ProgramBundleItemResponseDto>(items.Count);
        decimal retailTotal = 0;
        foreach (var item in items)
        {
            var program = await _unitOfWork.Programs.GetByIdAsync(item.ProgramId);
            var programPrice = program?.Price ?? 0;
            retailTotal += programPrice;
            itemDtos.Add(new ProgramBundleItemResponseDto
            {
                Id = item.Id,
                ProgramId = item.ProgramId,
                ProgramName = program?.Name ?? string.Empty,
                ProgramPrice = programPrice,
                SortOrder = item.SortOrder,
                RequiresPreviousCompletion = item.RequiresPreviousCompletion,
            });
        }

        return new ProgramBundleResponseDto
        {
            Id = bundle.Id,
            Code = bundle.Code,
            Name = bundle.Name,
            Description = bundle.Description,
            ThumbnailUrl = bundle.ThumbnailUrl,
            Category = bundle.Category,
            FrameworkId = bundle.FrameworkId,
            PricePercent = bundle.PricePercent,
            Price = bundle.Price,
            RetailTotal = retailTotal,
            Status = bundle.Status,
            Items = itemDtos,
            CreatedAt = bundle.CreatedAt,
            UpdatedAt = bundle.UpdatedAt,
        };
    }

    private async Task RecalculatePersistedPrice(ProgramBundle bundle)
    {
        var items = await LoadItems(bundle.Id);
        var retailTotal = await BundlePricingHelper.ComputeRetailTotalAsync(_unitOfWork, items);
        bundle.Price = BundlePricingHelper.ComputePriceFromPercent(retailTotal, bundle.PricePercent);
        await _unitOfWork.ProgramBundles.Update(bundle);
    }

    private async Task<List<ProgramBundleItem>> LoadItems(Guid bundleId)
    {
        return (await _unitOfWork.ProgramBundleItems.GetAllAsync(
                i => i.BundleId == bundleId && !i.IsDeleted))
            .OrderBy(i => i.SortOrder)
            .ToList();
    }

    private static int NextSortOrder(IReadOnlyList<ProgramBundleItem> items)
        => items.Count == 0 ? 1 : items.Max(i => i.SortOrder) + 1;

    private static bool HasId(Guid? id) => id.HasValue && id.Value != Guid.Empty;

    private const string ViewPathwayForbiddenMessage = "You do not have permission to view bundle pathways.";

    private async Task<List<BundleEnrollment>> LoadScopedPurchasedEnrollmentsAsync(User currentUser)
    {
        var query = _unitOfWork.BundleEnrollments
            .GetQueryable()
            .Where(e => !e.IsDeleted
                        && (e.Status == BundleEnrollmentStatus.Active
                            || e.Status == BundleEnrollmentStatus.Completed));

        if (currentUser.Role == RoleType.Student)
        {
            query = query.Where(e => e.StudentId == currentUser.Id);
        }
        else if (currentUser.Role == RoleType.Parent)
        {
            var parentLinks = await _unitOfWork.ParentStudents.GetAllAsync(
                ps => ps.ParentId == currentUser.Id && ps.IsVerified && !ps.IsDeleted);
            var linkedStudentIds = parentLinks.Select(ps => ps.StudentId).Distinct().ToList();
            query = linkedStudentIds.Count == 0
                ? query.Where(e => false)
                : query.Where(e => linkedStudentIds.Contains(e.StudentId));
        }

        return query.ToList();
    }

    private async Task<List<MyBundlePathwayDto>> MapPathwaysAsync(IReadOnlyList<BundleEnrollment> enrollments)
    {
        if (enrollments.Count == 0)
            return [];

        var bundleIds = enrollments.Select(e => e.BundleId).Distinct().ToList();
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

        var bundles = (await _unitOfWork.ProgramBundles.GetAllAsync(
                b => bundleIds.Contains(b.Id) && !b.IsDeleted))
            .ToDictionary(b => b.Id);

        var items = (await _unitOfWork.ProgramBundleItems.GetAllAsync(
                i => bundleIds.Contains(i.BundleId) && !i.IsDeleted))
            .OrderBy(i => i.SortOrder)
            .ToList();
        var itemsByBundleId = items
            .GroupBy(i => i.BundleId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortOrder).ToList());

        var programIds = items.Select(i => i.ProgramId).Distinct().ToList();
        var programs = programIds.Count == 0
            ? []
            : await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id) && !p.IsDeleted);
        var programsById = programs.ToDictionary(p => p.Id);

        var programEnrollments = programIds.Count == 0
            ? []
            : await _unitOfWork.ProgramEnrollments.GetAllAsync(
                pe => studentIds.Contains(pe.StudentId)
                      && programIds.Contains(pe.ProgramId)
                      && !pe.IsDeleted);

        var certificates = await _unitOfWork.Certificates.GetAllAsync(
            c => studentIds.Contains(c.StudentId)
                 && c.BundleId.HasValue
                 && bundleIds.Contains(c.BundleId.Value)
                 && c.ProgramId == null
                 && c.ModuleId == null
                 && !c.IsDeleted);
        var certificateByStudentAndBundle = certificates
            .GroupBy(c => (c.StudentId, c.BundleId!.Value))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.IssueDate ?? c.CreatedAt).First());

        var result = new List<MyBundlePathwayDto>(enrollments.Count);
        foreach (var enrollment in enrollments)
        {
            bundles.TryGetValue(enrollment.BundleId, out var bundle);
            var bundleItems = itemsByBundleId.GetValueOrDefault(enrollment.BundleId) ?? [];
            var completedProgramIds = programEnrollments
                .Where(pe => pe.StudentId == enrollment.StudentId
                             && pe.Status == EnrollmentStatus.Completed)
                .Select(pe => pe.ProgramId)
                .ToHashSet();

            var itemDtos = new List<MyBundlePathwayItemDto>(bundleItems.Count);
            foreach (var item in bundleItems)
            {
                var current = BundleEnrollmentHelper.SelectCurrentProgramEnrollment(
                    programEnrollments,
                    enrollment.StudentId,
                    item.ProgramId);
                programsById.TryGetValue(item.ProgramId, out var program);
                itemDtos.Add(new MyBundlePathwayItemDto
                {
                    ItemId = item.Id,
                    ProgramId = item.ProgramId,
                    ProgramEnrollmentId = current?.Id,
                    ProgramName = program?.Name ?? string.Empty,
                    ThumbnailUrl = program?.ThumbnailUrl,
                    SortOrder = item.SortOrder,
                    RequiresPreviousCompletion = item.RequiresPreviousCompletion,
                    Status = BundleEnrollmentHelper.ResolvePathwayItemStatus(
                        item,
                        bundleItems,
                        completedProgramIds,
                        current),
                    ProgressPercent = current?.ProgressPercent ?? 0m,
                });
            }

            certificateByStudentAndBundle.TryGetValue(
                (enrollment.StudentId, enrollment.BundleId),
                out var certificate);

            result.Add(new MyBundlePathwayDto
            {
                BundleEnrollmentId = enrollment.Id,
                BundleId = enrollment.BundleId,
                StudentId = enrollment.StudentId,
                BundleCode = bundle?.Code ?? string.Empty,
                BundleName = bundle?.Name ?? string.Empty,
                Description = bundle?.Description,
                ThumbnailUrl = bundle?.ThumbnailUrl,
                Status = enrollment.Status,
                ProgressPercent = enrollment.ProgressPercent,
                Items = itemDtos,
                Certificate = certificate == null
                    ? null
                    : new MyBundlePathwayCertificateDto
                    {
                        Id = certificate.Id,
                        Code = certificate.Code,
                        IssueDate = certificate.IssueDate,
                        PdfUrl = certificate.PdfUrl,
                        VerificationUrl = certificate.VerificationUrl,
                    },
                CreatedAt = enrollment.CreatedAt,
            });
        }

        return result;
    }
}
