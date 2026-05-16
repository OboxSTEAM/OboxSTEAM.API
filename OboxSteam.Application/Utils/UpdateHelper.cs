using System.Reflection;

namespace OboxSteam.Application.Utils;

public static class UpdateHelper
{
    public static bool ApplyUpdates<TEntity, TDto>(TEntity entity, TDto updateDto)
    {
        bool isUpdated = false;
        var entityType = typeof(TEntity);
        var dtoProperties = typeof(TDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var dtoProp in dtoProperties)
        {
            var updateValue = dtoProp.GetValue(updateDto);

            // Bỏ qua nếu giá trị là null
            if (updateValue == null) continue;

            // Block luôn trường hợp chuỗi rỗng (empty hoặc toàn khoảng trắng)
            if (updateValue is string strValue && string.IsNullOrWhiteSpace(strValue)) continue;

            var entityProp = entityType.GetProperty(dtoProp.Name, BindingFlags.Public | BindingFlags.Instance);

            // Đảm bảo property tồn tại bên Entity và có thể ghi
            if (entityProp != null && entityProp.CanWrite)
            {
                var currentValue = entityProp.GetValue(entity);

                // Chỉ cập nhật và đánh dấu là có thay đổi nếu giá trị khác với hiện tại
                if (!Equals(currentValue, updateValue))
                {
                    entityProp.SetValue(entity, updateValue);
                    isUpdated = true;
                }
            }
        }
        return isUpdated;
    }
}

