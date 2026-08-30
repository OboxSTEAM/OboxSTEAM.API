using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OboxSteam.Application.Utils;

namespace OboxSteam.Application.Services;

/// <summary>Issues RS256 JWTs accepted by 8x8 JaaS for a single meeting room.</summary>
public interface IJaasJwtService
{
    string AppId { get; }

    string Domain { get; }

    /// <summary>
    /// Creates a short-lived JaaS JWT for <paramref name="roomName"/>.
    /// Mentors receive <c>moderator: true</c> so they can open/control the room.
    /// </summary>
    string CreateMeetingToken(
        string roomName,
        Guid userId,
        string displayName,
        string? email,
        bool isModerator,
        DateTime utcNow);
}

/// <summary>
/// Reads JaaS credentials from environment variables
/// (<c>JaaS__AppId</c>, <c>JaaS__KeyId</c>, <c>JaaS__PrivateKey</c>, <c>JaaS__Domain</c>),
/// same pattern as AWS / Stripe secrets.
/// </summary>
public sealed class JaasJwtService : IJaasJwtService
{
    private readonly string _appId;
    private readonly string _keyId;
    private readonly string _privateKey;
    private readonly string _domain;
    private readonly ILogger<JaasJwtService> _logger;
    private readonly Lazy<RsaSecurityKey> _signingKey;

    public JaasJwtService(ILogger<JaasJwtService> logger)
        : this(
            Environment.GetEnvironmentVariable("JaaS__AppId"),
            Environment.GetEnvironmentVariable("JaaS__KeyId"),
            Environment.GetEnvironmentVariable("JaaS__PrivateKey"),
            Environment.GetEnvironmentVariable("JaaS__Domain"),
            logger)
    {
    }

    /// <summary>Test / explicit wiring overload (same env keys as docker-compose).</summary>
    internal JaasJwtService(
        string? appId,
        string? keyId,
        string? privateKey,
        string? domain,
        ILogger<JaasJwtService> logger)
    {
        _appId = appId?.Trim() ?? string.Empty;
        _keyId = keyId?.Trim() ?? string.Empty;
        _privateKey = privateKey ?? string.Empty;
        _domain = string.IsNullOrWhiteSpace(domain) ? "8x8.vc" : domain.Trim();
        _logger = logger;
        _signingKey = new Lazy<RsaSecurityKey>(CreateSigningKey);
    }

    public string AppId => _appId;

    public string Domain => _domain;

    public string CreateMeetingToken(
        string roomName,
        Guid userId,
        string displayName,
        string? email,
        bool isModerator,
        DateTime utcNow)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(roomName))
            throw ErrorHelper.BadRequest("Room name is required for JaaS join.");

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var expires = now.AddHours(3);

        var claims = new Dictionary<string, object>
        {
            ["aud"] = "jitsi",
            ["iss"] = "chat",
            ["sub"] = _appId,
            ["room"] = roomName,
            ["context"] = new Dictionary<string, object>
            {
                ["user"] = new Dictionary<string, object>
                {
                    ["id"] = userId.ToString(),
                    ["name"] = displayName,
                    ["email"] = email ?? string.Empty,
                    ["moderator"] = isModerator ? "true" : "false",
                },
                ["features"] = new Dictionary<string, object>
                {
                    ["livestreaming"] = false,
                    ["outbound-call"] = false,
                    ["transcription"] = false,
                    ["recording"] = isModerator,
                },
            },
        };

        var credentials = new SigningCredentials(_signingKey.Value, SecurityAlgorithms.RsaSha256);
        var header = new JwtHeader(credentials);
        header["kid"] = _keyId;

        var payload = new JwtPayload(
            issuer: "chat",
            audience: "jitsi",
            claims: null,
            notBefore: now.AddMinutes(-1),
            expires: expires,
            issuedAt: now);

        foreach (var (key, value) in claims)
        {
            if (payload.ContainsKey(key))
                continue;
            payload[key] = value;
        }

        payload["sub"] = _appId;
        payload["room"] = roomName;

        var token = new JwtSecurityToken(header, payload);
        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogDebug(
            "Issued JaaS JWT for room {Room}, user {UserId}, moderator={IsModerator}.",
            roomName,
            userId,
            isModerator);

        return encoded;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_appId)
            || string.IsNullOrWhiteSpace(_keyId)
            || string.IsNullOrWhiteSpace(_privateKey))
        {
            throw ErrorHelper.BadRequest(
                "JaaS is not configured. Set JaaS__AppId, JaaS__KeyId, and JaaS__PrivateKey.");
        }
    }

    private RsaSecurityKey CreateSigningKey()
    {
        EnsureConfigured();

        var pem = NormalizePem(_privateKey);
        try
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return new RsaSecurityKey(rsa) { KeyId = _keyId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import JaaS RSA private key PEM.");
            throw ErrorHelper.BadRequest("JaaS private key PEM is invalid.");
        }
    }

    internal static string NormalizePem(string privateKey)
    {
        var trimmed = privateKey.Trim().Trim('"');
        return trimmed
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
