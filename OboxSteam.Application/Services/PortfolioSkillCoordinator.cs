using OboxSteam.Application.DTOs.PortfolioDTO;
using OboxSteam.Application.DTOs.SkillDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Computes achieved catalog skills and stores portfolio curation (visibility, pin, order).
/// Skills are granted from completed program enrollments, issued certificates, and capstone items.
/// A completed program grants every <see cref="ProgramSkill"/> on that program.
/// Module-scoped links are also granted by a matching certificate or capstone before the program is completed.
/// </summary>
public sealed class PortfolioSkillCoordinator
{
    public const int MaxPinnedSkills = 6;

    private readonly IUnitOfWork _unitOfWork;

    public PortfolioSkillCoordinator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PortfolioSkillContext> SyncAndMapAsync(
        Portfolio portfolio,
        IReadOnlyList<PortfolioCustomItem> items,
        bool forPublic)
    {
        var achieved = await ComputeAchievedAsync(portfolio.StudentId, items);
        await EnsureCurationAsync(portfolio.Id, achieved);
        return MapContext(achieved, await LoadCurationAsync(portfolio.Id), forPublic);
    }

    public async Task<List<PortfolioSkillDto>> ReplaceCurationAsync(
        Portfolio portfolio,
        IReadOnlyList<PortfolioCustomItem> items,
        UpdatePortfolioSkillsRequestDto request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Portfolio skill curation is required.");
        }

        if (request.Skills == null)
        {
            throw ErrorHelper.BadRequest("Skills are required.");
        }

        var achieved = await ComputeAchievedAsync(portfolio.StudentId, items);
        await EnsureCurationAsync(portfolio.Id, achieved);

        var skillIds = request.Skills.Select(s => s.SkillId).ToList();
        if (skillIds.Any(id => id == Guid.Empty))
        {
            throw ErrorHelper.BadRequest("Skill id is required.");
        }

        if (skillIds.Count != skillIds.Distinct().Count())
        {
            throw ErrorHelper.BadRequest("Duplicate skill ids are not allowed.");
        }

        if (request.Skills.Any(s => s.DisplayOrder < 0))
        {
            throw ErrorHelper.BadRequest("DisplayOrder cannot be negative.");
        }

        if (request.Skills.Count(s => s.IsPinned) > MaxPinnedSkills)
        {
            throw ErrorHelper.BadRequest($"At most {MaxPinnedSkills} skills can be pinned.");
        }

        var achievedIds = achieved.Select(a => a.Skill.Id).ToHashSet();
        foreach (var skillId in skillIds)
        {
            if (!achievedIds.Contains(skillId))
            {
                throw ErrorHelper.BadRequest(
                    $"Skill '{skillId}' is not in the student's achieved set.");
            }
        }

        var missing = achievedIds.Where(id => !skillIds.Contains(id)).ToList();
        if (missing.Count > 0)
        {
            throw ErrorHelper.BadRequest("Curation must include every achieved skill.");
        }

        var curation = await LoadCurationAsync(portfolio.Id);
        var bySkillId = curation.ToDictionary(c => c.SkillId);
        var requestedBySkillId = request.Skills.ToDictionary(s => s.SkillId);

        foreach (var row in curation)
        {
            if (!requestedBySkillId.TryGetValue(row.SkillId, out var entry))
            {
                continue;
            }

            row.IsVisible = entry.IsVisible;
            row.IsPinned = entry.IsPinned;
            row.DisplayOrder = entry.DisplayOrder;
        }

        if (curation.Count > 0)
        {
            await _unitOfWork.PortfolioSkills.UpdateRange(curation);
            await _unitOfWork.SaveChangesAsync();
        }

        return MapContext(achieved, bySkillId.Values.ToList(), forPublic: false).Skills;
    }

    private async Task<List<AchievedSkill>> ComputeAchievedAsync(
        Guid studentId,
        IReadOnlyList<PortfolioCustomItem> items)
    {
        var enrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            e => e.StudentId == studentId
                 && e.Status == EnrollmentStatus.Completed
                 && !e.IsDeleted);

        var certificates = await _unitOfWork.Certificates.GetAllAsync(
            c => c.StudentId == studentId && !c.IsDeleted);

        var capstones = items
            .Where(i => i.ItemType == PortfolioItemType.CapstoneProject && !i.IsDeleted)
            .ToList();

        var certificateModuleIds = certificates
            .Where(c => c.ModuleId.HasValue && !c.ProgramId.HasValue)
            .Select(c => c.ModuleId!.Value)
            .Distinct()
            .ToList();

        var modules = certificateModuleIds.Count == 0
            ? new Dictionary<Guid, Module>()
            : (await _unitOfWork.Modules.GetAllAsync(
                    m => certificateModuleIds.Contains(m.Id) && !m.IsDeleted))
                .ToDictionary(m => m.Id);

        var moduleEnrollmentIds = capstones
            .Where(i => i.ModuleEnrollmentId.HasValue)
            .Select(i => i.ModuleEnrollmentId!.Value)
            .Distinct()
            .ToList();

        var moduleEnrollments = moduleEnrollmentIds.Count == 0
            ? new Dictionary<Guid, ModuleEnrollment>()
            : (await _unitOfWork.ModuleEnrollments.GetAllAsync(
                    me => moduleEnrollmentIds.Contains(me.Id) && !me.IsDeleted))
                .ToDictionary(me => me.Id);

        var programIds = enrollments.Select(e => e.ProgramId)
            .Concat(certificates.Select(c => c.ProgramId ?? Guid.Empty))
            .Concat(certificates
                .Where(c => !c.ProgramId.HasValue && c.ModuleId.HasValue)
                .Select(c => modules.GetValueOrDefault(c.ModuleId!.Value)?.ProgramId ?? Guid.Empty))
            .Concat(capstones.Select(i => i.ProgramId ?? Guid.Empty))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (programIds.Count == 0)
        {
            return [];
        }

        var links = await _unitOfWork.ProgramSkills.GetAllAsync(
            ps => programIds.Contains(ps.ProgramId) && !ps.IsDeleted);

        if (links.Count == 0)
        {
            return [];
        }

        var skillIds = links.Select(l => l.SkillId).Distinct().ToList();
        var skills = (await _unitOfWork.Skills.GetAllAsync(
                s => skillIds.Contains(s.Id) && !s.IsDeleted))
            .ToDictionary(s => s.Id);

        var programs = (await _unitOfWork.Programs.GetAllAsync(
                p => programIds.Contains(p.Id) && !p.IsDeleted))
            .ToDictionary(p => p.Id);

        var linksByProgram = links
            .Where(l => skills.ContainsKey(l.SkillId))
            .GroupBy(l => l.ProgramId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var evidencesBySkill = new Dictionary<Guid, List<EvidenceDraft>>();

        void AddEvidence(Guid skillId, EvidenceDraft evidence)
        {
            if (!evidencesBySkill.TryGetValue(skillId, out var list))
            {
                list = [];
                evidencesBySkill[skillId] = list;
            }

            var existing = list.FirstOrDefault(e => e.DedupeKey == evidence.DedupeKey);
            if (existing == null)
            {
                list.Add(evidence);
                return;
            }

            if (evidence.AchievedAt < existing.AchievedAt)
            {
                existing.AchievedAt = evidence.AchievedAt;
            }
        }

        foreach (var enrollment in enrollments)
        {
            if (!linksByProgram.TryGetValue(enrollment.ProgramId, out var programLinks))
            {
                continue;
            }

            programs.TryGetValue(enrollment.ProgramId, out var program);
            var achievedAt = enrollment.CompletedAt ?? enrollment.UpdatedAt ?? enrollment.CreatedAt;

            foreach (var link in programLinks)
            {
                AddEvidence(link.SkillId, new EvidenceDraft
                {
                    DedupeKey = $"program:{enrollment.ProgramId:N}",
                    Type = SkillEvidenceType.Program,
                    ProgramId = enrollment.ProgramId,
                    ProgramName = program?.Name,
                    AchievedAt = achievedAt,
                });
            }
        }

        foreach (var certificate in certificates)
        {
            var programId = certificate.ProgramId
                ?? (certificate.ModuleId.HasValue
                    ? modules.GetValueOrDefault(certificate.ModuleId.Value)?.ProgramId
                    : null);

            if (!programId.HasValue || !linksByProgram.TryGetValue(programId.Value, out var programLinks))
            {
                continue;
            }

            var applicable = programLinks.Where(link =>
                link.ModuleId == null
                || (certificate.ModuleId.HasValue && link.ModuleId == certificate.ModuleId));

            programs.TryGetValue(programId.Value, out var program);
            var certificateItem = items.FirstOrDefault(
                i => i.ItemType == PortfolioItemType.InternalCertificate
                     && i.ReferenceId == certificate.Id
                     && !i.IsDeleted);
            var achievedAt = certificate.IssueDate ?? certificate.CreatedAt;

            foreach (var link in applicable)
            {
                AddEvidence(link.SkillId, new EvidenceDraft
                {
                    DedupeKey = $"certificate:{certificate.Id:N}",
                    Type = SkillEvidenceType.Certificate,
                    ProgramId = programId.Value,
                    ProgramName = program?.Name,
                    CertificateCode = certificate.Code,
                    VerificationUrl = certificate.VerificationUrl,
                    PortfolioItemId = certificateItem?.Id,
                    AchievedAt = achievedAt,
                });
            }
        }

        foreach (var capstone in capstones)
        {
            if (!capstone.ProgramId.HasValue
                || !linksByProgram.TryGetValue(capstone.ProgramId.Value, out var programLinks))
            {
                continue;
            }

            var applicable = programLinks.Where(link =>
                link.ModuleId == null
                || (capstone.ModuleId.HasValue && link.ModuleId == capstone.ModuleId));

            programs.TryGetValue(capstone.ProgramId.Value, out var program);
            moduleEnrollments.TryGetValue(capstone.ModuleEnrollmentId ?? Guid.Empty, out var moduleEnrollment);
            var achievedAt = moduleEnrollment?.CompletedAt ?? capstone.CreatedAt;

            foreach (var link in applicable)
            {
                AddEvidence(link.SkillId, new EvidenceDraft
                {
                    DedupeKey = $"capstone:{capstone.Id:N}",
                    Type = SkillEvidenceType.Capstone,
                    ProgramId = capstone.ProgramId.Value,
                    ProgramName = program?.Name,
                    PortfolioItemId = capstone.Id,
                    AchievedAt = achievedAt,
                });
            }
        }

        return evidencesBySkill
            .Where(pair => skills.ContainsKey(pair.Key) && pair.Value.Count > 0)
            .Select(pair => new AchievedSkill(skills[pair.Key], pair.Value))
            .ToList();
    }

    private async Task EnsureCurationAsync(Guid portfolioId, List<AchievedSkill> achieved)
    {
        if (achieved.Count == 0)
        {
            return;
        }

        var existing = await LoadCurationAsync(portfolioId);
        var existingIds = existing.Select(c => c.SkillId).ToHashSet();
        var nextOrder = existing.Count == 0 ? 0 : existing.Max(c => c.DisplayOrder) + 1;
        var toAdd = new List<PortfolioSkill>();

        foreach (var skill in achieved.OrderBy(a => a.Skill.Name).ThenBy(a => a.Skill.Code))
        {
            if (existingIds.Contains(skill.Skill.Id))
            {
                continue;
            }

            toAdd.Add(new PortfolioSkill
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                SkillId = skill.Skill.Id,
                IsVisible = true,
                IsPinned = false,
                DisplayOrder = nextOrder++,
            });
        }

        if (toAdd.Count == 0)
        {
            return;
        }

        await _unitOfWork.PortfolioSkills.AddRangeAsync(toAdd);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<List<PortfolioSkill>> LoadCurationAsync(Guid portfolioId)
    {
        return await _unitOfWork.PortfolioSkills.GetAllAsync(
            ps => ps.PortfolioId == portfolioId && !ps.IsDeleted);
    }

    private static PortfolioSkillContext MapContext(
        List<AchievedSkill> achieved,
        List<PortfolioSkill> curation,
        bool forPublic)
    {
        var curationBySkill = curation.ToDictionary(c => c.SkillId);
        var skills = new List<PortfolioSkillDto>();
        var skillsByItemId = new Dictionary<Guid, List<SkillSummaryDto>>();

        foreach (var entry in achieved)
        {
            if (!curationBySkill.TryGetValue(entry.Skill.Id, out var row))
            {
                continue;
            }

            if (forPublic && !row.IsVisible)
            {
                continue;
            }

            var summary = MapSummary(entry.Skill);
            var evidences = entry.Evidences
                .OrderBy(e => e.AchievedAt)
                .Select(e => new SkillEvidenceDto
                {
                    Type = e.Type,
                    ProgramId = forPublic ? null : e.ProgramId,
                    ProgramName = e.ProgramName,
                    CertificateCode = e.CertificateCode,
                    VerificationUrl = e.VerificationUrl,
                    PortfolioItemId = e.PortfolioItemId,
                    AchievedAt = e.AchievedAt,
                })
                .ToList();

            skills.Add(new PortfolioSkillDto
            {
                SkillId = entry.Skill.Id,
                Skill = summary,
                FirstAchievedAt = evidences.Min(e => e.AchievedAt),
                EvidenceCount = evidences.Count,
                Evidences = evidences,
                IsVisible = row.IsVisible,
                IsPinned = row.IsPinned,
                DisplayOrder = row.DisplayOrder,
            });

            foreach (var evidence in evidences)
            {
                if (!evidence.PortfolioItemId.HasValue)
                {
                    continue;
                }

                if (!skillsByItemId.TryGetValue(evidence.PortfolioItemId.Value, out var itemSkills))
                {
                    itemSkills = [];
                    skillsByItemId[evidence.PortfolioItemId.Value] = itemSkills;
                }

                if (itemSkills.All(s => s.Id != summary.Id))
                {
                    itemSkills.Add(summary);
                }
            }
        }

        skills.Sort((a, b) =>
        {
            var order = a.DisplayOrder.CompareTo(b.DisplayOrder);
            return order != 0 ? order : string.Compare(a.Skill.Name, b.Skill.Name, StringComparison.Ordinal);
        });

        return new PortfolioSkillContext
        {
            Skills = skills,
            SkillsByItemId = skillsByItemId,
        };
    }

    private static SkillSummaryDto MapSummary(Skill skill) => new()
    {
        Id = skill.Id,
        Code = skill.Code,
        Name = skill.Name,
        Category = skill.Category,
        Subcategory = skill.Subcategory,
    };

    public sealed class PortfolioSkillContext
    {
        public List<PortfolioSkillDto> Skills { get; init; } = [];

        public IReadOnlyDictionary<Guid, List<SkillSummaryDto>> SkillsByItemId { get; init; }
            = new Dictionary<Guid, List<SkillSummaryDto>>();
    }

    private sealed class AchievedSkill
    {
        public AchievedSkill(Skill skill, List<EvidenceDraft> evidences)
        {
            Skill = skill;
            Evidences = evidences;
        }

        public Skill Skill { get; }

        public List<EvidenceDraft> Evidences { get; }
    }

    private sealed class EvidenceDraft
    {
        public string DedupeKey { get; init; } = null!;

        public SkillEvidenceType Type { get; init; }

        public Guid ProgramId { get; init; }

        public string? ProgramName { get; init; }

        public string? CertificateCode { get; init; }

        public string? VerificationUrl { get; init; }

        public Guid? PortfolioItemId { get; init; }

        public DateTime AchievedAt { get; set; }
    }
}
