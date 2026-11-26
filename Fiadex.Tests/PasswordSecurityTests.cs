using Xunit;

namespace Fiadex.Tests;

public class PasswordSecurityTests
{
    private const string ValidPassword = "ClaveDePrueba123!";

    [Fact]
    public void HashPassword_UsesVersionedPrefix()
    {
        var (hash, _) = PasswordSecurity.HashPassword(ValidPassword);

        Assert.StartsWith("F2$", hash);
        Assert.False(PasswordSecurity.NeedsRehash(hash));
    }

    [Fact]
    public void VerifyPassword_AcceptsCurrentFormat()
    {
        var (hash, salt) = PasswordSecurity.HashPassword(ValidPassword);

        Assert.True(PasswordSecurity.VerifyPassword(ValidPassword, salt, hash));
        Assert.False(PasswordSecurity.VerifyPassword("OtraClave2009.", salt, hash));
    }

    [Fact]
    public void VerifyPassword_AcceptsLegacyFormatAndFlagsItForRehash()
    {
        var salt = Convert.ToBase64String(new byte[16]);
        var legacyHash = LegacyHash(ValidPassword, salt);

        Assert.True(PasswordSecurity.VerifyPassword(ValidPassword, salt, legacyHash));
        Assert.True(PasswordSecurity.NeedsRehash(legacyHash));
    }

    [Fact]
    public void RehashPassword_KeepsSaltAndVerifiesWithNewFormat()
    {
        var salt = Convert.ToBase64String(new byte[16]);
        var legacyHash = LegacyHash(ValidPassword, salt);
        var rehashed = PasswordSecurity.RehashPassword(ValidPassword, salt);

        Assert.True(PasswordSecurity.VerifyPassword(ValidPassword, salt, rehashed));
        Assert.False(PasswordSecurity.NeedsRehash(rehashed));
    }

    private static string LegacyHash(string password, string saltText)
    {
        var salt = Convert.FromBase64String(saltText);
        var bytes = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            password, salt, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(bytes);
    }
}
