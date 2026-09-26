using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// TASK-028. The directory or identity provider proves who the person is; the platform decides whether they may hold a
/// session and with which roles (ADR-007: roles are assigned in the platform, never inherited from directory groups).
/// TASK-029. A person who requires MFA gets an MFA token instead of a session, and a session only once the MFA provider
/// has verified their second factor. Every session is issued by <see cref="Issue"/>, and every path into it for a
/// person who requires MFA passes <see cref="VerifySecondFactorAsync"/> first, or carries a verified factor forward.
/// </summary>
internal sealed partial class AuthenticationService(
    IDirectoryService directory,
    ISingleSignOnProvider singleSignOn,
    IMultiFactorProvider multiFactor,
    IUserAccessRepository users,
    ISessionTokenService tokens,
    MultiFactorPolicy multiFactorPolicy,
    TimeProvider timeProvider,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    /// <summary>
    /// Every rejected password sign-in or second factor answers no sooner than this after it began. Unknown user, wrong
    /// password and no platform account take different paths through the directory; without a common floor the
    /// response time would say which one it was. For a second factor it also slows code guessing (F-4).
    /// </summary>
    internal static readonly TimeSpan RejectionResponseFloor = TimeSpan.FromSeconds(1);

    public async Task<AuthenticationResult> SignInWithPasswordAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (!directory.IsConfigured)
        {
            return AuthenticationResult.Failed(AuthenticationFailure.NotConfigured);
        }

        // An empty password is an anonymous bind, which many directories accept: it must never reach the directory.
        return await WithRejectionFloorAsync(
                () => string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)
                    ? Task.FromResult(AuthenticationResult.Failed(AuthenticationFailure.Rejected))
                    : AuthenticateWithDirectoryAsync(username, password, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
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
            : await StartSessionAsync(result.SubjectId, AuthenticationMethod.SingleSignOn, knownEntry: null, IdentityProviderMultiFactor(result), cancellationToken)
                .ConfigureAwait(false);
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

        // A role that requires MFA, assigned after this session began without it, is not handed over by a refresh.
        if (RequiresMultiFactor(access) && !continuation.Authentication.MultiFactor)
        {
            LogRefreshWithoutMultiFactor(logger, access.UserId);
            return AuthenticationResult.Failed(AuthenticationFailure.Rejected);
        }

        return Issue(access, continuation.Authentication, continuation);
    }

    public async Task<MultiFactorChallengeResult> BeginMultiFactorSignInAsync(string mfaToken, CancellationToken cancellationToken)
    {
        PendingSignIn? pending = await tokens.ReadMultiFactorTokenAsync(mfaToken).ConfigureAwait(false);
        return pending is null
            ? MultiFactorChallengeResult.Failed(AuthenticationFailure.Rejected)
            : await BeginChallengeAsync(await users.FindByIdAsync(pending.UserId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    public Task<AuthenticationResult> CompleteMultiFactorSignInAsync(string mfaToken, string challengeId, string code, CancellationToken cancellationToken) =>
        WithRejectionFloorAsync(
            async () =>
            {
                PendingSignIn? pending = await tokens.ReadMultiFactorTokenAsync(mfaToken).ConfigureAwait(false);
                if (pending is null)
                {
                    return AuthenticationResult.Failed(AuthenticationFailure.Rejected);
                }

                UserAccess? access = await users.FindByIdAsync(pending.UserId, cancellationToken).ConfigureAwait(false);
                return await VerifySecondFactorAsync(access, challengeId, code, cancellationToken).ConfigureAwait(false) is { } failure
                    ? AuthenticationResult.Failed(failure)
                    : Issue(access!, new SessionAuthentication(pending.Method, MultiFactor: true, Now()), continuation: null);
            },
            cancellationToken);

    public async Task<MultiFactorChallengeResult> BeginStepUpAsync(string refreshToken, CancellationToken cancellationToken)
    {
        SessionContinuation? continuation = await tokens.ReadRefreshTokenAsync(refreshToken).ConfigureAwait(false);
        return continuation is null
            ? MultiFactorChallengeResult.Failed(AuthenticationFailure.Rejected)
            : await BeginChallengeAsync(await users.FindByIdAsync(continuation.UserId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    public Task<AuthenticationResult> CompleteStepUpAsync(string refreshToken, string challengeId, string code, CancellationToken cancellationToken) =>
        WithRejectionFloorAsync(
            async () =>
            {
                SessionContinuation? continuation = await tokens.ReadRefreshTokenAsync(refreshToken).ConfigureAwait(false);
                if (continuation is null)
                {
                    return AuthenticationResult.Failed(AuthenticationFailure.Rejected);
                }

                UserAccess? access = await users.FindByIdAsync(continuation.UserId, cancellationToken).ConfigureAwait(false);
                return await VerifySecondFactorAsync(access, challengeId, code, cancellationToken).ConfigureAwait(false) is { } failure
                    ? AuthenticationResult.Failed(failure)
                    : Issue(access!, new SessionAuthentication(continuation.Authentication.Method, MultiFactor: true, Now()), continuation);
            },
            cancellationToken);

    private async Task<AuthenticationResult> AuthenticateWithDirectoryAsync(string username, string password, CancellationToken cancellationToken)
    {
        DirectoryResult result = await directory.AuthenticateAsync(username, password, cancellationToken).ConfigureAwait(false);
        return result.Entry is null
            ? AuthenticationResult.Failed(result.Failure ?? AuthenticationFailure.Rejected)
            : await StartSessionAsync(result.Entry.SubjectId, AuthenticationMethod.Directory, result.Entry, verifiedByIdentityProvider: null, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Admits the person the directory or identity provider vouched for. <paramref name="verifiedByIdentityProvider"/> is
    /// set when the identity provider's own second factor is accepted (<see cref="MultiFactorPolicy.IdentityProviderMethods"/>).
    /// </summary>
    private async Task<AuthenticationResult> StartSessionAsync(
        string subjectId, AuthenticationMethod method, DirectoryEntry? knownEntry, SessionAuthentication? verifiedByIdentityProvider, CancellationToken cancellationToken)
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

        if (verifiedByIdentityProvider is not null)
        {
            return Issue(access, verifiedByIdentityProvider, continuation: null);
        }

        if (!RequiresMultiFactor(access))
        {
            return Issue(access, new SessionAuthentication(method, MultiFactor: false, Now()), continuation: null);
        }

        // CTL-07: no session yet. The MFA token admits the person to the second factor and to nothing else.
        if (!multiFactor.IsConfigured)
        {
            LogMultiFactorNotConfigured(logger, access.UserId);
            return AuthenticationResult.Failed(AuthenticationFailure.NotConfigured);
        }

        if (PurposeFor(access) is null)
        {
            LogEnrolmentNotAllowed(logger, access.UserId);
            return AuthenticationResult.Failed(AuthenticationFailure.Rejected);
        }

        return AuthenticationResult.MultiFactorRequired(
            tokens.IssueMultiFactorToken(new PendingSignIn(access.UserId, method), enrolmentRequired: !access.MultiFactorEnrolled));
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

    private async Task<MultiFactorChallengeResult> BeginChallengeAsync(UserAccess? access, CancellationToken cancellationToken) =>
        access is null || !access.MaySignIn ? MultiFactorChallengeResult.Failed(AuthenticationFailure.Rejected)
        : !multiFactor.IsConfigured ? MultiFactorChallengeResult.Failed(AuthenticationFailure.NotConfigured)
        : PurposeFor(access) is { } purpose ? await multiFactor.StartAsync(access.UserId, purpose, cancellationToken).ConfigureAwait(false)
        : MultiFactorChallengeResult.Failed(AuthenticationFailure.Rejected);

    /// <summary>Null when the second factor passed; otherwise why not. An enrolment that passes is recorded on the user.</summary>
    private async Task<AuthenticationFailure?> VerifySecondFactorAsync(UserAccess? access, string challengeId, string code, CancellationToken cancellationToken)
    {
        // Re-read, not taken from the token: a user disabled since the first factor gets no session.
        if (access is null || !access.MaySignIn)
        {
            return AuthenticationFailure.Rejected;
        }

        if (!multiFactor.IsConfigured)
        {
            return AuthenticationFailure.NotConfigured;
        }

        // The purpose comes from the user's stored enrolment, never from the client: an enrolled user cannot be talked
        // into enrolling a second, attacker-held factor.
        if (PurposeFor(access) is not { } purpose)
        {
            return AuthenticationFailure.Rejected;
        }

        MultiFactorVerification verification = await multiFactor.CompleteAsync(access.UserId, purpose, challengeId, code, cancellationToken).ConfigureAwait(false);
        if (!verification.Verified)
        {
            LogSecondFactorRejected(logger, access.UserId, purpose);
            return verification.Failure ?? AuthenticationFailure.Rejected;
        }

        if (purpose == MultiFactorPurpose.Enrolment)
        {
            await users.RecordMultiFactorEnrolmentAsync(access.UserId, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>Verification if the user has a factor; enrolment if they have none and the policy allows it there; else none.</summary>
    private MultiFactorPurpose? PurposeFor(UserAccess access) =>
        access.MultiFactorEnrolled ? MultiFactorPurpose.Verification
        : multiFactorPolicy.AllowEnrolmentAtSignIn ? MultiFactorPurpose.Enrolment
        : null;

    /// <summary>A role on the policy's list requires MFA; so does an enrolment already made, so it can never be skipped later.</summary>
    private bool RequiresMultiFactor(UserAccess access) =>
        access.MultiFactorEnrolled || multiFactorPolicy.RequiresMultiFactor(access.RoleAssignments.Select(a => a.RoleCode));

    /// <summary>
    /// The identity provider's second factor, when the policy trusts the <c>amr</c> value it reports and the ID token
    /// says when the person authenticated. That time, not the sign-in's, is what step-up freshness is measured from.
    /// </summary>
    private SessionAuthentication? IdentityProviderMultiFactor(SingleSignOnResult result)
    {
        if (result.AuthenticatedAt is not { } authenticatedAt
            || !result.AuthenticationMethods.Any(m => multiFactorPolicy.IdentityProviderMethods.Contains(m, StringComparer.Ordinal)))
        {
            return null;
        }

        DateTimeOffset now = Now();
        return new SessionAuthentication(AuthenticationMethod.SingleSignOn, MultiFactor: true, authenticatedAt < now ? authenticatedAt : now);
    }

    private async Task<AuthenticationResult> WithRejectionFloorAsync(Func<Task<AuthenticationResult>> attempt, CancellationToken cancellationToken)
    {
        long startedAt = timeProvider.GetTimestamp();
        AuthenticationResult result = await attempt().ConfigureAwait(false);

        if (result.Failure == AuthenticationFailure.Rejected)
        {
            // A timer can fire a fraction of a millisecond before the high-resolution clock reaches the floor, so the
            // floor is re-checked after each wait rather than trusted to one delay.
            TimeSpan remaining;
            while ((remaining = RejectionResponseFloor - timeProvider.GetElapsedTime(startedAt)) > TimeSpan.Zero)
            {
                await Task.Delay(remaining, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        return result;
    }

    private AuthenticationResult Issue(UserAccess access, SessionAuthentication authentication, SessionContinuation? continuation)
    {
        IReadOnlyList<string> roleCodes = [.. access.RoleAssignments.Select(a => a.RoleCode).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        SessionTokens issued = tokens.Issue(new SessionTokenSubject(access.UserId, access.UserType, authentication, roleCodes), continuation);

        return AuthenticationResult.Succeeded(new PlatformSession(
            issued.AccessToken,
            issued.AccessTokenExpiresAt,
            issued.RefreshToken,
            issued.RefreshTokenExpiresAt,
            issued.SessionExpiresAt,
            new SessionUser(
                access.UserId,
                access.UserType,
                access.Username,
                access.DisplayName,
                access.PreferredLanguage,
                authentication.Method,
                authentication.MultiFactor,
                authentication.AuthenticatedAt,
                access.RoleAssignments)));
    }

    /// <summary>Whole seconds, as a token's <c>auth_time</c> carries them, so the time reported with a session is the one in its tokens.</summary>
    private DateTimeOffset Now() => DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in by {Method} rejected: the directory subject has no platform user who may sign in (user {UserId}).")]
    private static partial void LogSignInRejected(ILogger logger, AuthenticationMethod method, Guid? userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session refresh rejected for user {UserId}: the user may no longer sign in.")]
    private static partial void LogRefreshRejected(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session refresh rejected for user {UserId}: the user now requires MFA and the session never passed it.")]
    private static partial void LogRefreshWithoutMultiFactor(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory attributes of user {UserId} not refreshed at sign-in: {Failure}.")]
    private static partial void LogDirectoryAttributesNotRefreshed(ILogger logger, Guid userId, AuthenticationFailure? failure);

    [LoggerMessage(Level = LogLevel.Error, Message = "User {UserId} requires MFA and no MFA provider is configured; no session is issued.")]
    private static partial void LogMultiFactorNotConfigured(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} requires MFA, has no enrolled factor, and enrolment at sign-in is not allowed; no session is issued.")]
    private static partial void LogEnrolmentNotAllowed(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Second factor ({Purpose}) rejected for user {UserId}.")]
    private static partial void LogSecondFactorRejected(ILogger logger, Guid userId, MultiFactorPurpose purpose);
}
