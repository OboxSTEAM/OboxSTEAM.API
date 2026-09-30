using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OboxSteam.Application.Commons.CurriculumChanges;

/// <summary>
/// Serialization for change-log JSON columns and typed field values.
/// </summary>
public static class CurriculumChangeJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        int number => JsonValue.Create(number),
        long number => JsonValue.Create(number),
        // Dividing by 1.000...m strips trailing zeros so 5.00 (database scale) equals 5 (request).
        decimal number => JsonValue.Create(number / 1.000000000000000000000000000000000m),
        double number => JsonValue.Create(number),
        Guid id => JsonValue.Create(id.ToString()),
        DateTime at => JsonValue.Create(at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
        Enum member => JsonValue.Create(member.ToString()),
        IEnumerable<string> items => new JsonArray(items.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray()),
        _ => JsonValue.Create(value.ToString()),
    };

    public static bool NodesEqual(JsonNode? left, JsonNode? right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }

        return string.Equals(left.ToJsonString(), right.ToJsonString(), StringComparison.Ordinal);
    }

    public static string SerializeFields(IEnumerable<CurriculumFieldChange> fields)
        => JsonSerializer.Serialize(fields, Options);

    public static List<CurriculumFieldChange> DeserializeFields(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<CurriculumFieldChange>>(json, Options) ?? [];

    public static string SerializePath(IEnumerable<CurriculumPathSegment> path)
        => JsonSerializer.Serialize(path, Options);

    public static List<CurriculumPathSegment> DeserializePath(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<CurriculumPathSegment>>(json, Options) ?? [];
}
