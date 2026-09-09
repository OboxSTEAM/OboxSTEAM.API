using System.Security.Cryptography;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Generates cryptographically random temporary passwords for provisioned staff accounts.
/// Omits ambiguous characters (0/O, 1/l/I) for easier reading in email.
/// </summary>
public static class TemporaryPasswordGenerator
{
    private const string Alphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";

    public static string Generate(int length = 12)
    {
        if (length < 8)
            throw new ArgumentOutOfRangeException(nameof(length), "Password length must be at least 8.");

        var bytes = RandomNumberGenerator.GetBytes(length);
        return string.Create(length, bytes, static (chars, source) =>
        {
            for (var i = 0; i < chars.Length; i++)
                chars[i] = Alphabet[source[i] % Alphabet.Length];
        });
    }
}
