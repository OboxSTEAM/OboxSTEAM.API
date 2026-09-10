using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramBundleDTO;
using OboxSteam.Application.DTOs.VoucherDTO;
using OboxSteam.Application.Interfaces;
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

    public async Task<ProgramBundleResponseDto> CreateBundle(CreateProgramBundleRequestDto request)
    {
        var code = ProgramBundleValidator.NormalizeCode(request.Code);
        var name = ProgramBundleValidator.ValidateName(request.Name);
        ProgramBundleValidator.ValidatePrice(request.Price);
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
            Price = request.Price,
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

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("[CreateBundle] Draft bundle {Code} ({BundleId}).", code, bundle.Id);
        return await MapToResponse(bundle);
    }

    public async Task<ProgramBundleResponseDto> PublishBundle(Guid bundleId)
    {
        var bundle = ProgramBundleValidator.RequireExisting(
            await _unitOfWork.ProgramBundles.GetByIdAsync(bundleId),
            bundleId);

        var items = await _unitOfWork.ProgramBundleItems.GetAllAsync(
            i => i.BundleId == bundle.Id && !i.IsDeleted);
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
            Price = bundle.Price,
            RetailTotal = retailTotal,
            Status = bundle.Status,
            Items = itemDtos,
            CreatedAt = bundle.CreatedAt,
            UpdatedAt = bundle.UpdatedAt,
        };
    }

    private static bool HasId(Guid? id) => id.HasValue && id.Value != Guid.Empty;
}
