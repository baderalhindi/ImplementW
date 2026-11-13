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

    /// <summary>
    /// The 13 Nov 2026 review: each language's field holds its own language. Arabic text has an Arabic letter, and may still name a
    /// product or acronym in Latin letters ("خادم البريد Exchange"); English text has an English letter and no Arabic one.
    /// </summary>
    public static bool IsInScript(string value, TextScript script) => script switch
    {
        TextScript.Arabic => ArabicLetter().IsMatch(value),
        TextScript.English => LatinLetter().IsMatch(value) && !ArabicLetter().IsMatch(value),
        _ => throw new ArgumentOutOfRangeException(nameof(script)),
    };

    /// <summary>The validation code for text not in <paramref name="script"/>.</summary>
    public static string NotInScript(TextScript script) =>
        script == TextScript.Arabic ? ValidationCode.NotArabic : ValidationCode.NotEnglish;

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailShape();

    [GeneratedRegex(@"^\+[1-9][0-9]{1,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164();

    /// <summary>A letter of the Arabic blocks (Arabic, Supplement, Extended-A, Presentation Forms A and B); digits and marks are not letters.</summary>
    [GeneratedRegex(@"[\u0621-\u064A\u066E-\u06D3\u06D5\u06EE\u06EF\u06FA-\u06FC\u0750-\u077F\u08A0-\u08C9\uFB50-\uFDFB\uFE70-\uFEFC]", RegexOptions.CultureInvariant)]
    private static partial Regex ArabicLetter();

    [GeneratedRegex("[A-Za-z]", RegexOptions.CultureInvariant)]
    private static partial Regex LatinLetter();
}

/// <summary>The language a bilingual field's text must be written in.</summary>
public enum TextScript
{
    Arabic = 1,
    English = 2,
}
