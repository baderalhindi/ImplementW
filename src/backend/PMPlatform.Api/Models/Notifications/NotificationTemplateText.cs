using System.Text.RegularExpressions;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;

namespace PMPlatform.Api.Models.Notifications;

/// <summary>Shape validation of template requests (T-3: shape here, the content rules in the module).</summary>
internal static partial class NotificationTemplateText
{
    public const int CodeLength = 100;
    public const int SubjectLength = 500;
    public const int BodyLength = 10_000;

    /// <summary>A family code as FG-04 stores it.</summary>
    public static void Code(string? value, string field, List<FieldError> errors)
    {
        RequestValidation.RequireText(value, field, CodeLength, errors);
        if (!string.IsNullOrWhiteSpace(value) && value.Length <= CodeLength && !CodeShape().IsMatch(value))
        {
            errors.Add(new FieldError(field, FieldError.Malformed));
        }
    }

    /// <summary>event-conventions EV-1: <c>&lt;ProducerModule&gt;.&lt;EventName&gt;</c>, both PascalCase.</summary>
    public static void EventType(string? value, string field, List<FieldError> errors)
    {
        RequestValidation.RequireText(value, field, CodeLength, errors);
        if (!string.IsNullOrWhiteSpace(value) && value.Length <= CodeLength && !EventTypeShape().IsMatch(value))
        {
            errors.Add(new FieldError(field, FieldError.Malformed));
        }
    }

    /// <summary>Subjects are optional here (SMS has none); a body is required in both languages.</summary>
    public static void Texts(string? subjectAr, string? subjectEn, string? bodyAr, string? bodyEn, List<FieldError> errors)
    {
        RequestValidation.Optional(subjectAr, "subjectAr", SubjectLength, errors);
        RequestValidation.Optional(subjectEn, "subjectEn", SubjectLength, errors);
        RequestValidation.RequireText(bodyAr, "bodyAr", BodyLength, errors);
        RequestValidation.RequireText(bodyEn, "bodyEn", BodyLength, errors);
    }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*\\.[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex EventTypeShape();
}
