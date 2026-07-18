using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Ensures a string is null/whitespace or a well-formed JSON object.
/// Does not validate inner keys or values.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class JsonObjectAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
        {
            return ValidationResult.Success;
        }

        try
        {
            using var doc = JsonDocument.Parse(s);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? ValidationResult.Success
                : new ValidationResult(
                    $"{validationContext.DisplayName} must be a JSON object.",
                    [validationContext.MemberName ?? string.Empty]);
        }
        catch (JsonException)
        {
            return new ValidationResult(
                $"{validationContext.DisplayName} must be valid JSON.",
                [validationContext.MemberName ?? string.Empty]);
        }
    }
}
