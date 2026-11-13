using PMPlatform.Application.Common.Validation;

namespace PMPlatform.Api.Errors;

/// <summary>One <c>errors[]</c> item of a 400 (R-23): a field path and a validation code, never the value supplied (R-25).</summary>
internal sealed record FieldError(string Field, string Code)
{
    public const string Required = ValidationCode.Required;
    public const string MaxLength = ValidationCode.MaxLength;
    public const string Malformed = ValidationCode.Malformed;
    public const string EnumValue = ValidationCode.EnumValue;
    public const string OutOfRange = ValidationCode.OutOfRange;
    public const string NotAllowed = ValidationCode.NotAllowed;

    /// <summary>A date that must not precede another does (api-conventions R-23).</summary>
    public const string DateBeforeStart = ValidationCode.DateBeforeStart;

    /// <summary>Text holding a character no field accepts (TASK-077).</summary>
    public const string InvalidCharacter = ValidationCode.InvalidCharacter;

    /// <summary>The Arabic text of a bilingual field has no Arabic letter (13 Nov 2026 review).</summary>
    public const string NotArabic = ValidationCode.NotArabic;

    /// <summary>The English text of a bilingual field has no English letter, or has an Arabic one.</summary>
    public const string NotEnglish = ValidationCode.NotEnglish;
}
