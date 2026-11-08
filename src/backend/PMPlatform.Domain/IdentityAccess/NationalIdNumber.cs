namespace PMPlatform.Domain.IdentityAccess;

/// <summary>A Saudi National ID (starts with 1) or Iqama number (starts with 2): ten digits whose last is a Luhn check digit.</summary>
public static class NationalIdNumber
{
    public const int Length = 10;

    public const int VisibleDigits = 4;

    public static bool IsValid(string? value)
    {
        if (value is not { Length: Length } || value[0] is not ('1' or '2') || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        int sum = 0;
        for (int i = 0; i < Length; i++)
        {
            int digit = value[i] - '0';
            if (i % 2 == 0)
            {
                digit *= 2;
                digit = digit > 9 ? digit - 9 : digit;
            }

            sum += digit;
        }

        return sum % 10 == 0;
    }

    /// <summary>"******6789": the last four digits only.</summary>
    public static string Masked(string lastDigits) => new string('*', Length - VisibleDigits) + lastDigits;
}
