using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// TASK-028. The directory or identity provider proves who the person is; the platform decides whether they may hold a
/// session and with which roles (ADR-007: roles are assigned in the platform, never inherited from directory groups).
/// </summary>
internal sealed partial class AuthenticationService(
    IDirectoryService directory,
    ISingleSignOnProvider singleSignOn,
    IUserAccessRepository users,
    ISessionTokenService tokens,
    TimeProvider timeProvider,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    /// <summary>
    /// Every rejected password sign-in answers no sooner than this after it began. Unknown user, wrong password and
    /// no platform account take different paths through the directory; without a common floor the response time
    /// would say which one it was.
    /// </summary>
    internal static readonly TimeSpan RejectionResponseFloor = TimeSpan.FromSeconds(1);

    public async Task<AuthenticationResult> SignInWithPasswordAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (!directory.IsConfigured)
        {
            return AuthenticationResult.Failed(AuthenticationFailure.NotConfigured);
        }

        long startedAt = timeProvider.GetTimestamp();

        // An empty password is an anonymous bind, which many directories accept: it must never reach the directory.
        AuthenticationResult result = string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)
            ? AuthenticationResult.Failed(AuthenticationFailure.Rejected)
            : await AuthenticateWithDirectoryAsync(username, password, cancellationToken).ConfigureAwait(false);

        if (result.Failure == AuthenticationFailure.Rejected)
        {
            TimeSpan remaining = RejectionResponseFloor - timeProvider.GetElapsedTime(startedAt);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        return result;
    }

    public Task<SsoAuthorizationResult> BeginSsoSignInAsync(CancellationToken cancellationToken) =>
        singleSignOn.IsConfigured
            ? singleSignOn.BeginAsync(cancellationToken)
            : Task.FromResult(new SsoAuthorizationResult(null, null, AuthenticationFailure.NotConfigured));

    public async Task<AuthenticationResult> CompleteSsoSignInAsync(string code, string state, string transaction, CancellationToken cancellationToken)
    {
        if (!singleSignOn.IsConfigured)
        {
            return AuthenticationResult.Failed(AuthenticationFailure.NotConfigured);
        }

        SingleSignOnResult result = await singleSignOn.CompleteAsync(code, state, transaction, cancellationToken).ConfigureAwait(false);
        return result.SubjectId is null
            ? AuthenticationResult.Failed(result.Failure ?? AuthenticationFailure.Rejected)
            : await StartSessionAsync(result.SubjectId, AuthenticationMethod.SingleSignOn, knownEntry: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        SessionContinuation? continuation = await tokens.ReadRefreshTokenAsync(refreshToken).ConfigureAwait(false);
        if (continuation is null)
        {
            return AuthenticationResult.Failed(AuthenticationFailure.Rejected);
        }

        // Re-read on every refresh: a disabled account or a suspended entity ends the session here, and an ended
        // assignment's role is not in the next token pair.
        UserAccess? access = await users.FindByIdAsync(continuation.UserId, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.MaySignIn)
        {
            LogRefreshRejected(logger, continuation.UserId);
            return AuthenticationResult.Failed(AuthenticationFailure.Rejected);
        }

        return Issue(access, continuation.Method, continuation);
    }

    private async Task<AuthenticationResult> AuthenticateWithDirectoryAsync(string username, string password, CancellationToken cancellationToken)
    {
        DirectoryResult result = await directory.AuthenticateAsync(username, password, cancellationToken).ConfigureAwait(false);
        return result.Entry is null
            ? AuthenticationResult.Failed(result.Failure ?? AuthenticationFailure.Rejected)
            : await StartSessionAsync(result.Entry.SubjectId, AuthenticationMethod.Directory, result.Entry, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AuthenticationResult> StartSessionAsync(
        string subjectId, AuthenticationMethod method, DirectoryEntry? knownEntry, CancellationToken cancellationToken)
    {
        UserAccess? access = await users.FindByDirectorySubjectAsync(subjectId, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.MaySignIn)
        {
            // The directory knows the person; the platform either does not, or does not let them in. Same answer.
            LogSignInRejected(logger, method, access?.UserId);
            return AuthenticationResult.Failed(AuthenticationFailure.Rejected);
        }

        if (access.UserType == UserType.Internal)
        {
            await RefreshDirectoryAttributesAsync(access.UserId, subjectId, knownEntry, cancellationToken).ConfigureAwait(false);
        }

        return Issue(access, method, continuation: null);
    }

    /// <summary>
    /// ADR-007: the directory is authoritative for department, manager and job title, so each sign-in copies them. An
    /// unreachable directory does not block an SSO sign-in; the stored values stand until the next one.
    /// </summary>
    private async Task RefreshDirectoryAttributesAsync(Guid userId, string subjectId, DirectoryEntry? knownEntry, CancellationToken cancellationToken)
    {
        DirectoryEntry? entry = knownEntry;
        if (entry is null && directory.IsConfigured)
        {
            DirectoryResult lookup = await directory.FindBySubjectAsync(subjectId, cancellationToken).ConfigureAwait(false);
            entry = lookup.Entry;
            if (entry is null)
            {
                LogDirectoryAttributesNotRefreshed(logger, userId, lookup.Failure);
            }
        }

        if (entry is not null)
        {
            await users.ApplyDirectoryAttributesAsync(userId, entry, cancellationToken).ConfigureAwait(false);
        }
    }

    private AuthenticationResult Issue(UserAccess access, AuthenticationMethod method, SessionContinuation? continuation)
    {
        IReadOnlyList<string> roleCodes = [.. access.RoleAssignments.Select(a => a.RoleCode).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        SessionTokens issued = tokens.Issue(new SessionTokenSubject(access.UserId, access.UserType, method, roleCodes), continuation);

        return AuthenticationResult.Succeeded(new PlatformSession(
            issued.AccessToken,
            issued.AccessTokenExpiresAt,
            issued.RefreshToken,
            issued.RefreshTokenExpiresAt,
            issued.SessionExpiresAt,
            new SessionUser(access.UserId, access.UserType, access.Username, access.DisplayName, access.PreferredLanguage, method, access.RoleAssignments)));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in by {Method} rejected: the directory subject has no platform user who may sign in (user {UserId}).")]
    private static partial void LogSignInRejected(ILogger logger, AuthenticationMethod method, Guid? userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session refresh rejected for user {UserId}: the user may no longer sign in.")]
    private static partial void LogRefreshRejected(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory attributes of user {UserId} not refreshed at sign-in: {Failure}.")]
    private static partial void LogDirectoryAttributesNotRefreshed(ILogger logger, Guid userId, AuthenticationFailure? failure);
}
