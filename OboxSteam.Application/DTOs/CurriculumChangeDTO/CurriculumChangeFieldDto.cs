using System.Text.Json.Nodes;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.CurriculumChangeDTO;

public sealed class CurriculumChangeFieldDto
{
    public string FieldKey { get; set; } = null!;
    public string Label { get; set; } = null!;
    public CurriculumFieldValueType ValueType { get; set; }
    public JsonNode? Before { get; set; }
    public JsonNode? After { get; set; }
}
