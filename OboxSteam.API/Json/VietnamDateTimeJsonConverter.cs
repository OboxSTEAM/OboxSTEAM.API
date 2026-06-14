using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OboxSteam.API.Swagger;

namespace OboxSteam.API.Json;

/// <summary>
/// Serializes DateTime values using Vietnam reader-friendly formats at runtime.
/// Input without timezone is interpreted as GMT+7; output is formatted in GMT+7.
/// ISO 8601 strings (with offset or Z) remain supported for compatibility.
/// </summary>
public sealed class VietnamDateTimeJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert == typeof(DateTime) || typeToConvert == typeof(DateTime?);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => typeToConvert == typeof(DateTime?)
            ? new NullableVietnamDateTimeJsonConverter()
            : new VietnamDateTimeJsonConverter();
}

public sealed class VietnamDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a string value for DateTime.");
        }

        var value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("DateTime value cannot be null or empty.");
        }

        return VietnamDateTimeParsing.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var vietnamLocal = VietnamDateTimeParsing.ToVietnamLocal(value);
        writer.WriteStringValue(
            vietnamLocal.ToString(SwaggerDateTimeDisplayFormat.DateTimePattern, CultureInfo.InvariantCulture));
    }
}

public sealed class NullableVietnamDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a string value for DateTime.");
        }

        var value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return VietnamDateTimeParsing.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        var vietnamLocal = VietnamDateTimeParsing.ToVietnamLocal(value.Value);
        writer.WriteStringValue(
            vietnamLocal.ToString(SwaggerDateTimeDisplayFormat.DateTimePattern, CultureInfo.InvariantCulture));
    }
}

internal static class VietnamDateTimeParsing
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    private static readonly string[] VietnamReadFormats =
    [
        SwaggerDateTimeDisplayFormat.DateTimePattern,
        SwaggerDateTimeDisplayFormat.DatePattern,
    ];

    public static DateTime Parse(string value)
    {
        if (DateTime.TryParseExact(
                value,
                VietnamReadFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var vietnamLocal))
        {
            return ToUtcFromVietnam(vietnamLocal);
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed.Kind switch
            {
                DateTimeKind.Utc => parsed,
                DateTimeKind.Local => parsed.ToUniversalTime(),
                DateTimeKind.Unspecified => ToUtcFromVietnam(parsed),
                _ => parsed.ToUniversalTime(),
            };
        }

        throw new JsonException(
            $"The JSON value '{value}' could not be converted to DateTime. " +
            $"Use '{SwaggerDateTimeDisplayFormat.DateTimePattern}' (GMT+7) or ISO 8601.");
    }

    public static DateTime ToVietnamLocal(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime(),
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
    }

    private static DateTime ToUtcFromVietnam(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, VietnamTimeZone);
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        var timeZoneId = OperatingSystem.IsWindows()
            ? "SE Asia Standard Time"
            : "Asia/Ho_Chi_Minh";

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Vietnam",
                TimeSpan.FromHours(7),
                "Vietnam",
                "Vietnam");
        }
    }
}
