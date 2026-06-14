using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OboxSteam.API.Swagger;

/// <summary>
/// Updates Swagger schemas so DateTime fields show Vietnam reader-friendly examples.
/// </summary>
public sealed class SwaggerDateTimeSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(DateTime) || context.Type == typeof(DateTime?))
        {
            ApplyDateTimeExample(schema, isDateOnly: false);
            return;
        }

        if (schema.Properties == null)
        {
            return;
        }

        foreach (var property in schema.Properties)
        {
            if (!IsDateTimeSchema(property.Value))
            {
                continue;
            }

            ApplyDateTimeExample(property.Value, IsDateOnlyProperty(property.Key));
        }
    }

    private static void ApplyDateTimeExample(OpenApiSchema schema, bool isDateOnly)
    {
        schema.Type = "string";
        schema.Format = null;
        schema.Example = new OpenApiString(
            isDateOnly ? SwaggerDateTimeDisplayFormat.DateExample : SwaggerDateTimeDisplayFormat.DateTimeExample);
        schema.Description = isDateOnly
            ? $"Date ({SwaggerDateTimeDisplayFormat.DatePattern}, GMT+7)."
            : $"Date/time ({SwaggerDateTimeDisplayFormat.DateTimePattern}, GMT+7).";
    }

    private static bool IsDateTimeSchema(OpenApiSchema schema)
        => schema.Type == "string" && schema.Format is "date-time" or "date";

    private static bool IsDateOnlyProperty(string propertyName)
        => propertyName.EndsWith("Date", StringComparison.OrdinalIgnoreCase);
}
