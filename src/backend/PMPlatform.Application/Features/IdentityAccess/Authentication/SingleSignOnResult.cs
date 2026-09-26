using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// The directory subject the identity provider vouched for, or why there is none. <see cref="AuthenticationMethods"/>
/// and <see cref="AuthenticatedAt"/> are the ID token's <c>amr</c> and <c>auth_time</c>, when it carries them.
/// </summary>
public sealed record SingleSignOnResult(string? SubjectId, IReadOnlyList<string> AuthenticationMethods, DateTimeOffset? AuthenticatedAt, AuthenticationFailure? Failure)
{
    public static SingleSignOnResult Authenticated(string subjectId, IReadOnlyList<string> authenticationMethods, DateTimeOffset? authenticatedAt) =>
        new(subjectId, authenticationMethods, authenticatedAt, null);

    public static SingleSignOnResult Failed(AuthenticationFailure failure) => new(null, [], null, failure);
}
