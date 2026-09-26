using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>The directory subject the identity provider vouched for, or why there is none.</summary>
public sealed record SingleSignOnResult(string? SubjectId, AuthenticationFailure? Failure)
{
    public static SingleSignOnResult Authenticated(string subjectId) => new(subjectId, null);

    public static SingleSignOnResult Failed(AuthenticationFailure failure) => new(null, failure);
}
