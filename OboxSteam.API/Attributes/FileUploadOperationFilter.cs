using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;


public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParameters = context.ApiDescription.ParameterDescriptions
            .Where(p => p.ModelMetadata?.ModelType == typeof(IFormFile))
            .ToList();

        if (!fileParameters.Any())
            return;

        // Collect all form parameters (file and non-file)
        var allFormParameters = context.ApiDescription.ParameterDescriptions
            .Where(p => p.Source?.Id == "Form" || p.ModelMetadata?.ModelType == typeof(IFormFile))
            .ToList();

        // Clear existing parameters that will be part of the request body
        foreach (var param in allFormParameters)
        {
            var paramToRemove = operation.Parameters
                .FirstOrDefault(p => p.Name == param.Name);

            if (paramToRemove != null)
            {
                operation.Parameters.Remove(paramToRemove);
            }
        }

        // Build schema with all form properties
        var properties = new Dictionary<string, OpenApiSchema>();
        var required = new HashSet<string>();

        foreach (var param in allFormParameters)
        {
            if (param.ModelMetadata?.ModelType == typeof(IFormFile))
            {
                properties[param.Name] = new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary"
                };
            }
            else
            {
                var schema = context.SchemaGenerator.GenerateSchema(
                    param.ModelMetadata!.ModelType,
                    context.SchemaRepository);
                properties[param.Name] = schema;
            }

            if (param.IsRequired)
            {
                required.Add(param.Name);
            }
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = required.Any(),
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = properties,
                        Required = required
                    }
                }
            }
        };
    }
}
