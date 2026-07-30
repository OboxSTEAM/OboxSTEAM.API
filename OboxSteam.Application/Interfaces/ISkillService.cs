using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.SkillDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface ISkillService
{
    /// <summary>
    /// Get a paginated list of skills from the catalog (soft-deleted excluded).
    /// Supports search across code/name/subcategory, category filter, and sort.
    /// </summary>
    Task<Pagination<SkillSummaryDto>> GetSkills(
        string? search,
        SkillCategory? category,
        int page,
        int pageSize,
        string? sortBy,
        bool isDescending);
}
