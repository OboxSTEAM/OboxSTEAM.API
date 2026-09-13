using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramBundleDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IProgramBundleService
{
    /// <summary>
    /// Ownership-first price quote for <paramref name="studentId"/>.
    /// Optional <paramref name="voucherCode"/> is applied after ownership; invalid codes do not throw.
    /// </summary>
    Task<BundlePriceQuoteDto> GetPriceQuote(Guid bundleId, Guid studentId, string? voucherCode);

    /// <summary>
    /// Paginated catalog. Search matches code or name. Optional status and category filters.
    /// Item programs are omitted; use <see cref="GetBundleById"/> for items and retail total.
    /// Student, Parent, and anonymous callers only receive Active bundles;
    /// Admin and Manager may filter any status.
    /// </summary>
    Task<Pagination<ProgramBundleResponseDto>> GetAllBundles(
        string? search,
        ProgramBundleStatus? status,
        ProgramCategory? category,
        int page,
        int pageSize);

    /// <summary>Detail including ordered items and retail total.</summary>
    Task<ProgramBundleResponseDto> GetBundleById(Guid bundleId);

    /// <summary>
    /// Purchased pathways for the caller scope (Student own, Parent linked, Admin/Manager all).
    /// Active and Completed only; unpaid PendingPayment rows are omitted.
    /// Each row includes ordered nodes and the pathway certificate when issued.
    /// </summary>
    Task<Pagination<MyBundlePathwayDto>> GetMyPathways(int page, int pageSize);

    /// <summary>One purchased pathway by <see cref="BundleEnrollment"/> id, same visibility as the list.</summary>
    Task<MyBundlePathwayDto> GetMyPathwayByEnrollmentId(Guid bundleEnrollmentId);

    /// <summary>Creates a Draft bundle. Catalog purchase requires <see cref="PublishBundle"/>.</summary>
    Task<ProgramBundleResponseDto> CreateBundle(CreateProgramBundleRequestDto request);

    /// <summary>Updates catalog fields and <c>PricePercent</c>; recalculates persisted <c>Price</c>.</summary>
    Task<ProgramBundleResponseDto> UpdateBundle(Guid bundleId, UpdateProgramBundleRequestDto request);

    /// <summary>Adds a program to a Draft or Inactive bundle and recalculates <c>Price</c>.</summary>
    Task<ProgramBundleResponseDto> AddBundleItem(Guid bundleId, CreateProgramBundleItemRequestDto request);

    /// <summary>Updates sort, gate, or program on a Draft or Inactive bundle item.</summary>
    Task<ProgramBundleResponseDto> UpdateBundleItem(
        Guid bundleId,
        Guid itemId,
        UpdateProgramBundleItemRequestDto request);

    /// <summary>Soft-deletes an item and recalculates <c>Price</c>.</summary>
    Task<ProgramBundleResponseDto> DeleteBundleItem(Guid bundleId, Guid itemId);

    /// <summary>
    /// Manager publication: Draft → Active after item count, Active programs,
    /// and bundle price &lt; retail sum checks.
    /// </summary>
    Task<ProgramBundleResponseDto> PublishBundle(Guid bundleId);
}
