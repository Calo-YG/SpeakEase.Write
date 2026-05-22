using System.Security.Cryptography;
using System.Text;

namespace SpeakEase.Write.Application.Shared;

public readonly record struct PasswordVerificationResult(bool IsValid, bool NeedsRehash);

public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Prefix = "PBKDF2";

    public static string GenerateSalt()
    {
        var bytes = new byte[SaltSize];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string HashPassword(string password, string salt)
    {
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String(salt),
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(hashBytes)}";
    }

    public static PasswordVerificationResult VerifyPassword(string password, string salt, string storedPassword)
    {
        if (storedPassword?.StartsWith($"{Prefix}$", StringComparison.Ordinal) == true)
        {
            var currentHash = HashPassword(password, salt);
            return new PasswordVerificationResult(FixedTimeEquals(currentHash, storedPassword), false);
        }

        var legacyHash = HashLegacyPassword(password, salt);
        return new PasswordVerificationResult(FixedTimeEquals(legacyHash, storedPassword ?? string.Empty), true);
    }

    private static string HashLegacyPassword(string password, string salt)
    {
        using var hmac = new HMACSHA256(Convert.FromBase64String(salt));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
    }
}
