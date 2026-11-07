using System.Security.Cryptography;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// A local account's password (the 5 Nov 2026 review §6.1): the rules a new one must meet, its PBKDF2-SHA256 hash, and the temporary one the platform
/// generates. The plain text is never stored or logged; an administrator sees a temporary one once, in the response that set it.
/// </summary>
public static class LocalPassword
{
    public const int MinimumLength = 12;
    public const int RememberedPasswords = 5;

    /// <summary>A field error code: the password breaks the length, letters-and-digits or not-the-username rule.</summary>
    public const string TooWeak = "PASSWORD_TOO_WEAK";

    /// <summary>A field error code: the password is one of the last five.</summary>
    public const string Reused = "PASSWORD_REUSED";

    /// <summary>A field error code: the current password given when changing it is wrong, or the account is locked.</summary>
    public const string IncorrectCurrent = "PASSWORD_INCORRECT";

    /// <summary>The directory's own password policy refused a directory account's new password.</summary>
    public const string RejectedByDirectory = "PASSWORD_REJECTED_BY_DIRECTORY";

    private const int Iterations = 600_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Scheme = "pbkdf2-sha256";
    private const string GeneratedAlphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int GeneratedLength = 16;

    public static bool MeetsRules(string password, string username)
    {
        ArgumentNullException.ThrowIfNull(password);
        return password.Length >= MinimumLength
            && password.Any(char.IsLetter)
            && password.Any(char.IsDigit)
            && !string.Equals(password, username, StringComparison.OrdinalIgnoreCase);
    }

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(stored);
        string[] parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out int iterations))
        {
            return false;
        }

        byte[] expected = Convert.FromBase64String(parts[3]);
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[2]), iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>Names one stored password in a reset token. Its input is a salted hash, so the fingerprint gives no way to test guesses at the password.</summary>
    public static string Fingerprint(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        return Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stored)));
    }

    /// <summary>A password that meets the rules, of letters and digits with the look-alikes left out.</summary>
    public static string Generate()
    {
        while (true)
        {
            string password = RandomNumberGenerator.GetString(GeneratedAlphabet, GeneratedLength);
            if (password.Any(char.IsLetter) && password.Any(char.IsDigit))
            {
                return password;
            }
        }
    }
}
