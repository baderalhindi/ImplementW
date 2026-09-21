namespace PMPlatform.Domain.Common;

/// <summary>The two platform languages (ADR-012). Persisted as the ISO 639-1 code in a <c>char(2)</c> column.</summary>
public enum Language
{
    Ar = 1,
    En = 2,
}

public static class LanguageCode
{
    public static string Of(Language language) => language switch
    {
        Language.Ar => "ar",
        Language.En => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown language."),
    };

    public static Language Parse(string code) => code switch
    {
        "ar" => Language.Ar,
        "en" => Language.En,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Language code must be 'ar' or 'en'."),
    };
}
