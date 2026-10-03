using System.Net;
using System.Text.RegularExpressions;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class AdvisoryReferenceResolver : IAdvisoryReferenceResolver
{
    private static readonly Regex MarkupPattern = new("<[^>]+>", RegexOptions.Compiled);

    private readonly IUnitOfWork _unitOfWork;

    public AdvisoryReferenceResolver(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyDictionary<Guid, AdvisoryReferenceDto>> ResolveLoadedAsync(
        Guid programId,
        IReadOnlyCollection<ProgramAdvisoryReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        var result = new Dictionary<Guid, AdvisoryReferenceDto>();
        ProgramCurriculumTreeSnapshot? tree = null;
        foreach (var reference in references.Where(r => r.ProgramId == programId && !r.IsDeleted))
        {
            tree ??= await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, programId);
            result[reference.Id] = Resolve(reference, tree);
        }

        return result;
    }

    private static AdvisoryReferenceDto Resolve(ProgramAdvisoryReference reference, ProgramCurriculumTreeSnapshot tree)
    {
        var dto = Map(reference);
        try
        {
            var (_, fields) = ResolveLiveTarget(tree, reference.TargetType, reference.TargetId);
            var currentValue = reference.AnchorKind == ProgramAdvisoryAnchorKind.Node
                ? reference.CapturedLabel
                : GetFieldValue(fields, reference.FieldKey!);

            dto.IsAvailable = true;
            var normalizedCurrent = NormalizePlainText(currentValue);
            var normalizedQuote = NormalizePlainText(reference.Quote);
            dto.QuoteMatched = reference.AnchorKind != ProgramAdvisoryAnchorKind.Quote
                || normalizedCurrent.Contains(normalizedQuote, StringComparison.OrdinalIgnoreCase);
            if (!dto.QuoteMatched)
            {
                dto.UnavailableReason = "The quote no longer matches exactly; focus the saved field instead.";
            }
        }
        catch (Exception ex) when (ex is BadRequestException or NotFoundException)
        {
            dto.IsAvailable = false;
            dto.UnavailableReason = "The referenced curriculum target is no longer available.";
        }

        return dto;
    }

    private static (string Label, IReadOnlyDictionary<string, string?> Fields) ResolveLiveTarget(
        ProgramCurriculumTreeSnapshot tree,
        ProgramAdvisoryTargetType targetType,
        Guid targetId)
    {
        if (targetType == ProgramAdvisoryTargetType.Program)
        {
            return (tree.Program.Name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = tree.Program.Name,
                ["code"] = tree.Program.Code,
                ["description"] = tree.Program.Description,
            });
        }

        return targetType switch
        {
            ProgramAdvisoryTargetType.Module => tree.Modules.FirstOrDefault(m => m.Id == targetId) is { } module
                ? (module.Name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = module.Name, ["code"] = module.Code,
                    ["type"] = module.ModuleType.ToString(),
                    ["learningOutcomes"] = string.Join("|", module.LearningOutcomes ?? []),
                })
                : throw ErrorHelper.BadRequest("Target module does not belong to this program."),
            ProgramAdvisoryTargetType.Course => tree.CoursesByModuleId.Values.SelectMany(x => x)
                .FirstOrDefault(c => c.Id == targetId) is { } course
                ? (course.Name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = course.Name, ["code"] = course.Code, ["description"] = course.Description,
                })
                : throw ErrorHelper.BadRequest("Target course does not belong to this program."),
            ProgramAdvisoryTargetType.Activity => tree.ActivitiesById.TryGetValue(targetId, out var activity)
                ? (activity.Name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = activity.Name, ["type"] = activity.ActivityType.ToString(),
                    ["description"] = activity.Description, ["durationMinutes"] = activity.DurationMinutes?.ToString(),
                    ["requireQrCheckin"] = activity.RequireQrCheckin.ToString(),
                    ["requireMediaEvidence"] = activity.RequireMediaEvidence.ToString(),
                })
                : throw ErrorHelper.BadRequest("Target activity does not belong to this program."),
            ProgramAdvisoryTargetType.Assignment => tree.AssignmentsById.TryGetValue(targetId, out var assignment)
                ? (assignment.Title, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = assignment.Title, ["code"] = assignment.Code, ["description"] = assignment.Description,
                    ["assignmentType"] = assignment.AssignmentType.ToString(), ["maxPoints"] = assignment.MaxPoints.ToString(),
                    ["passScore"] = assignment.PassScore.ToString(),
                    ["isRequiredForModulePass"] = assignment.IsRequiredForModulePass.ToString(),
                })
                : throw ErrorHelper.BadRequest("Target assignment does not belong to this program."),
            ProgramAdvisoryTargetType.ResearchMilestone => tree.MilestonesByModuleId.Values.SelectMany(x => x)
                .FirstOrDefault(m => m.Id == targetId) is { } milestone
                ? (milestone.Title, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = milestone.Title, ["code"] = milestone.Code, ["description"] = milestone.Description,
                    ["isCapstone"] = milestone.IsCapstone.ToString(),
                })
                : throw ErrorHelper.BadRequest("Target research milestone does not belong to this program."),
            ProgramAdvisoryTargetType.Material => tree.MaterialsByActivityId.Values
                .FirstOrDefault(m => m.Id == targetId && !m.IsDeleted) is { } material
                ? (material.Title, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = material.Title, ["materialType"] = material.MaterialType.ToString(),
                    ["fileName"] = Path.GetFileName(material.FileUrl ?? string.Empty),
                })
                : throw ErrorHelper.BadRequest("Target material does not belong to this program."),
            _ => throw ErrorHelper.BadRequest("Unsupported advisory reference target type."),
        };
    }

    private static string GetFieldValue(IReadOnlyDictionary<string, string?> fields, string fieldKey)
    {
        if (!fields.ContainsKey(fieldKey))
        {
            throw ErrorHelper.BadRequest($"Field '{fieldKey}' is not supported for this target.");
        }

        return fields[fieldKey] ?? string.Empty;
    }

    private static AdvisoryReferenceDto Map(ProgramAdvisoryReference reference)
        => new()
        {
            Id = reference.Id,
            ProgramId = reference.ProgramId,
            TargetType = reference.TargetType,
            TargetId = reference.TargetId,
            AnchorKind = reference.AnchorKind,
            FieldKey = reference.FieldKey,
            Quote = reference.Quote,
            QuotePrefix = reference.QuotePrefix,
            QuoteSuffix = reference.QuoteSuffix,
            CapturedLabel = reference.CapturedLabel,
            CapturedExcerpt = reference.CapturedExcerpt,
            CapturedAt = reference.CapturedAt,
            IsAvailable = false,
        };

    private static string NormalizePlainText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(value);
        return string.Join(' ', MarkupPattern.Replace(decoded, " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
