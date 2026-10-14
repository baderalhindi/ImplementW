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
}
