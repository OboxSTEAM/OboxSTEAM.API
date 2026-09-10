using OboxSteam.Application.DTOs.VoucherDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Voucher issue, update, and apply-time business rules.
/// </summary>
public static class VoucherValidator
{
    public const int MaxCodeLength = 50;

    public static class ApplyError
    {
        public const string NotFound = "NotFound";
        public const string NotYetActive = "NotYetActive";
        public const string Expired = "Expired";
        public const string UsageLimitReached = "UsageLimitReached";
        public const string MaxUsagePerStudent = "MaxUsagePerStudent";
        public const string ScopeMismatch = "ScopeMismatch";
    }

    public readonly record struct ApplyRejection(string ErrorCode, string Message);

    public static string ValidateForCreate(CreateVoucherRequestDto request, DateTime now)
    {
        ValidateDiscountFields(request.PercentOff, request.AmountOff);
        ValidateScope(request.Scope);
        var code = NormalizeCode(request.Code);
        ValidateValidityWindow(request.StartsAt, request.ExpiryAt);
        ValidateExpiryIsInFuture(request.ExpiryAt, now);
        ValidatePositiveCap(request.UsageLimit, nameof(request.UsageLimit));
        ValidatePositiveCap(request.MaxUsagePerStudent, nameof(request.MaxUsagePerStudent));
        return code;
    }

    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw ErrorHelper.BadRequest("Code is required.");

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > MaxCodeLength)
            throw ErrorHelper.BadRequest($"Code cannot exceed {MaxCodeLength} characters.");

        return normalized;
    }

    public static void ValidateDiscountFields(decimal? percentOff, decimal? amountOff)
    {
        var hasPercent = percentOff.HasValue;
        var hasAmount = amountOff.HasValue;
        if (hasPercent == hasAmount)
        {
            throw ErrorHelper.BadRequest("Exactly one of PercentOff or AmountOff is required.");
        }

        if (hasPercent && (percentOff <= 0 || percentOff > 100))
            throw ErrorHelper.BadRequest("PercentOff must be greater than 0 and at most 100.");

        if (hasAmount && amountOff <= 0)
            throw ErrorHelper.BadRequest("AmountOff must be greater than 0.");
    }

    public static void ValidateScope(VoucherScope scope)
    {
        if (!Enum.IsDefined(scope))
            throw ErrorHelper.BadRequest("Scope must be Bundle, Program, or Both.");
    }

    public static void ValidatePositiveCap(int? value, string fieldName)
    {
        if (value.HasValue && value.Value < 1)
            throw ErrorHelper.BadRequest($"{fieldName} must be at least 1.");
    }

    public static void ValidateExpiryIsInFuture(DateTime? expiryAt, DateTime now)
    {
        if (!expiryAt.HasValue)
            return;

        if (expiryAt.Value <= now)
            throw ErrorHelper.BadRequest("ExpiryAt must be in the future.");
    }

    public static void ValidateValidityWindow(DateTime? startsAt, DateTime? expiryAt)
    {
        if (startsAt.HasValue && expiryAt.HasValue && startsAt.Value >= expiryAt.Value)
            throw ErrorHelper.BadRequest("StartsAt must be before ExpiryAt.");
    }

    public static VoucherStatus ResolveStatus(DateTime? startsAt, DateTime now)
    {
        if (startsAt.HasValue && startsAt.Value > now)
            return VoucherStatus.Draft;

        return VoucherStatus.Active;
    }

    /// <summary>
    /// Promotes Draft to Active when <paramref name="now"/> has reached <see cref="Voucher.StartsAt"/>.
    /// </summary>
    public static bool TryActivateIfDue(Voucher voucher, DateTime now)
    {
        if (voucher.Status != VoucherStatus.Draft)
            return false;

        if (ResolveStatus(voucher.StartsAt, now) != VoucherStatus.Active)
            return false;

        voucher.Status = VoucherStatus.Active;
        return true;
    }

    public static async Task EnsureCodeIsUnique(IUnitOfWork unitOfWork, string code)
    {
        var exists = await unitOfWork.Vouchers.AnyIncludingDeletedAsync(v => v.Code == code);
        if (exists)
            throw ErrorHelper.Conflict($"Voucher code '{code}' already exists.");
    }

    public static Voucher RequireExisting(Voucher? voucher, Guid voucherId)
    {
        if (voucher == null || voucher.IsDeleted)
            throw ErrorHelper.NotFound($"Voucher '{voucherId}' not found.");

        return voucher;
    }

    public static void ValidateUsageLimitNotBelowCurrent(int usageLimit, int currentUsage)
    {
        if (usageLimit < currentUsage)
        {
            throw ErrorHelper.Conflict(
                $"UsageLimit cannot be below the current usage count ({currentUsage}).");
        }
    }

    public static void ValidateHasUpdate(bool hasChange)
    {
        if (!hasChange)
            throw ErrorHelper.BadRequest("No fields to update.");
    }

    public static void ValidatePreviewTarget(Guid? bundleId, Guid? programId)
    {
        var hasBundle = HasId(bundleId);
        var hasProgram = HasId(programId);
        if (hasBundle == hasProgram)
            throw ErrorHelper.BadRequest("Provide exactly one of BundleId or ProgramId.");
    }

    public static bool ScopeAllows(VoucherScope scope, Guid? bundleId, Guid? programId)
    {
        var hasBundle = HasId(bundleId);
        return scope switch
        {
            VoucherScope.Both => true,
            VoucherScope.Bundle => hasBundle,
            VoucherScope.Program => !hasBundle,
            _ => false,
        };
    }

    public static ApplyRejection? ValidateApply(
        Voucher? voucher,
        int usageCount,
        int studentUsageCount,
        Guid? bundleId,
        Guid? programId,
        DateTime now)
    {
        if (voucher == null)
            return new ApplyRejection(ApplyError.NotFound, "Voucher code was not found.");

        if (voucher.Status == VoucherStatus.Draft
            || (voucher.StartsAt.HasValue && voucher.StartsAt.Value > now))
            return new ApplyRejection(ApplyError.NotYetActive, "Voucher code is not yet active.");

        if (voucher.ExpiryAt.HasValue && voucher.ExpiryAt.Value <= now)
            return new ApplyRejection(ApplyError.Expired, "Voucher code has expired.");

        if (voucher.UsageLimit.HasValue && usageCount >= voucher.UsageLimit.Value)
        {
            return new ApplyRejection(
                ApplyError.UsageLimitReached,
                "Voucher has reached its usage limit.");
        }

        if (voucher.MaxUsagePerStudent.HasValue && studentUsageCount >= voucher.MaxUsagePerStudent.Value)
        {
            return new ApplyRejection(
                ApplyError.MaxUsagePerStudent,
                "You have already used this voucher the maximum number of times.");
        }

        if (!ScopeAllows(voucher.Scope, bundleId, programId))
        {
            return new ApplyRejection(
                ApplyError.ScopeMismatch,
                "Voucher cannot be applied to this product.");
        }

        return null;
    }

    public static async Task EnsureCallerCanActForStudent(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Guid studentId)
    {
        var student = await unitOfWork.Users.GetByIdAsync(studentId);
        if (student == null || student.IsDeleted || student.Role != RoleType.Student)
            throw ErrorHelper.NotFound($"Student '{studentId}' not found.");

        var actorId = claimsService.GetCurrentUserId;
        var actor = await unitOfWork.Users.GetByIdAsync(actorId)
            ?? throw ErrorHelper.Unauthorized("User not found.");

        if (actor.Role is RoleType.Admin or RoleType.Manager)
            return;

        if (actor.Role == RoleType.Student)
        {
            if (actor.Id != studentId)
                throw ErrorHelper.Forbidden("You can only preview vouchers for yourself.");
            return;
        }

        if (actor.Role == RoleType.Parent)
        {
            var link = await unitOfWork.ParentStudents.FirstOrDefaultAsync(
                ps => ps.ParentId == actor.Id
                      && ps.StudentId == studentId
                      && ps.IsVerified
                      && !ps.IsDeleted);
            if (link == null)
                throw ErrorHelper.Forbidden("You can only preview vouchers for a linked student.");
            return;
        }

        throw ErrorHelper.Forbidden("You do not have permission to preview this voucher.");
    }

    private static bool HasId(Guid? id) => id.HasValue && id.Value != Guid.Empty;
}
