using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>The fields ADM-004 and ADM-005 share, with the lengths of <c>identity_access.user</c>.</summary>
internal static class UserRequestValidation
{
    public static void Common(
        string? username, string? displayName, string? email, string? mobileNumber, string? directorySubjectId, string? jobTitle, List<FieldError> errors)
    {
        RequestValidation.RequireText(username, "username", 100, errors);
        RequestValidation.RequireText(displayName, "displayName", RequestValidation.NameLength, errors);
        RequestValidation.Email(email, "email", errors);
        RequestValidation.MobileNumber(mobileNumber, "mobileNumber", errors);
        RequestValidation.Optional(directorySubjectId, "directorySubjectId", 200, errors);
        RequestValidation.Optional(jobTitle, "jobTitle", RequestValidation.NameLength, errors);
    }
}
