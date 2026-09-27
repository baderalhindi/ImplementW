using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>ADM-005 Edit User: the editable representation in full (R-5); user type and entity are fixed at creation.</summary>
public sealed record UserUpdateRequest(
    string? Username,
    string? DisplayName,
    string? Email,
    string? MobileNumber,
    string? PreferredLanguage,
    string? DirectorySubjectId,
    string? JobTitle)
{
    internal List<FieldError> Validate(out UserChanges? changes)
    {
        List<FieldError> errors = [];
        UserRequestValidation.Common(Username, DisplayName, Email, MobileNumber, DirectorySubjectId, JobTitle, errors);
        Domain.Common.Language language = RequestValidation.Language(PreferredLanguage, "preferredLanguage", fallback: null, errors);

        changes = errors.Count > 0 ? null : new UserChanges(Username!, DisplayName!, Email!, MobileNumber, language, DirectorySubjectId, JobTitle);
        return errors;
    }
}
