namespace PMPlatform.Api.Errors;

/// <summary>One <c>errors[]</c> item of a 400 (R-23): a field path and a validation code, never the value supplied (R-25).</summary>
internal sealed record FieldError(string Field, string Code)
{
    public const string Required = "REQUIRED";
    public const string MaxLength = "MAX_LENGTH";
    public const string Malformed = "MALFORMED";
    public const string EnumValue = "ENUM_VALUE";
    public const string OutOfRange = "OUT_OF_RANGE";
    public const string NotAllowed = "NOT_ALLOWED";

    /// <summary>A date that must not precede another does (api-conventions R-23).</summary>
    public const string DateBeforeStart = "DATE_BEFORE_START";
}
