using OboxSteam.Application.Utils;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Reusable validation when a new item order must be strictly greater than the current maximum in a scope.
/// </summary>
public static class SequentialOrderValidator
{
    public static void ValidateMustExceedMax(
        int requestedOrder,
        int currentMaxOrder,
        string orderPropertyName = "Order",
        string scopeDescription = "this scope")
    {
        if (requestedOrder <= currentMaxOrder)
        {
            throw ErrorHelper.BadRequest(
                $"{orderPropertyName} must be greater than the current maximum order ({currentMaxOrder}) for {scopeDescription}. " +
                $"Use {orderPropertyName} {currentMaxOrder + 1} or higher.");
        }
    }

    /// <summary>
    /// Validates that a requested order sits within an inclusive range. Used for insert/reorder
    /// operations where an item can be placed at any existing slot (or appended).
    /// </summary>
    public static void ValidateWithinRange(
        int requestedOrder,
        int minOrder,
        int maxOrder,
        string orderPropertyName = "Order",
        string scopeDescription = "this scope")
    {
        if (requestedOrder < minOrder || requestedOrder > maxOrder)
        {
            throw ErrorHelper.BadRequest(
                $"{orderPropertyName} must be between {minOrder} and {maxOrder} for {scopeDescription}.");
        }
    }
}
