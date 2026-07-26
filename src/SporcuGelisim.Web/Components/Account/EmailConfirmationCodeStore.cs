using System.Security.Cryptography;
using System.Text;

namespace SporcuGelisim.Web.Components.Account;

internal static class EmailConfirmationCodeStore
{
    public const string LoginProvider = "EmailConfirmation";
    public const string TokenName = "Code";
    public const string EmailChangeLoginProvider = "EmailChange";

    public static string GenerateCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string CreateTokenValue(Guid userId, string code, DateTimeOffset now)
    {
        var expiresAt = now.AddMinutes(30);
        return $"{expiresAt:O}|{Hash(userId, code)}";
    }

    public static string CreateEmailChangeTokenValue(Guid userId, string newEmail, string code, DateTimeOffset now)
    {
        var expiresAt = now.AddMinutes(30);
        return $"{expiresAt:O}|{newEmail.Trim()}|{Hash(userId, newEmail.Trim().ToUpperInvariant(), code)}";
    }

    public static bool Verify(Guid userId, string code, string? tokenValue, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            return false;
        }

        var parts = tokenValue.Split('|', 2);
        if (parts.Length != 2 || !DateTimeOffset.TryParse(parts[0], out var expiresAt) || expiresAt < now)
        {
            return false;
        }

        try
        {
            var expectedHash = Convert.FromHexString(parts[1]);
            var actualHash = Convert.FromHexString(Hash(userId, code));
            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool VerifyEmailChange(Guid userId, string code, string? tokenValue, DateTimeOffset now, out string newEmail)
    {
        newEmail = string.Empty;
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            return false;
        }

        var parts = tokenValue.Split('|', 3);
        if (parts.Length != 3 || !DateTimeOffset.TryParse(parts[0], out var expiresAt) || expiresAt < now)
        {
            return false;
        }

        newEmail = parts[1];
        try
        {
            var expectedHash = Convert.FromHexString(parts[2]);
            var actualHash = Convert.FromHexString(Hash(userId, newEmail.Trim().ToUpperInvariant(), code));
            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            newEmail = string.Empty;
            return false;
        }
    }

    private static string Hash(Guid userId, string code)
    {
        var input = Encoding.UTF8.GetBytes($"{userId:N}:{code.Trim()}");
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private static string Hash(Guid userId, string email, string code)
    {
        var input = Encoding.UTF8.GetBytes($"{userId:N}:{email.Trim()}:{code.Trim()}");
        return Convert.ToHexString(SHA256.HashData(input));
    }
}
