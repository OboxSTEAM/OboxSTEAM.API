using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Shared bundle list-price after ownership deduction.
/// Effective price = clamp(bundle.Price − Σ retail of owned Active/Completed programs, 0).
/// Voucher is applied by callers after this result.
/// </summary>
public static class BundlePricingHelper
{
    public static async Task<BundleOwnershipQuote> ComputeOwnershipQuote(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid bundleId)
    {
        var bundle = await unitOfWork.ProgramBundles.GetByIdAsync(bundleId);
        if (bundle == null || bundle.IsDeleted)
            throw ErrorHelper.NotFound($"Bundle '{bundleId}' not found.");

        if (bundle.Status != ProgramBundleStatus.Active)
            throw ErrorHelper.BadRequest("Bundle is not available for purchase.");

        var items = (await unitOfWork.ProgramBundleItems.GetAllAsync(
                i => i.BundleId == bundleId && !i.IsDeleted))
            .OrderBy(i => i.SortOrder)
            .ToList();

        var programIds = items.Select(i => i.ProgramId).Distinct().ToList();
        var ownedLines = new List<OwnedProgramLine>();
        decimal ownedRetail = 0;

        if (programIds.Count > 0)
        {
            var ownedEnrollments = await unitOfWork.ProgramEnrollments.GetAllAsync(
                pe => pe.StudentId == studentId
                      && !pe.IsDeleted
                      && programIds.Contains(pe.ProgramId)
                      && (pe.Status == EnrollmentStatus.Active
                          || pe.Status == EnrollmentStatus.Completed));

            var ownedProgramIds = ownedEnrollments
                .Select(pe => pe.ProgramId)
                .Distinct()
                .ToHashSet();

            var sortByProgramId = items
                .GroupBy(i => i.ProgramId)
                .ToDictionary(g => g.Key, g => g.Min(i => i.SortOrder));

            foreach (var programId in ownedProgramIds.OrderBy(id => sortByProgramId.GetValueOrDefault(id)))
            {
                var program = await unitOfWork.Programs.GetByIdAsync(programId);
                if (program == null || program.IsDeleted)
                    continue;

                var deducted = program.Price ?? 0;
                ownedRetail += deducted;
                ownedLines.Add(new OwnedProgramLine(program.Id, program.Name, deducted));
            }
        }

        return new BundleOwnershipQuote(
            bundle.Id,
            bundle.Name,
            bundle.Price,
            ownedLines,
            ownedRetail,
            ClampNonNegative(bundle.Price - ownedRetail));
    }

    public static decimal ClampNonNegative(decimal value) => value < 0 ? 0 : value;
}

public sealed record OwnedProgramLine(Guid ProgramId, string Name, decimal DeductedPrice);

public sealed record BundleOwnershipQuote(
    Guid BundleId,
    string BundleName,
    decimal BundlePrice,
    IReadOnlyList<OwnedProgramLine> OwnedPrograms,
    decimal OwnershipDeduction,
    decimal PriceAfterOwnership);
