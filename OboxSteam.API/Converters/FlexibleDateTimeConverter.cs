using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OboxSteam.API.Converters;

public sealed class FlexibleDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly string[] SimpleFormats =
    [
        "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm",
        "dd/MM/yyyy"
    ];

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            throw new JsonException("Cannot convert null to non-nullable DateTime.");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("DateTime value cannot be empty.");
        }

        if (TryParseFlexible(value, out var parsed))
        {
            return parsed;
        }

        throw new JsonException(
            $"Invalid DateTime format '{value}'. Expected dd/MM/yyyy HH:mm[:ss] or ISO 8601.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }

    internal static bool TryParseFlexible(string value, out DateTime result)
    {
        if (DateTime.TryParseExact(
                value,
                SimpleFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
        {
            result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result))
        {
            return true;
        }

        result = default;
        return false;
    }
}

public sealed class FlexibleDateTimeNullableConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("DateTime value cannot be empty.");
        }

        if (FlexibleDateTimeConverter.TryParseFlexible(value, out var parsed))
        {
            return parsed;
        }

        throw new JsonException(
            $"Invalid DateTime format '{value}'. Expected dd/MM/yyyy HH:mm[:ss] or ISO 8601.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}
