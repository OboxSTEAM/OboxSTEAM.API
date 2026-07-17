using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.PortfolioDTO;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class PortfolioMediaAssetInputDto
{
    /// <summary>Id of a caller-owned portfolio media upload. Server fills url/type.</summary>
    public Guid? Id { get; set; }

    [MaxLength(255)]
    public string? Caption { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "DisplayOrder cannot be negative.")]
    public int DisplayOrder { get; set; }
}

public class PortfolioMediaAssetResponseDto
{
    public Guid Id { get; set; }

    public string Url { get; set; } = null!;

    public PortfolioMediaType Type { get; set; }

    public string? Caption { get; set; }

    public int DisplayOrder { get; set; }
}

public class PortfolioMediaUploadResponseDto
{
    public Guid Id { get; set; }

    public string Url { get; set; } = null!;

    public PortfolioMediaType Type { get; set; }

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }
}
