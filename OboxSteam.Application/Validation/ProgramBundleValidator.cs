using OboxSteam.Application.DTOs.ProgramBundleDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>Create and publish rules for program bundles.</summary>
public static class ProgramBundleValidator
{
    public const int MaxCodeLength = 50;
    public const int MinItemsToPublish = 2;
    public const decimal MinPricePercent = 0.01m;
    public const decimal MaxPricePercent = 99.99m;

    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw ErrorHelper.BadRequest("Code is required.");

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > MaxCodeLength)
            throw ErrorHelper.BadRequest($"Code cannot exceed {MaxCodeLength} characters.");

        return normalized;
    }

    public static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw ErrorHelper.BadRequest("Name is required.");

        var trimmed = name.Trim();
        if (trimmed.Length > 255)
            throw ErrorHelper.BadRequest("Name cannot exceed 255 characters.");

        return trimmed;
    }

    public static decimal ValidatePricePercent(decimal pricePercent)
    {
        if (pricePercent < MinPricePercent || pricePercent > MaxPricePercent)
        {
            throw ErrorHelper.BadRequest(
                "PricePercent must be greater than 0 and less than 100.");
        }

        return pricePercent;
    }

    public static void ValidateCategory(ProgramCategory category)
    {
        if (!Enum.IsDefined(category))
            throw ErrorHelper.BadRequest("Category is invalid.");
    }

    public static async Task EnsureCodeIsUnique(IUnitOfWork unitOfWork, string code)
    {
        var exists = await unitOfWork.ProgramBundles.AnyIncludingDeletedAsync(b => b.Code == code);
        if (exists)
            throw ErrorHelper.Conflict($"Bundle code '{code}' already exists.");
    }

    public static ProgramBundle RequireExisting(ProgramBundle? bundle, Guid bundleId)
    {
        if (bundle == null || bundle.IsDeleted)
            throw ErrorHelper.NotFound($"Bundle '{bundleId}' not found.");

        return bundle;
    }

    public static void EnsureItemsMutable(ProgramBundle bundle)
    {
        if (bundle.Status == ProgramBundleStatus.Active)
        {
            throw ErrorHelper.Conflict(
                "Cannot add or change programs on a published bundle.");
        }
    }

    public static ProgramBundleItem RequireItem(
        ProgramBundleItem? item,
        Guid bundleId,
        Guid itemId)
    {
        if (item == null || item.IsDeleted || item.BundleId != bundleId)
            throw ErrorHelper.NotFound($"Bundle item '{itemId}' not found.");

        return item;
    }

    public static void EnsureProgramNotInBundle(
        IReadOnlyList<ProgramBundleItem> items,
        Guid programId,
        Guid? excludeItemId = null)
    {
        if (items.Any(i => i.ProgramId == programId && i.Id != excludeItemId))
            throw ErrorHelper.Conflict("Bundle already contains this program.");
    }

    public static void EnsureSortOrderAvailable(
        IReadOnlyList<ProgramBundleItem> items,
        int sortOrder,
        Guid? excludeItemId = null)
    {
        if (items.Any(i => i.SortOrder == sortOrder && i.Id != excludeItemId))
            throw ErrorHelper.BadRequest("SortOrder is already used by another item.");
    }

    public static void ValidateItemUpdate(UpdateProgramBundleItemRequestDto request)
    {
        if (!request.ProgramId.HasValue
            && !request.SortOrder.HasValue
            && !request.RequiresPreviousCompletion.HasValue)
        {
            throw ErrorHelper.BadRequest("No fields to update.");
        }

        if (request.ProgramId.HasValue && request.ProgramId.Value == Guid.Empty)
            throw ErrorHelper.BadRequest("ProgramId is required.");
    }

    public static async Task EnsureFrameworkExists(IUnitOfWork unitOfWork, Guid frameworkId)
    {
        var framework = await unitOfWork.ProgramFrameworks.GetByIdAsync(frameworkId);
        if (framework == null || framework.IsDeleted)
            throw ErrorHelper.NotFound($"Framework '{frameworkId}' not found.");
    }

    public static void ValidateItemPayload(IReadOnlyList<CreateProgramBundleItemRequestDto> items)
    {
        if (items.Count == 0)
            return;

        var programIds = items.Select(i => i.ProgramId).ToList();
        if (programIds.Any(id => id == Guid.Empty))
            throw ErrorHelper.BadRequest("Each item must have a ProgramId.");

        if (programIds.Distinct().Count() != programIds.Count)
            throw ErrorHelper.BadRequest("Bundle items cannot repeat the same program.");

        var specifiedOrders = items.Where(i => i.SortOrder.HasValue).Select(i => i.SortOrder!.Value).ToList();
        if (specifiedOrders.Count > 0 && specifiedOrders.Count != items.Count)
            throw ErrorHelper.BadRequest("Provide SortOrder on every item, or omit it on every item.");

        if (specifiedOrders.Count > 0 && specifiedOrders.Distinct().Count() != specifiedOrders.Count)
            throw ErrorHelper.BadRequest("SortOrder values must be unique.");
    }

    public static async Task<IReadOnlyList<Program>> LoadProgramsForItems(
        IUnitOfWork unitOfWork,
        IReadOnlyList<CreateProgramBundleItemRequestDto> items)
    {
        var programs = new List<Program>(items.Count);
        foreach (var item in items)
        {
            var program = await unitOfWork.Programs.GetByIdAsync(item.ProgramId);
            if (program == null || program.IsDeleted)
                throw ErrorHelper.NotFound($"Program '{item.ProgramId}' not found.");

            programs.Add(program);
        }

        return programs;
    }

    public static void ValidateForPublish(
        ProgramBundle bundle,
        IReadOnlyList<ProgramBundleItem> items,
        IReadOnlyDictionary<Guid, Program> programsById)
    {
        if (bundle.Status == ProgramBundleStatus.Active)
            throw ErrorHelper.Conflict("Bundle is already published.");

        if (bundle.Status != ProgramBundleStatus.Draft)
            throw ErrorHelper.BadRequest("Only a Draft bundle can be published.");

        if (items.Count < MinItemsToPublish)
        {
            throw ErrorHelper.BadRequest(
                $"A bundle needs at least {MinItemsToPublish} programs before it can be published.");
        }

        var ordered = items.OrderBy(i => i.SortOrder).ToList();
        if (ordered[0].RequiresPreviousCompletion)
            throw ErrorHelper.BadRequest("The first program cannot require previous completion.");

        decimal retailTotal = 0;
        foreach (var item in ordered)
        {
            if (!programsById.TryGetValue(item.ProgramId, out var program) || program.IsDeleted)
                throw ErrorHelper.NotFound($"Program '{item.ProgramId}' not found.");

            if (program.Status != ProgramStatus.Active)
            {
                throw ErrorHelper.BadRequest(
                    $"Program '{program.Name}' must be Active before the bundle can be published.");
            }

            retailTotal += program.Price ?? 0;
        }

        if (bundle.Price >= retailTotal)
        {
            throw ErrorHelper.BadRequest(
                "Bundle price must be lower than the sum of item retail prices.");
        }
    }
}
