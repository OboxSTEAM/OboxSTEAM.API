using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Interfaces;

public interface IMomoPaymentService
{
    /// <summary>Builds the MoMo payment URL for the given payment.</summary>
    Task<string> CreatePaymentUrl(Payment payment, string orderInfo);

    /// <summary>Verifies the HMAC-SHA256 signature on an incoming MoMo IPN callback.</summary>
    bool ValidateCallback(Dictionary<string, string> parameters);

    /// <summary>Extracts orderId, transactionId, and resultCode from MoMo callback params.</summary>
    (string orderId, string transactionId, int resultCode) ParseCallback(
        Dictionary<string, string> parameters);
}
