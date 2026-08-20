using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// QR / 6-digit-code check-in rules for Offline sessions.
/// The token and code rotate together and share a short TTL so a photographed
/// QR code cannot be reused by students who are not physically present.
/// </summary>
public static class ClassSessionCheckInValidator
{
    /// <summary>How long a generated token/code pair stays valid.</summary>
    public const int TokenTtlSeconds = 60;

    public const string SessionNotOpenMessage =
        "This session is not open for check-in.";

    public const string NoActiveTokenMessage =
        "This session does not have an active check-in QR code. Ask your mentor to display one.";

    public const string TokenExpiredMessage =
        "The check-in QR code has expired. Ask your mentor for the current code.";

    public const string TokenInvalidMessage =
        "Invalid check-in token or code.";

    public static void ValidateSessionOpenForCheckIn(ClassSession session)
    {
        if (session.Status is not (ClassSessionStatus.Scheduled or ClassSessionStatus.InProgress))
        {
            throw ErrorHelper.BadRequest(SessionNotOpenMessage);
        }
    }

    public static void ValidateTokenOrCode(
        ClassSession session,
        Guid? token,
        string? code,
        DateTime now)
    {
        if (session.CheckInToken is null || session.CheckInTokenExpiresAt is null)
        {
            throw ErrorHelper.BadRequest(NoActiveTokenMessage);
        }

        if (now > session.CheckInTokenExpiresAt.Value)
        {
            throw ErrorHelper.BadRequest(TokenExpiredMessage);
        }

        var tokenMatches = token.HasValue && token.Value == session.CheckInToken.Value;
        var codeMatches = !string.IsNullOrWhiteSpace(code)
            && string.Equals(code.Trim(), session.CheckInCode, StringComparison.Ordinal);

        if (!tokenMatches && !codeMatches)
        {
            throw ErrorHelper.BadRequest(TokenInvalidMessage);
        }
    }
}
