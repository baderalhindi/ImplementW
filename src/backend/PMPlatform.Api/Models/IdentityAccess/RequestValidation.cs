using System.Text.RegularExpressions;
using PMPlatform.Api.Errors;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Shape validation of request strings (T-3: shape here, meaning in the module).</summary>
internal static partial class RequestValidation
{
    public const int CodeLength = 50;
    public const int NameLength = 200;
    public static void Require(string? value, string field, int maxLength, List<FieldError> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
        else if (value.Length > maxLength)
        {
            errors.Add(new FieldError(field, FieldError.MaxLength));
        }
    }

    /// <summary>A second-factor challenge id and the code entered for it (TASK-029).</summary>
    public static void RequireChallenge(string? challengeId, string? code, List<FieldError> errors)
    {
        Require(challengeId, "challengeId", 256, errors);
        Require(code, "code", 64, errors);
    }

    /// <summary>A name or label: present and not blank.</summary>
    public static void RequireText(string? value, string field, int maxLength, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
        else if (value.Length > maxLength)
        {
            errors.Add(new FieldError(field, FieldError.MaxLength));
        }
    }

    /// <summary>Absent, or present and not blank.</summary>
    public static void Optional(string? value, string field, int maxLength, List<FieldError> errors)
    {
        if (value is not null)
        {
            RequireText(value, field, maxLength, errors);
        }
    }

    public static void RequireId(Guid? value, string field, List<FieldError> errors)
    {
        if (value is null || value == Guid.Empty)
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
    }

    public static void Email(string? value, string field, List<FieldError> errors)
    {
        RequireText(value, field, NameLength, errors);
        if (!string.IsNullOrWhiteSpace(value) && value.Length <= NameLength && !EmailShape().IsMatch(value))
        {
            errors.Add(new FieldError(field, FieldError.Malformed));
        }
    }

    /// <summary>ADR-004: E.164, a plus sign and up to fifteen digits, the first not zero. The database checks the same pattern.</summary>
    public static void MobileNumber(string? value, string field, List<FieldError> errors)
    {
        if (value is not null && !E164().IsMatch(value))
        {
            errors.Add(new FieldError(field, FieldError.Malformed));
        }
    }

    /// <summary>A business code: upper-case letters, digits, hyphen and underscore.</summary>
    public static void Code(string? value, string field, List<FieldError> errors)
    {
        RequireText(value, field, CodeLength, errors);
        if (!string.IsNullOrWhiteSpace(value) && value.Length <= CodeLength && !CodeShape().IsMatch(value))
        {
            errors.Add(new FieldError(field, FieldError.Malformed));
        }
    }

    /// <summary>R-17: both languages, each present and not blank.</summary>
    public static void Label(BilingualLabelRequest? value, string field, List<FieldError> errors)
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, FieldError.Required));
            return;
        }

        RequireText(value.Ar, $"{field}.ar", NameLength, errors);
        RequireText(value.En, $"{field}.en", NameLength, errors);
    }

    /// <summary>R-18: <c>ar</c> or <c>en</c>; <paramref name="fallback"/> when absent, or REQUIRED if there is none.</summary>
    public static Language Language(string? value, string field, Language? fallback, List<FieldError> errors)
    {
        switch (value)
        {
            case "ar":
                return Domain.Common.Language.Ar;
            case "en":
                return Domain.Common.Language.En;
            case null when fallback is { } language:
                return language;
            case null:
                errors.Add(new FieldError(field, FieldError.Required));
                return default;
            default:
                errors.Add(new FieldError(field, FieldError.EnumValue));
                return default;
        }
    }

    [GeneratedRegex(@"^\+[1-9][0-9]{1,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailShape();

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();
}
