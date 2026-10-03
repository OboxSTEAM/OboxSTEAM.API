using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.SkillDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class SkillService : ISkillService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SkillService> _logger;

    public SkillService(IUnitOfWork unitOfWork, ILogger<SkillService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task<Pagination<SkillSummaryDto>> GetSkills(
        string? search,
        SkillCategory? category,
        int page,
        int pageSize,
        string? sortBy,
        bool isDescending)
    {
        _logger.LogInformation(
            "[GetSkills] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}', category: {Category}",
            page, pageSize, search, category);

        var query = _unitOfWork.Skills
            .GetQueryable()
            .Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(s =>
                s.Code.ToLower().Contains(lowerSearch) ||
                s.Name.ToLower().Contains(lowerSearch) ||
                (s.Subcategory != null && s.Subcategory.ToLower().Contains(lowerSearch)));
        }

        if (category.HasValue)
            query = query.Where(s => s.Category == category.Value);

        query = sortBy?.ToLower() switch
        {
            "code" => isDescending
                ? query.OrderByDescending(s => s.Code)
                : query.OrderBy(s => s.Code),
            "category" => isDescending
                ? query.OrderByDescending(s => s.Category)
                : query.OrderBy(s => s.Category),
            "createdat" => isDescending
                ? query.OrderByDescending(s => s.CreatedAt)
                : query.OrderBy(s => s.CreatedAt),
            _ => isDescending
                ? query.OrderByDescending(s => s.Name)
                : query.OrderBy(s => s.Name),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SkillSummaryDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Category = s.Category,
                Subcategory = s.Subcategory
            })
            .ToList();

        _logger.LogInformation(
            "[GetSkills] Retrieved {Count}/{Total} skills.",
            items.Count, totalCount);

        return Task.FromResult(new Pagination<SkillSummaryDto>(items, totalCount, page, pageSize));
    }

    public async Task<SkillDetailDto> CreateSkill(CreateSkillRequestDto request)
    {
        if (request == null)
        {
            throw ErrorHelper.BadRequest("Skill is required.");
        }

        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw ErrorHelper.BadRequest("Skill code is required.");
        }

        if (code.Length > 50)
        {
            throw ErrorHelper.BadRequest("Skill code cannot exceed 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw ErrorHelper.BadRequest("Skill name is required.");
        }

        if (name.Length > 255)
        {
            throw ErrorHelper.BadRequest("Skill name cannot exceed 255 characters.");
        }

        if (!request.Category.HasValue || !Enum.IsDefined(request.Category.Value))
        {
            throw ErrorHelper.BadRequest("Skill category is required.");
        }

        var subcategory = string.IsNullOrWhiteSpace(request.Subcategory)
            ? null
            : request.Subcategory.Trim();
        if (subcategory != null && subcategory.Length > 100)
        {
            throw ErrorHelper.BadRequest("Skill subcategory cannot exceed 100 characters.");
        }

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        var normalizedCode = code.ToLower();
        var codeTaken = await _unitOfWork.Skills.AnyIncludingDeletedAsync(
            skill => skill.Code.ToLower() == normalizedCode);
        if (codeTaken)
        {
            throw ErrorHelper.Conflict($"Skill with code '{code}' already exists.");
        }

        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Category = request.Category.Value,
            Subcategory = subcategory,
            Description = description,
        };

        await _unitOfWork.Skills.AddAsync(skill);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("[CreateSkill] Created skill {Code} with Id {Id}.", skill.Code, skill.Id);

        return new SkillDetailDto
        {
            Id = skill.Id,
            Code = skill.Code,
            Name = skill.Name,
            Category = skill.Category,
            Subcategory = skill.Subcategory,
            Description = skill.Description,
        };
    }
}
