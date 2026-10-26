using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// ADM-004 Create User. An internal user is linked to the directory by <see cref="DirectorySubjectId"/>, which sign-in
/// matches (ADR-007); an external user belongs to <see cref="ExternalEntityId"/> (ADR-013). <see cref="PreferredLanguage"/>
/// defaults to <c>ar</c>.
/// </summary>
public sealed record UserCreateRequest(
    UserType? UserType,
    string? Username,
    string? DisplayName,
    string? Email,
    string? MobileNumber,
    string? PreferredLanguage,
    string? DirectorySubjectId,
    string? JobTitle,
    Guid? ExternalEntityId)
{
    internal List<FieldError> Validate(out UserDraft? draft)
    {
        List<FieldError> errors = [];
        if (UserType is null)
        {
            errors.Add(new FieldError("userType", FieldError.Required));
        }

        UserRequestValidation.Common(Username, DisplayName, Email, MobileNumber, DirectorySubjectId, JobTitle, errors);
        Domain.Common.Language language = RequestValidation.Language(PreferredLanguage, "preferredLanguage", Domain.Common.Language.Ar, errors);

        if (UserType == Domain.IdentityAccess.UserType.Internal && DirectorySubjectId is null)
        {
            errors.Add(new FieldError("directorySubjectId", FieldError.Required));
        }

        if (UserType == Domain.IdentityAccess.UserType.External)
        {
            RequestValidation.RequireId(ExternalEntityId, "externalEntityId", errors);
        }
        else if (ExternalEntityId is not null)
        {
            errors.Add(new FieldError("externalEntityId", FieldError.NotAllowed));
        }

        draft = errors.Count > 0
            ? null
            : new UserDraft(UserType!.Value, Username!, DisplayName!, Email!, MobileNumber, language, DirectorySubjectId, JobTitle, ExternalEntityId);
        return errors;
    }
}
