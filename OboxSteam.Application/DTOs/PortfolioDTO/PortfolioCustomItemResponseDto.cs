using System.Text.Json.Serialization;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class PortfolioCustomItemResponseDto
{
    public Guid Id { get; set; }

    public PortfolioItemType ItemType { get; set; }

    public string Title { get; set; } = null!;

    public string? Subtitle { get; set; }

    public string? Organization { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Description { get; set; }

    public string? MentorEndorsement { get; set; }

    public string? StudentEditedBody { get; set; }

    public string? MediaUrl { get; set; }

    public string? ExternalUrl { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsVisible { get; set; }

    public PortfolioItemSource Source { get; set; }

    public string? AccentColor { get; set; }

    public bool? IsFeatured { get; set; }

    [JsonConverter(typeof(CamelCaseJsonStringEnumConverter))]
    public PortfolioItemSpan? Span { get; set; }

    public List<PortfolioMediaAssetResponseDto> MediaAssets { get; set; } = [];

    public Guid? ProgramId { get; set; }

    public string? ProgramName { get; set; }

    public Guid? ModuleId { get; set; }

    public string? ModuleName { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }

    public Guid? SubmissionId { get; set; }

    public List<PortfolioAppendixItemDto> AppendixSections { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
