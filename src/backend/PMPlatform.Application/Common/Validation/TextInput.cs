namespace PMPlatform.Application.Common.Validation;

/// <summary>
/// The rules every piece of text the platform receives obeys, whatever field it fills (TASK-077, CTL-11). They hold beside each
/// field's own rules, which may only be stricter: a field's maximum length is its own, and none exceeds <see cref="MaxLength"/>.
/// </summary>
/// <remarks>
/// Text is stored as entered (ADR-012), so nothing here escapes or rewrites a value; markup is made inert where text is rendered.
/// What is refused is text no field means to hold:
/// <list type="bullet">
/// <item>control characters other than tab, line feed and carriage return — U+0000 to U+001F, U+007F and U+0080 to U+009F. PostgreSQL
/// refuses U+0000 in text, which made it a 500; the rest are invisible, and carry log, header and terminal injection;</item>
/// <item>a surrogate without its pair, which is not Unicode and cannot be encoded as UTF-8 to be stored.</item>
/// </list>
/// </remarks>
public static class TextInput
{
    /// <summary>The longest text any field accepts: the longest a field of the API declares (10,000 characters).</summary>
    public const int MaxLength = 10_000;

    /// <summary>The validation code <paramref name="value"/> breaks, or null when it obeys every rule.</summary>
    public static string? Violation(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > MaxLength)
        {
            return ValidationCode.MaxLength;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(c) || IsRefusedControl(c))
            {
                return ValidationCode.InvalidCharacter;
            }
        }

        return null;
    }

    private static bool IsRefusedControl(char c) => char.IsControl(c) && c is not ('\t' or '\n' or '\r');
}
