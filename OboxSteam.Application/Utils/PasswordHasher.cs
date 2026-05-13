using System.Security.Cryptography;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Secure password hasher using PBKDF2 (RFC 2898) with HMAC-SHA256.
/// This is the industry-standard approach for password storage.
/// Each password gets a unique 16-byte random salt — brute-forcing one hash
/// does not help crack any other hash in the database.
/// </summary>
public class PasswordHasher
{
    private const int SaltSize = 16;       // 128-bit salt
    private const int HashSize = 32;       // 256-bit output
    private const int Iterations = 100_000; // NIST recommended minimum

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.");

        // Generate a cryptographically random salt
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        // Derive key with PBKDF2-HMAC-SHA256
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        // Store as "iterations:salt:hash" (all Base64)
        return $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string? storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split(':');
        if (parts.Length != 3) return false;

        if (!int.TryParse(parts[0], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}