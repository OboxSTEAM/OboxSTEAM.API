using System.Text.Json;
using System.Text.Json.Serialization;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Serializes enums as camelCase strings (e.g. Sm → "sm") for portfolio theme/span contracts.
/// </summary>
public sealed class CamelCaseJsonStringEnumConverter : JsonStringEnumConverter
{
    public CamelCaseJsonStringEnumConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}
