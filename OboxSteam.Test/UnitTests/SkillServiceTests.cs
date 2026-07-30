using Microsoft.Extensions.Logging.Abstractions;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class SkillServiceTests
{
    private readonly InMemoryUnitOfWork _db = new();

    private SkillService CreateSut()
        => new(_db, NullLogger<SkillService>.Instance);

    private Skill SeedSkill(
        string code,
        string name,
        SkillCategory category,
        string? subcategory = null,
        bool isDeleted = false)
    {
        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Category = category,
            Subcategory = subcategory,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow
        };

        _db.Skills.Seed(skill);
        return skill;
    }

    [Fact]
    public async Task GetSkills_ExcludesSoftDeleted()
    {
        SeedSkill("SKL-001", "Python", SkillCategory.Technology, "Programming");
        SeedSkill("SKL-002", "Deleted Skill", SkillCategory.Math, isDeleted: true);
        var sut = CreateSut();

        var result = await sut.GetSkills(null, null, 1, 50, "name", false);

        Assert.Single(result.Items);
        Assert.Equal("SKL-001", result.Items[0].Code);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetSkills_SearchMatchesCodeNameSubcategory_CaseInsensitive()
    {
        SeedSkill("SKL-TECH-PY", "Python", SkillCategory.Technology, "Programming");
        SeedSkill("SKL-ART-DRAW", "Drawing", SkillCategory.Arts, "Visual Arts");
        SeedSkill("SKL-MATH-ALG", "Algebra", SkillCategory.Math);
        var sut = CreateSut();

        var byCode = await sut.GetSkills("skl-tech", null, 1, 50, "name", false);
        Assert.Single(byCode.Items);
        Assert.Equal("Python", byCode.Items[0].Name);

        var byName = await sut.GetSkills("DRAWING", null, 1, 50, "name", false);
        Assert.Single(byName.Items);

        var bySubcategory = await sut.GetSkills("visual", null, 1, 50, "name", false);
        Assert.Single(bySubcategory.Items);
        Assert.Equal("SKL-ART-DRAW", bySubcategory.Items[0].Code);

        var noMatch = await sut.GetSkills("robotics", null, 1, 50, "name", false);
        Assert.Empty(noMatch.Items);
        Assert.Equal(0, noMatch.TotalCount);
    }

    [Fact]
    public async Task GetSkills_FiltersByCategory()
    {
        SeedSkill("SKL-001", "Python", SkillCategory.Technology);
        SeedSkill("SKL-002", "Algebra", SkillCategory.Math);
        SeedSkill("SKL-003", "Physics", SkillCategory.Science);
        var sut = CreateSut();

        var result = await sut.GetSkills(null, SkillCategory.Math, 1, 50, "name", false);

        Assert.Single(result.Items);
        Assert.Equal("Algebra", result.Items[0].Name);
        Assert.Equal(SkillCategory.Math, result.Items[0].Category);
    }

    [Fact]
    public async Task GetSkills_SortsByConfiguredColumns()
    {
        SeedSkill("SKL-002", "Zulu", SkillCategory.Arts);
        SeedSkill("SKL-001", "Alpha", SkillCategory.Math);
        var sut = CreateSut();

        var byNameAsc = await sut.GetSkills(null, null, 1, 50, "name", false);
        Assert.Equal("Alpha", byNameAsc.Items[0].Name);

        var byNameDesc = await sut.GetSkills(null, null, 1, 50, "name", true);
        Assert.Equal("Zulu", byNameDesc.Items[0].Name);

        var byCode = await sut.GetSkills(null, null, 1, 50, "code", false);
        Assert.Equal("SKL-001", byCode.Items[0].Code);

        var byCategory = await sut.GetSkills(null, null, 1, 50, "category", false);
        Assert.Equal(2, byCategory.Items.Count);

        var byCreatedAt = await sut.GetSkills(null, null, 1, 50, "createdAt", true);
        Assert.Equal(2, byCreatedAt.Items.Count);

        var defaultSort = await sut.GetSkills(null, null, 1, 50, "unknown", false);
        Assert.Equal("Alpha", defaultSort.Items[0].Name);
    }

    [Fact]
    public async Task GetSkills_Paginates()
    {
        for (var i = 1; i <= 5; i++)
            SeedSkill($"SKL-{i:D3}", $"Skill {i:D2}", SkillCategory.Technology);

        var sut = CreateSut();

        var page1 = await sut.GetSkills(null, null, 1, 2, "name", false);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.False(page1.HasPrevious);
        Assert.True(page1.HasNext);

        var page3 = await sut.GetSkills(null, null, 3, 2, "name", false);
        Assert.Single(page3.Items);
        Assert.True(page3.HasPrevious);
        Assert.False(page3.HasNext);
    }
}
