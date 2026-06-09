using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Infrastructure.Services;

public class MomoPaymentService : IMomoPaymentService
{
    private readonly MomoSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MomoPaymentService> _logger;

    public MomoPaymentService(
        IOptions<MomoSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<MomoPaymentService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> CreatePaymentUrl(Payment payment, string orderInfo)
    {
        var orderId = payment.Id.ToString("N"); // no dashes
        var requestId = Guid.NewGuid().ToString("N");
        var requestType = "captureWallet";
        var amount = (long)payment.Amount;
        var extraData = string.Empty;

        var rawSignature =
            $"accessKey={_settings.AccessKey}" +
            $"&amount={amount}" +
            $"&extraData={extraData}" +
            $"&ipnUrl={_settings.NotifyUrl}" +
            $"&orderId={orderId}" +
            $"&orderInfo={orderInfo}" +
            $"&partnerCode={_settings.PartnerCode}" +
            $"&redirectUrl={_settings.ReturnUrl}" +
            $"&requestId={requestId}" +
            $"&requestType={requestType}";

        var signature = SignHmacSha256(rawSignature, _settings.SecretKey);

        var body = new
        {
            partnerCode = _settings.PartnerCode,
            requestId,
            amount,
            orderId,
            orderInfo,
            redirectUrl = _settings.ReturnUrl,
            ipnUrl = _settings.NotifyUrl,
            requestType,
            extraData,
            lang = "vi",
            signature
        };

        _logger.LogInformation("[MoMo] Creating payment for orderId={OrderId}, amount={Amount}", orderId, amount);

        var client = _httpClientFactory.CreateClient("momo");
        var response = await client.PostAsJsonAsync($"{_settings.ApiEndpoint}/create", body);
        var json = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[MoMo] Create response: {Json}", json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("payUrl", out var payUrlProp) || string.IsNullOrEmpty(payUrlProp.GetString()))
        {
            var resultCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1;
            var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
            throw ErrorHelper.BadRequest($"MoMo payment failed (code {resultCode}): {message}");
        }

        return payUrlProp.GetString()!;
    }

    public bool ValidateCallback(Dictionary<string, string> parameters)
    {
        // MoMo IPN params for signature rebuild
        if (!parameters.TryGetValue("signature", out var receivedSignature))
            return false;

        var accessKey = parameters.GetValueOrDefault("accessKey", string.Empty);
        var amount = parameters.GetValueOrDefault("amount", string.Empty);
        var extraData = parameters.GetValueOrDefault("extraData", string.Empty);
        var message = parameters.GetValueOrDefault("message", string.Empty);
        var orderId = parameters.GetValueOrDefault("orderId", string.Empty);
        var orderInfo = parameters.GetValueOrDefault("orderInfo", string.Empty);
        var orderType = parameters.GetValueOrDefault("orderType", string.Empty);
        var partnerCode = parameters.GetValueOrDefault("partnerCode", string.Empty);
        var payType = parameters.GetValueOrDefault("payType", string.Empty);
        var requestId = parameters.GetValueOrDefault("requestId", string.Empty);
        var responseTime = parameters.GetValueOrDefault("responseTime", string.Empty);
        var resultCode = parameters.GetValueOrDefault("resultCode", string.Empty);
        var transId = parameters.GetValueOrDefault("transId", string.Empty);

        var rawSignature =
            $"accessKey={accessKey}" +
            $"&amount={amount}" +
            $"&extraData={extraData}" +
            $"&message={message}" +
            $"&orderId={orderId}" +
            $"&orderInfo={orderInfo}" +
            $"&orderType={orderType}" +
            $"&partnerCode={partnerCode}" +
            $"&payType={payType}" +
            $"&requestId={requestId}" +
            $"&responseTime={responseTime}" +
            $"&resultCode={resultCode}" +
            $"&transId={transId}";

        var expectedSignature = SignHmacSha256(rawSignature, _settings.SecretKey);
        return string.Equals(expectedSignature, receivedSignature, StringComparison.OrdinalIgnoreCase);
    }

    public (string orderId, string transactionId, int resultCode) ParseCallback(
        Dictionary<string, string> parameters)
    {
        var orderId = parameters.GetValueOrDefault("orderId", string.Empty);
        var transId = parameters.GetValueOrDefault("transId", string.Empty);
        var resultCode = int.TryParse(parameters.GetValueOrDefault("resultCode"), out var rc) ? rc : -1;
        return (orderId, transId, resultCode);
    }

    private static string SignHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLower();
    }
}
