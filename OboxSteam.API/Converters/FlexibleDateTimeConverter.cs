using System.Text.Json;
using System.Text.Json.Serialization;
using OboxSteam.Application.Utils;

namespace OboxSteam.API.Converters;

/// <summary>
/// JSON DateTime converter. See <see cref="AppDateTime"/> for the product timezone contract.
/// </summary>
public sealed class FlexibleDateTimeConverter : JsonConverter<DateTime>
{
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

        if (AppDateTime.TryParseFlexible(value, out var parsed))
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

        if (AppDateTime.TryParseFlexible(value, out var parsed))
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
