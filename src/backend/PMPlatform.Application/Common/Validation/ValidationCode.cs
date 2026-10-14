namespace PMPlatform.Application.Common.Validation;

/// <summary>
/// The validation codes of <c>ProblemDetails.errors[].code</c> (api-conventions R-23, R-24): what was wrong with a field, never the
/// value supplied (R-25). One list for the API's shape checks and for any module that checks text it receives (TASK-077).
/// </summary>
public static class ValidationCode
{
    public const string Required = "REQUIRED";
    public const string MaxLength = "MAX_LENGTH";
    public const string Malformed = "MALFORMED";
    public const string EnumValue = "ENUM_VALUE";
    public const string OutOfRange = "OUT_OF_RANGE";
    public const string NotAllowed = "NOT_ALLOWED";

    /// <summary>A date that must not precede another does.</summary>
    public const string DateBeforeStart = "DATE_BEFORE_START";

    /// <summary>Text holding a character no field accepts (<see cref="TextInput"/>).</summary>
    public const string InvalidCharacter = "INVALID_CHARACTER";
}
