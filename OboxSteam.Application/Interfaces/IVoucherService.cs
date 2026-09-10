using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.VoucherDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IVoucherService
{
    /// <summary>
    /// Paginated manager list. Search matches code. Optional scope and status filters.
    /// Due Draft codes are activated before paging. <see cref="VoucherResponseDto.Usages"/> is empty.
    /// </summary>
    Task<Pagination<VoucherResponseDto>> GetAllVouchers(
        string? search,
        VoucherScope? scope,
        VoucherStatus? status,
        int page,
        int pageSize);

    /// <summary>
    /// Manager detail including successful-payment usage history.
    /// </summary>
    Task<VoucherResponseDto> GetVoucherById(Guid voucherId);

    /// <summary>
    /// Issue a new code. Exactly one of percent-off or amount-off. Code is unique.
    /// </summary>
    Task<VoucherResponseDto> CreateVoucher(CreateVoucherRequestDto request);

    /// <summary>
    /// Update expiry, usage caps, and scope. Does not change code or discount type/amount.
    /// </summary>
    Task<VoucherResponseDto> UpdateVoucher(Guid voucherId, UpdateVoucherRequestDto request);

    /// <summary>
    /// Disable a code via soft-delete. Existing successful payments keep their voucher link.
    /// </summary>
    Task<bool> DeleteVoucher(Guid voucherId);

    /// <summary>
    /// Payment-dialog preview for <paramref name="studentId"/>. Always returns a DTO;
    /// invalid codes set <see cref="VoucherPreviewDto.IsValid"/> to false instead of throwing.
    /// Does not persist usage. <paramref name="studentId"/> is the learner, not necessarily the JWT user
    /// (parent-pay uses the child).
    /// </summary>
    Task<VoucherPreviewDto> PreviewVoucher(Guid studentId, PreviewVoucherRequestDto request);

    /// <summary>
    /// Same computation as <see cref="PreviewVoucher"/> but throws if the code cannot be applied.
    /// Used by checkout. Does not persist usage; the payment row records <c>VoucherId</c>.
    /// </summary>
    Task<VoucherPreviewDto> ValidateForCheckout(Guid studentId, PreviewVoucherRequestDto request);
}
