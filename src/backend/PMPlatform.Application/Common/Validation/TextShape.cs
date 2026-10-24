using System.Text.RegularExpressions;

namespace PMPlatform.Application.Common.Validation;

/// <summary>The shapes a request field and an imported cell share (T-3: shape at the edge, meaning in the module).</summary>
public static partial class TextShape
{
    /// <summary>A business code: upper-case letters, digits, hyphen and underscore.</summary>
    public static bool IsCode(string value) => CodeShape().IsMatch(value);

    public static bool IsEmail(string value) => EmailShape().IsMatch(value);

    /// <summary>ADR-004: E.164, a plus sign and up to fifteen digits, the first not zero. The database checks the same pattern.</summary>
    public static bool IsE164(string value) => E164().IsMatch(value);

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailShape();

    [GeneratedRegex(@"^\+[1-9][0-9]{1,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164();
}
