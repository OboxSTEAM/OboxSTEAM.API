using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OboxSteam.Infrastructure.Commons;

public static class ModelBuilderEnumExtensions
{
    /// <summary>
    /// Converts all enum properties in the model to string columns in the database.
    /// This improves readability and makes migrations safer when enum values change.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance</param>
    public static void UseStringForEnums(this ModelBuilder modelBuilder)
    {
        // Loop through every entity type in the model
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Loop through each property of that entity
            foreach (var property in entityType.GetProperties())
            {
                var propertyType = property.ClrType;

                // Handle nullable enums (e.g., ActivityStatus? or TimeSlot?)
                var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

                if (underlyingType.IsEnum)
                {
                    // Create converter: Enum <-> String
                    var converterType = typeof(EnumToStringConverter<>).MakeGenericType(underlyingType);
                    var converter = (ValueConverter)Activator.CreateInstance(
                        converterType, 
                        (ConverterMappingHints?)null)!;

                    property.SetValueConverter(converter);
                }
            }
        }
    }
}
