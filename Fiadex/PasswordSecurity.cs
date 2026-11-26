using System.Security.Cryptography;

namespace Fiadex;

public static class PasswordSecurity
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int CurrentIterations = 600_000;
    private const int LegacyIterations = 100_000;
    private const string VersionPrefix = "F2$";

    public static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, CurrentIterations, HashSize);

        return (VersionPrefix + Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password, string saltText, string hashText)
    {
        var currentVersion = hashText.StartsWith(VersionPrefix, StringComparison.Ordinal);
        var iterations = currentVersion ? CurrentIterations : LegacyIterations;
        var hashTextWithoutPrefix = currentVersion ? hashText[VersionPrefix.Length..] : hashText;

        var salt = Convert.FromBase64String(saltText);
        var expected = Convert.FromBase64String(hashTextWithoutPrefix);
        var actual = Derive(password, salt, iterations, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static string RehashPassword(string password, string saltText)
    {
        var salt = Convert.FromBase64String(saltText);
        var hash = Derive(password, salt, CurrentIterations, HashSize);

        return VersionPrefix + Convert.ToBase64String(hash);
    }

    public static bool NeedsRehash(string hashText)
    {
        return !hashText.StartsWith(VersionPrefix, StringComparison.Ordinal);
    }

    public static void ValidatePassword(string password)
    {
        if (password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new ArgumentException(
                "La contraseña debe tener 12 caracteres, mayúscula, minúscula, número y símbolo.");
        }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int hashSize)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            hashSize);
    }
}
