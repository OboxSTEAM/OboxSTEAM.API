using System.Net;
using System.Text.Json;
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
    private const int MaxCapturedExcerptLength = 4000;
    private static readonly Regex MarkupPattern = new("<[^>]+>", RegexOptions.Compiled);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTime _currentTime;

    public AdvisoryReferenceResolver(IUnitOfWork unitOfWork, ICurrentTime currentTime)
    {
        _unitOfWork = unitOfWork;
        _currentTime = currentTime;
    }

    public async Task<ProgramAdvisoryReference> CaptureAsync(
        Program program,
        User actor,
        CreateAdvisoryReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);
        var targetId = request.TargetId ?? (request.TargetType == ProgramAdvisoryTargetType.Program
            ? program.Id
            : Guid.Empty);
        if (targetId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("TargetId is required for this reference target.");
        }

        string label;
        IReadOnlyDictionary<string, string?> fields;
        if (request.Context == AdvisoryReferenceContext.Submission)
        {
            if (!request.SubmissionId.HasValue)
            {
                throw ErrorHelper.BadRequest("SubmissionId is required for a submission reference.");
            }

            var submission = await _unitOfWork.ProgramReviewSubmissions.GetByIdAsync(request.SubmissionId.Value);
            if (submission == null || submission.IsDeleted || submission.ProgramId != program.Id)
            {
                throw ErrorHelper.BadRequest("Submission does not belong to this program.");
            }

            if (request.TargetType == ProgramAdvisoryTargetType.RubricCriterion)
            {
                (label, fields) = ResolveRubricSnapshotTarget(submission.RubricSnapshotJson, targetId);
            }
            else
            {
                var snapshot = CurriculumReviewSnapshotBuilder.TryDeserialize(submission.CurriculumSnapshotJson)
                    ?? throw ErrorHelper.Conflict("The selected submission snapshot is unavailable.");
                (label, fields) = ResolveSnapshotTarget(snapshot, request.TargetType, targetId);
            }
        }
        else
        {
            if (request.SubmissionId.HasValue)
            {
                throw ErrorHelper.BadRequest("Working-draft references cannot include SubmissionId.");
            }

            if (request.TargetType == ProgramAdvisoryTargetType.RubricCriterion)
            {
                (label, fields) = await ResolveLiveRubricTargetAsync(program, targetId);
            }
            else
            {
                var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, program.Id);
                (label, fields) = ResolveLiveTarget(tree, request.TargetType, targetId);
            }
        }

        var excerpt = request.AnchorKind switch
        {
            ProgramAdvisoryAnchorKind.Node => label,
            ProgramAdvisoryAnchorKind.Field => GetFieldValue(fields, request.FieldKey!),
            ProgramAdvisoryAnchorKind.Quote => GetFieldValue(fields, request.FieldKey!),
            _ => throw ErrorHelper.BadRequest("Unsupported reference anchor kind."),
        };

        var normalizedExcerpt = NormalizePlainText(excerpt);
        var normalizedQuote = NormalizePlainText(request.Quote);
        if (request.AnchorKind == ProgramAdvisoryAnchorKind.Quote
            && (string.IsNullOrWhiteSpace(normalizedQuote)
                || string.IsNullOrWhiteSpace(normalizedExcerpt)
                || !normalizedExcerpt.Contains(normalizedQuote, StringComparison.OrdinalIgnoreCase)))
        {
            throw ErrorHelper.BadRequest("Quote does not match the selected field content.");
        }

        var now = _currentTime.GetCurrentTime().ToUniversalTime();
        return new ProgramAdvisoryReference
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Context = request.Context,
            SubmissionId = request.SubmissionId,
            TargetType = request.TargetType,
            TargetId = targetId,
            AnchorKind = request.AnchorKind,
            FieldKey = NormalizeOptional(request.FieldKey),
            Quote = NormalizeOptional(request.Quote),
            QuotePrefix = NormalizeOptional(request.QuotePrefix),
            QuoteSuffix = NormalizeOptional(request.QuoteSuffix),
            CapturedLabel = label,
            CapturedExcerpt = Limit(normalizedExcerpt),
            CapturedAt = now,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };
    }

    public async Task<AdvisoryReferenceDto> ResolveAsync(Guid programId, Guid referenceId)
    {
        var reference = await _unitOfWork.ProgramAdvisoryReferences.GetByIdAsync(referenceId);
        if (reference == null || reference.IsDeleted || reference.ProgramId != programId)
        {
            throw ErrorHelper.NotFound($"Advisory reference '{referenceId}' was not found.");
        }

        var dto = Map(reference);
        try
        {
            string currentValue;
            if (reference.Context == AdvisoryReferenceContext.Submission)
            {
                var submission = reference.SubmissionId.HasValue
                    ? await _unitOfWork.ProgramReviewSubmissions.GetByIdAsync(reference.SubmissionId.Value)
                    : null;
                if (submission == null || submission.IsDeleted || submission.ProgramId != programId)
                {
                    dto.UnavailableReason = "The original submission snapshot is unavailable.";
                    return dto;
                }

                IReadOnlyDictionary<string, string?> fields;
                if (reference.TargetType == ProgramAdvisoryTargetType.RubricCriterion)
                {
                    (_, fields) = ResolveRubricSnapshotTarget(submission.RubricSnapshotJson, reference.TargetId);
                }
                else
                {
                    var snapshot = CurriculumReviewSnapshotBuilder.TryDeserialize(submission.CurriculumSnapshotJson);
                    if (snapshot == null)
                    {
                        dto.UnavailableReason = "The original submission snapshot is unavailable.";
                        return dto;
                    }

                    (_, fields) = ResolveSnapshotTarget(snapshot, reference.TargetType, reference.TargetId);
                }
                currentValue = reference.AnchorKind == ProgramAdvisoryAnchorKind.Node
                    ? reference.CapturedLabel
                    : GetFieldValue(fields, reference.FieldKey!);
            }
            else
            {
                IReadOnlyDictionary<string, string?> fields;
                if (reference.TargetType == ProgramAdvisoryTargetType.RubricCriterion)
                {
                    var program = await _unitOfWork.Programs.GetByIdAsync(programId)
                        ?? throw ErrorHelper.NotFound("Program not found.");
                    (_, fields) = await ResolveLiveRubricTargetAsync(program, reference.TargetId);
                }
                else
                {
                    var tree = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, programId);
                    (_, fields) = ResolveLiveTarget(tree, reference.TargetType, reference.TargetId);
                }
                currentValue = reference.AnchorKind == ProgramAdvisoryAnchorKind.Node
                    ? reference.CapturedLabel
                    : GetFieldValue(fields, reference.FieldKey!);
            }

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

    public async Task<IReadOnlyList<AdvisoryReferenceDto>> ResolveManyAsync(
        Guid programId,
        IReadOnlyList<Guid> referenceIds)
    {
        if (referenceIds.Count > 10)
        {
            throw ErrorHelper.BadRequest("A message may contain at most 10 references.");
        }

        var distinct = referenceIds.Distinct().ToList();
        if (distinct.Count != referenceIds.Count || distinct.Any(id => id == Guid.Empty))
        {
            throw ErrorHelper.BadRequest("Reference ids must be unique and non-empty.");
        }

        var result = new List<AdvisoryReferenceDto>(distinct.Count);
        foreach (var referenceId in distinct)
        {
            var reference = await ResolveAsync(programId, referenceId);
            result.Add(reference);
        }

        return result;
    }

    private static void ValidateRequest(CreateAdvisoryReferenceRequest request)
    {
        if (request.AnchorKind == ProgramAdvisoryAnchorKind.Field
            && string.IsNullOrWhiteSpace(request.FieldKey))
        {
            throw ErrorHelper.BadRequest("FieldKey is required for a Field reference.");
        }

        if (request.AnchorKind == ProgramAdvisoryAnchorKind.Quote
            && (string.IsNullOrWhiteSpace(request.FieldKey) || string.IsNullOrWhiteSpace(request.Quote)))
        {
            throw ErrorHelper.BadRequest("FieldKey and Quote are required for a Quote reference.");
        }

        if (request.FieldKey?.Length > 100
            || request.Quote?.Length > 2000
            || request.QuotePrefix?.Length > 1000
            || request.QuoteSuffix?.Length > 1000)
        {
            throw ErrorHelper.BadRequest("Reference values exceed the allowed length.");
        }
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
                ["skillsGained"] = tree.Program.SkillsGained,
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

    private async Task<(string Label, IReadOnlyDictionary<string, string?> Fields)> ResolveLiveRubricTargetAsync(
        Program program,
        Guid targetId)
    {
        if (!program.FrameworkVersionId.HasValue)
        {
            throw ErrorHelper.BadRequest("Program has no pinned framework version for rubric references.");
        }

        var criterion = await _unitOfWork.FrameworkRubricCriteria.GetByIdAsync(targetId);
        if (criterion == null
            || criterion.IsDeleted
            || criterion.FrameworkVersionId != program.FrameworkVersionId)
        {
            throw ErrorHelper.BadRequest("Target rubric criterion does not belong to this program.");
        }

        return (criterion.Name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = criterion.Name,
            ["description"] = criterion.Description,
            ["evidenceGuidance"] = criterion.EvidenceGuidance,
            ["maxScore"] = criterion.MaxScore.ToString(),
        });
    }

    private static (string Label, IReadOnlyDictionary<string, string?> Fields) ResolveRubricSnapshotTarget(
        string? json,
        Guid targetId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw ErrorHelper.NotFound("The referenced rubric criterion is no longer available.");
        }

        var criteria = JsonSerializer.Deserialize<List<CurriculumReviewSnapshotBuilder.RubricCriterionSnapshot>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var criterion = criteria?.FirstOrDefault(c => c.Id == targetId);
        if (criterion == null)
        {
            throw ErrorHelper.NotFound("The referenced rubric criterion is no longer available.");
        }

        return (criterion.Name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = criterion.Name,
            ["description"] = criterion.Description,
            ["evidenceGuidance"] = criterion.EvidenceGuidance,
            ["maxScore"] = criterion.MaxScore.ToString(),
        });
    }

    private static (string Label, IReadOnlyDictionary<string, string?> Fields) ResolveSnapshotTarget(
        CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument snapshot,
        ProgramAdvisoryTargetType targetType,
        Guid targetId)
    {
        if (targetType == ProgramAdvisoryTargetType.Program)
        {
            var program = snapshot.Program;
            return (program.Name ?? snapshot.ProgramName ?? "Program", new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = program.Name, ["code"] = program.Code,
                ["description"] = program.Description, ["skillsGained"] = program.SkillsGained,
            });
        }

        foreach (var module in snapshot.Modules)
        {
            if (targetType == ProgramAdvisoryTargetType.Module && module.Id == targetId)
            {
                return (module.Name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = module.Name, ["code"] = module.Code, ["type"] = module.ModuleType ?? module.Type,
                    ["learningOutcomes"] = string.Join("|", module.LearningOutcomes ?? []),
                });
            }

            if (targetType == ProgramAdvisoryTargetType.Course)
            {
                var course = module.Courses.FirstOrDefault(c => c.Id == targetId);
                if (course != null)
                {
                    return (course.Name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["name"] = course.Name, ["code"] = course.Code, ["description"] = course.Description,
                    });
                }
            }

            if (targetType == ProgramAdvisoryTargetType.Activity)
            {
                var activity = module.Activities.FirstOrDefault(a => a.Id == targetId)
                    ?? module.Courses.SelectMany(c => c.Activities).FirstOrDefault(a => a.Id == targetId)
                    ?? module.Milestones.SelectMany(m => m.Activities).FirstOrDefault(a => a.Id == targetId);
                if (activity != null)
                {
                    return (activity.Name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["name"] = activity.Name, ["type"] = activity.ActivityType ?? activity.Type,
                        ["description"] = activity.Description, ["durationMinutes"] = activity.DurationMinutes?.ToString(),
                        ["requireQrCheckin"] = activity.RequireQrCheckin.ToString(),
                        ["requireMediaEvidence"] = activity.RequireMediaEvidence.ToString(),
                    });
                }
            }

            if (targetType == ProgramAdvisoryTargetType.Material)
            {
                var material = module.Activities.Select(a => a.Material)
                    .Concat(module.Courses.SelectMany(c => c.Activities).Select(a => a.Material))
                    .Concat(module.Milestones.SelectMany(m => m.Activities).Select(a => a.Material))
                    .FirstOrDefault(m => m?.Id == targetId);
                if (material != null)
                {
                    return (material.Title, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["title"] = material.Title, ["materialType"] = material.MaterialType ?? material.Type,
                        ["fileName"] = material.FileName,
                    });
                }
            }

            if (targetType == ProgramAdvisoryTargetType.Assignment)
            {
                var assignment = module.Assignments.FirstOrDefault(a => a.Id == targetId)
                    ?? module.Milestones.Select(m => m.Assignment).FirstOrDefault(a => a?.Id == targetId);
                if (assignment != null)
                {
                    return (assignment.Title, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["title"] = assignment.Title, ["code"] = assignment.Code, ["description"] = assignment.Description,
                        ["assignmentType"] = assignment.AssignmentType, ["maxPoints"] = assignment.MaxPoints.ToString(),
                        ["passScore"] = assignment.PassScore.ToString(),
                        ["isRequiredForModulePass"] = assignment.IsRequiredForModulePass.ToString(),
                    });
                }
            }

            if (targetType == ProgramAdvisoryTargetType.ResearchMilestone)
            {
                var milestone = module.Milestones.FirstOrDefault(m => m.Id == targetId);
                if (milestone != null)
                {
                    return (milestone.Title, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["title"] = milestone.Title, ["code"] = milestone.Code, ["description"] = milestone.Description,
                        ["isCapstone"] = milestone.IsCapstone.ToString(),
                    });
                }
            }
        }

        throw ErrorHelper.NotFound("The referenced curriculum target is no longer available.");
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
            Context = reference.Context,
            SubmissionId = reference.SubmissionId,
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

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Limit(string value)
        => value.Length <= MaxCapturedExcerptLength ? value : value[..MaxCapturedExcerptLength];
}
