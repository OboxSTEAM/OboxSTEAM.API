using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.VoucherDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class VoucherService : IVoucherService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly ILogger<VoucherService> _logger;

    public VoucherService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        ILogger<VoucherService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _logger = logger;
    }

    public async Task<Pagination<VoucherResponseDto>> GetAllVouchers(
        string? search,
        VoucherScope? scope,
        VoucherStatus? status,
        int page,
        int pageSize)
    {
        await ActivateDueVouchers();

        var query = _unitOfWork.Vouchers.GetQueryable().Where(v => !v.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(v => v.Code.ToUpper().Contains(term));
        }

        if (scope.HasValue)
            query = query.Where(v => v.Scope == scope.Value);

        if (status.HasValue)
            query = query.Where(v => v.Status == status.Value);

        query = query.OrderByDescending(v => v.CreatedAt);

        var totalCount = query.Count();
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var usageCounts = GetUsageCounts(items.Select(v => v.Id).ToList());
        var now = _currentTime.GetCurrentTime();
        var dtos = items
            .Select(v => MapToResponse(v, usageCounts.GetValueOrDefault(v.Id), now, includeUsages: false))
            .ToList();

        return new Pagination<VoucherResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<VoucherResponseDto> GetVoucherById(Guid voucherId)
    {
        var voucher = VoucherValidator.RequireExisting(
            await _unitOfWork.Vouchers.GetByIdAsync(voucherId),
            voucherId);
        var now = _currentTime.GetCurrentTime();
        await PersistActivationIfDue(voucher, now);
        var usages = await GetUsageItems(voucher.Id);
        return MapToResponse(voucher, usages.Count, now, includeUsages: true, usages);
    }

    public async Task<VoucherResponseDto> CreateVoucher(CreateVoucherRequestDto request)
    {
        var now = _currentTime.GetCurrentTime();
        var code = VoucherValidator.ValidateForCreate(request, now);
        await VoucherValidator.EnsureCodeIsUnique(_unitOfWork, code);

        var voucher = new Voucher
        {
            Id = Guid.NewGuid(),
            Code = code,
            PercentOff = request.PercentOff,
            AmountOff = request.AmountOff,
            StartsAt = request.StartsAt,
            ExpiryAt = request.ExpiryAt,
            UsageLimit = request.UsageLimit,
            MaxUsagePerStudent = request.MaxUsagePerStudent,
            Scope = request.Scope,
            Status = VoucherValidator.ResolveStatus(request.StartsAt, now),
        };

        await _unitOfWork.Vouchers.AddAsync(voucher);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[CreateVoucher] Issued voucher {Code} ({VoucherId}).", code, voucher.Id);
        return MapToResponse(voucher, 0, _currentTime.GetCurrentTime(), includeUsages: false);
    }

    public async Task<VoucherResponseDto> UpdateVoucher(Guid voucherId, UpdateVoucherRequestDto request)
    {
        var voucher = VoucherValidator.RequireExisting(
            await _unitOfWork.Vouchers.GetByIdAsync(voucherId),
            voucherId);
        var now = _currentTime.GetCurrentTime();
        var hasChange = false;
        var startsAt = voucher.StartsAt;
        var expiryAt = voucher.ExpiryAt;

        if (request.StartsAt.HasValue)
        {
            startsAt = request.StartsAt;
            hasChange = true;
        }

        if (request.ExpiryAt.HasValue)
        {
            VoucherValidator.ValidateExpiryIsInFuture(request.ExpiryAt, now);
            expiryAt = request.ExpiryAt;
            hasChange = true;
        }
        else if (request.ClearExpiryAt == true)
        {
            expiryAt = null;
            hasChange = true;
        }

        if (request.StartsAt.HasValue || request.ExpiryAt.HasValue || request.ClearExpiryAt == true)
        {
            VoucherValidator.ValidateValidityWindow(startsAt, expiryAt);
            voucher.StartsAt = startsAt;
            voucher.ExpiryAt = expiryAt;
        }

        if (request.UsageLimit.HasValue)
        {
            VoucherValidator.ValidatePositiveCap(request.UsageLimit, nameof(request.UsageLimit));
            VoucherValidator.ValidateUsageLimitNotBelowCurrent(
                request.UsageLimit.Value,
                CountSuccessfulUsages(voucher.Id));
            voucher.UsageLimit = request.UsageLimit;
            hasChange = true;
        }
        else if (request.ClearUsageLimit == true)
        {
            voucher.UsageLimit = null;
            hasChange = true;
        }

        if (request.MaxUsagePerStudent.HasValue)
        {
            VoucherValidator.ValidatePositiveCap(
                request.MaxUsagePerStudent,
                nameof(request.MaxUsagePerStudent));
            voucher.MaxUsagePerStudent = request.MaxUsagePerStudent;
            hasChange = true;
        }
        else if (request.ClearMaxUsagePerStudent == true)
        {
            voucher.MaxUsagePerStudent = null;
            hasChange = true;
        }

        if (request.Scope.HasValue)
        {
            VoucherValidator.ValidateScope(request.Scope.Value);
            voucher.Scope = request.Scope.Value;
            hasChange = true;
        }

        VoucherValidator.ValidateHasUpdate(hasChange);
        voucher.Status = VoucherValidator.ResolveStatus(voucher.StartsAt, now);

        await _unitOfWork.Vouchers.Update(voucher);
        await _unitOfWork.SaveChangesAsync();

        var usages = await GetUsageItems(voucher.Id);
        return MapToResponse(voucher, usages.Count, _currentTime.GetCurrentTime(), includeUsages: true, usages);
    }

    public async Task<bool> DeleteVoucher(Guid voucherId)
    {
        var voucher = await _unitOfWork.Vouchers.GetByIdAsync(voucherId);
        if (voucher == null || voucher.IsDeleted)
            return false;

        await _unitOfWork.Vouchers.SoftRemove(voucher);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("[DeleteVoucher] Disabled voucher {Code} ({VoucherId}).", voucher.Code, voucher.Id);
        return true;
    }

    public async Task<VoucherPreviewDto> PreviewVoucher(Guid studentId, PreviewVoucherRequestDto request)
    {
        await VoucherValidator.EnsureCallerCanActForStudent(_unitOfWork, _claimsService, studentId);
        var baseAmount = await ComputeBaseAmount(studentId, request);
        return await EvaluateVoucher(studentId, request, baseAmount, throwOnInvalid: false);
    }

    public async Task<VoucherPreviewDto> ValidateForCheckout(Guid studentId, PreviewVoucherRequestDto request)
    {
        await VoucherValidator.EnsureCallerCanActForStudent(_unitOfWork, _claimsService, studentId);
        var baseAmount = await ComputeBaseAmount(studentId, request);
        return await EvaluateVoucher(studentId, request, baseAmount, throwOnInvalid: true);
    }

    private async Task<VoucherPreviewDto> EvaluateVoucher(
        Guid studentId,
        PreviewVoucherRequestDto request,
        decimal baseAmount,
        bool throwOnInvalid)
    {
        var code = VoucherValidator.NormalizeCode(request.Code);
        var voucher = await _unitOfWork.Vouchers.FirstOrDefaultAsync(
            v => v.Code == code && !v.IsDeleted);
        var now = _currentTime.GetCurrentTime();
        if (voucher != null)
            await PersistActivationIfDue(voucher, now);

        var rejection = VoucherValidator.ValidateApply(
            voucher,
            voucher == null ? 0 : CountSuccessfulUsages(voucher.Id),
            voucher == null ? 0 : CountSuccessfulUsages(voucher.Id, studentId),
            request.BundleId,
            request.ProgramId,
            now);

        if (rejection.HasValue)
            return Fail(rejection.Value.ErrorCode, rejection.Value.Message, baseAmount, throwOnInvalid);

        if (voucher == null)
            return Fail(VoucherValidator.ApplyError.NotFound, "Voucher code was not found.", baseAmount, throwOnInvalid);

        var discount = ComputeVoucherDiscount(voucher, baseAmount);
        return new VoucherPreviewDto
        {
            IsValid = true,
            VoucherId = voucher.Id,
            Code = voucher.Code,
            Scope = voucher.Scope,
            PercentOff = voucher.PercentOff,
            AmountOff = voucher.AmountOff,
            DiscountAmount = discount,
            BaseAmount = baseAmount,
            FinalAmount = baseAmount - discount,
        };
    }

    private async Task<decimal> ComputeBaseAmount(Guid studentId, PreviewVoucherRequestDto request)
    {
        VoucherValidator.ValidatePreviewTarget(request.BundleId, request.ProgramId);

        if (request.BundleId.HasValue && request.BundleId.Value != Guid.Empty)
            return await ComputeBundleBaseAmount(studentId, request.BundleId.Value);

        return await ComputeProgramBaseAmount(request.ProgramId!.Value);
    }

    private async Task<decimal> ComputeBundleBaseAmount(Guid studentId, Guid bundleId)
    {
        var quote = await BundlePricingHelper.ComputeOwnershipQuote(_unitOfWork, studentId, bundleId);
        return quote.PriceAfterOwnership;
    }

    private async Task<decimal> ComputeProgramBaseAmount(Guid programId)
    {
        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (program == null || program.IsDeleted)
            throw ErrorHelper.NotFound($"Program '{programId}' not found.");

        if (program.Status != ProgramStatus.Active)
            throw ErrorHelper.BadRequest("Program is not available for purchase.");

        return ClampNonNegative(program.Price ?? 0);
    }

    private async Task ActivateDueVouchers()
    {
        var now = _currentTime.GetCurrentTime();
        var due = _unitOfWork.Vouchers.GetQueryable()
            .Where(v => !v.IsDeleted
                        && v.Status == VoucherStatus.Draft
                        && (v.StartsAt == null || v.StartsAt <= now))
            .ToList();

        if (due.Count == 0)
            return;

        foreach (var voucher in due)
            voucher.Status = VoucherStatus.Active;

        await _unitOfWork.Vouchers.UpdateRange(due);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task PersistActivationIfDue(Voucher voucher, DateTime now)
    {
        if (!VoucherValidator.TryActivateIfDue(voucher, now))
            return;

        await _unitOfWork.Vouchers.Update(voucher);
        await _unitOfWork.SaveChangesAsync();
    }

    private static decimal ClampNonNegative(decimal value) => value < 0 ? 0 : value;

    private static VoucherPreviewDto Fail(
        string errorCode,
        string message,
        decimal baseAmount,
        bool throwOnInvalid)
    {
        if (throwOnInvalid)
            throw ErrorHelper.BadRequest(message);

        return new VoucherPreviewDto
        {
            IsValid = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
            DiscountAmount = 0,
            BaseAmount = baseAmount,
            FinalAmount = baseAmount,
        };
    }

    private static decimal ComputeVoucherDiscount(Voucher voucher, decimal baseAmount)
    {
        decimal discount;
        if (voucher.PercentOff.HasValue)
        {
            discount = Math.Round(
                baseAmount * voucher.PercentOff.Value / 100m,
                2,
                MidpointRounding.AwayFromZero);
        }
        else
        {
            discount = voucher.AmountOff ?? 0;
        }

        if (discount > baseAmount)
            discount = baseAmount;
        if (discount < 0)
            discount = 0;

        return discount;
    }

    private int CountSuccessfulUsages(Guid voucherId, Guid? studentId = null)
    {
        var query = _unitOfWork.Payments.GetQueryable()
            .Where(p => !p.IsDeleted
                        && p.VoucherId == voucherId
                        && p.Status == PaymentStatus.Success);

        if (studentId.HasValue)
            query = query.Where(p => p.StudentId == studentId.Value);

        return query.Count();
    }

    private Dictionary<Guid, int> GetUsageCounts(List<Guid> voucherIds)
    {
        if (voucherIds.Count == 0)
            return [];

        return _unitOfWork.Payments.GetQueryable()
            .Where(p => !p.IsDeleted
                        && p.VoucherId.HasValue
                        && voucherIds.Contains(p.VoucherId.Value)
                        && p.Status == PaymentStatus.Success)
            .GroupBy(p => p.VoucherId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<List<VoucherUsageItemDto>> GetUsageItems(Guid voucherId)
    {
        var payments = await _unitOfWork.Payments.GetAllAsync(
            p => !p.IsDeleted
                 && p.VoucherId == voucherId
                 && p.Status == PaymentStatus.Success);

        var studentIds = payments.Select(p => p.StudentId).Distinct().ToList();
        var students = (await _unitOfWork.Users.GetAllAsync(u => studentIds.Contains(u.Id)))
            .ToDictionary(u => u.Id);

        return payments
            .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
            .Select(p => new VoucherUsageItemDto
            {
                PaymentId = p.Id,
                PaymentCode = p.Code,
                StudentId = p.StudentId,
                StudentName = students.GetValueOrDefault(p.StudentId)?.FullName,
                Amount = p.Amount,
                DiscountAmount = p.DiscountAmount,
                Currency = p.Currency,
                PaidAt = p.PaidAt ?? p.CreatedAt,
            })
            .ToList();
    }

    private static VoucherResponseDto MapToResponse(
        Voucher voucher,
        int usageCount,
        DateTime now,
        bool includeUsages,
        List<VoucherUsageItemDto>? usages = null)
    {
        int? remaining = voucher.UsageLimit.HasValue
            ? Math.Max(0, voucher.UsageLimit.Value - usageCount)
            : null;

        return new VoucherResponseDto
        {
            Id = voucher.Id,
            Code = voucher.Code,
            PercentOff = voucher.PercentOff,
            AmountOff = voucher.AmountOff,
            StartsAt = voucher.StartsAt,
            ExpiryAt = voucher.ExpiryAt,
            UsageLimit = voucher.UsageLimit,
            MaxUsagePerStudent = voucher.MaxUsagePerStudent,
            Scope = voucher.Scope,
            Status = voucher.Status,
            UsageCount = usageCount,
            RemainingUses = remaining,
            IsExpired = voucher.ExpiryAt.HasValue && voucher.ExpiryAt.Value <= now,
            IsNotYetActive = voucher.StartsAt.HasValue && voucher.StartsAt.Value > now,
            Usages = includeUsages ? usages ?? [] : [],
            CreatedAt = voucher.CreatedAt,
            UpdatedAt = voucher.UpdatedAt,
        };
    }
}
