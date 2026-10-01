using System.Reflection;

namespace OboxSteam.Application.Utils;

public static class UpdateHelper
{
    public static bool ApplyUpdates<TEntity, TDto>(TEntity entity, TDto updateDto)
    {
        bool isUpdated = false;
        var dtoProperties = typeof(TDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var dtoProp in dtoProperties)
        {
            if (TryGetPendingChange(entity, updateDto, dtoProp, out var entityProp, out var updateValue))
            {
                entityProp.SetValue(entity, updateValue);
                isUpdated = true;
            }
        }
        return isUpdated;
    }

    /// <summary>
    /// True when <see cref="ApplyUpdates"/> would change at least one of the named entity properties.
    /// </summary>
    public static bool WouldChange<TEntity, TDto>(TEntity entity, TDto updateDto, IEnumerable<string> propertyNames)
    {
        foreach (var name in propertyNames)
        {
            var dtoProp = typeof(TDto).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (dtoProp != null && TryGetPendingChange(entity, updateDto, dtoProp, out _, out _))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryGetPendingChange<TEntity, TDto>(
        TEntity entity,
        TDto updateDto,
        PropertyInfo dtoProp,
        out PropertyInfo entityProp,
        out object? updateValue)
    {
        entityProp = null!;
        updateValue = dtoProp.GetValue(updateDto);

        // Bỏ qua nếu giá trị là null
        if (updateValue == null) return false;

        // Block luôn trường hợp chuỗi rỗng (empty hoặc toàn khoảng trắng)
        if (updateValue is string strValue && string.IsNullOrWhiteSpace(strValue)) return false;

        var property = typeof(TEntity).GetProperty(dtoProp.Name, BindingFlags.Public | BindingFlags.Instance);

        // Đảm bảo property tồn tại bên Entity và có thể ghi
        if (property == null || !property.CanWrite) return false;

        // Chỉ cập nhật và đánh dấu là có thay đổi nếu giá trị khác với hiện tại
        if (Equals(property.GetValue(entity), updateValue)) return false;

        entityProp = property;
        return true;
    }
}
