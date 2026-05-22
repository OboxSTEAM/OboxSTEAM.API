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
}
