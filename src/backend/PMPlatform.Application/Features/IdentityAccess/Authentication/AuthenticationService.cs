using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// TASK-028. The directory or identity provider proves who the person is; the platform decides whether they may hold a
/// session and with which roles (ADR-007: roles are assigned in the platform, never inherited from directory groups).
/// TASK-029. A person who requires MFA gets an MFA token instead of a session, and a session only once the MFA provider
/// has verified their second factor. Every session is issued by <see cref="Issue"/>, and every path into it for a
/// person who requires MFA passes <see cref="VerifySecondFactorAsync"/> first, or carries a verified factor forward.
/// TASK-033. Every attempt ends in <see cref="SucceedAsync"/> or <see cref="FailAsync"/>, which record its audit event
/// (CTL-25) before the caller is answered; the event says why an attempt failed, the caller never learns it.
/// TASK-068. The last gate before a new session is <see cref="AdmitAsync"/>: an external user whose identity Nafath has
/// not verified gets a verification token instead (ADR-007, ADR-013). Nafath is called only from
/// <see cref="BeginIdentityVerificationAsync"/> and <see cref="CompleteIdentityVerificationAsync"/>, for such a user.
/// </summary>
internal sealed partial class AuthenticationService(
    IDirectoryService directory,
    ISingleSignOnProvider singleSignOn,
    IMultiFactorProvider multiFactor,
    IIdentityVerificationProvider identityVerification,
    IUserAccessRepository users,
    ISessionTokenService tokens,
    MultiFactorPolicy multiFactorPolicy,
    IdentityVerificationPolicy identityVerificationPolicy,
    IAuditTrail audit,
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
        Attempt attempt = Attempt.SignIn(AuthenticationMethod.Directory, username);
        if (!directory.IsConfigured)
        {
            return await FailAsync(attempt, AuthenticationFailureReason.NotConfigured, userId: null).ConfigureAwait(false);
        }

        // An empty password is an anonymous bind, which many directories accept: it must never reach the directory.
        return await WithRejectionFloorAsync(
                () => string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)
                    ? FailAsync(attempt, AuthenticationFailureReason.CredentialsRejected, userId: null)
                    : AuthenticateWithDirectoryAsync(attempt, username, password, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<SsoAuthorizationResult> BeginSsoSignInAsync(CancellationToken cancellationToken) =>
        singleSignOn.IsConfigured
            ? singleSignOn.BeginAsync(cancellationToken)
            : Task.FromResult(new SsoAuthorizationResult(null, null, AuthenticationFailure.NotConfigured));

    public async Task<AuthenticationResult> CompleteSsoSignInAsync(string code, string state, string transaction, CancellationToken cancellationToken)
    {
        Attempt attempt = Attempt.SignIn(AuthenticationMethod.SingleSignOn);
        if (!singleSignOn.IsConfigured)
        {
            return await FailAsync(attempt, AuthenticationFailureReason.NotConfigured, userId: null).ConfigureAwait(false);
        }

        SingleSignOnResult result = await singleSignOn.CompleteAsync(code, state, transaction, cancellationToken).ConfigureAwait(false);
        return result.SubjectId is null
            ? await FailAsync(attempt, ReasonOf(result.Failure, AuthenticationFailureReason.CredentialsRejected), userId: null).ConfigureAwait(false)
            : await StartSessionAsync(attempt, result.SubjectId, knownEntry: null, IdentityProviderMultiFactor(result), cancellationToken).ConfigureAwait(false);
    }

    public async Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        SessionContinuation? continuation = await tokens.ReadRefreshTokenAsync(refreshToken).ConfigureAwait(false);
        if (continuation is null)
        {
            return await FailAsync(Attempt.Refresh(method: null), AuthenticationFailureReason.TokenInvalid, userId: null).ConfigureAwait(false);
        }

        // Re-read on every refresh: a disabled account or a suspended entity ends the session here, and an ended
        // assignment's role is not in the next token pair.
        Attempt attempt = Attempt.Refresh(continuation.Authentication.Method);
        UserAccess? access = await users.FindByIdAsync(continuation.UserId, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.MaySignIn)
        {
            LogRefreshRejected(logger, continuation.UserId);
            return await FailAsync(attempt, AuthenticationFailureReason.AccountInactive, continuation.UserId).ConfigureAwait(false);
        }

        // A role that requires MFA, assigned after this session began without it, is not handed over by a refresh.
        if (RequiresMultiFactor(access) && !continuation.Authentication.MultiFactor)
        {
            LogRefreshWithoutMultiFactor(logger, access.UserId);
            return await FailAsync(attempt, AuthenticationFailureReason.MultiFactorMissing, access.UserId).ConfigureAwait(false);
        }

        // Nor does a session that began before the verification applied to this user: they sign in again and are verified.
        if (RequiresIdentityVerification(access))
        {
            LogRefreshWithoutIdentityVerification(logger, access.UserId);
            return await FailAsync(attempt, AuthenticationFailureReason.IdentityVerificationMissing, access.UserId).ConfigureAwait(false);
        }

        return await SucceedAsync(attempt, access, continuation.Authentication, continuation).ConfigureAwait(false);
    }

    public async Task<MultiFactorChallengeResult> BeginMultiFactorSignInAsync(string mfaToken, CancellationToken cancellationToken)
    {
        PendingSignIn? pending = await tokens.ReadMultiFactorTokenAsync(mfaToken).ConfigureAwait(false);
        return pending is null
            ? await FailChallengeAsync(Attempt.SignIn(method: null), AuthenticationFailureReason.TokenInvalid, userId: null).ConfigureAwait(false)
            : await BeginChallengeAsync(Attempt.SignIn(pending.Method), pending.UserId, cancellationToken).ConfigureAwait(false);
    }

    public Task<AuthenticationResult> CompleteMultiFactorSignInAsync(string mfaToken, string challengeId, string code, CancellationToken cancellationToken) =>
        WithRejectionFloorAsync(
            async () =>
            {
                PendingSignIn? pending = await tokens.ReadMultiFactorTokenAsync(mfaToken).ConfigureAwait(false);
                if (pending is null)
                {
                    return await FailAsync(Attempt.SignIn(method: null), AuthenticationFailureReason.TokenInvalid, userId: null).ConfigureAwait(false);
                }

                Attempt attempt = Attempt.SignIn(pending.Method);
                UserAccess? access = await users.FindByIdAsync(pending.UserId, cancellationToken).ConfigureAwait(false);
                return await VerifySecondFactorAsync(access, challengeId, code, cancellationToken).ConfigureAwait(false) is { } reason
                    ? await FailAsync(attempt, reason, pending.UserId).ConfigureAwait(false)
                    : await AdmitAsync(attempt, access!, new SessionAuthentication(pending.Method, MultiFactor: true, Now())).ConfigureAwait(false);
            },
            cancellationToken);

    public async Task<MultiFactorChallengeResult> BeginStepUpAsync(string refreshToken, CancellationToken cancellationToken)
    {
        SessionContinuation? continuation = await tokens.ReadRefreshTokenAsync(refreshToken).ConfigureAwait(false);
        return continuation is null
            ? await FailChallengeAsync(Attempt.StepUp(method: null), AuthenticationFailureReason.TokenInvalid, userId: null).ConfigureAwait(false)
            : await BeginChallengeAsync(Attempt.StepUp(continuation.Authentication.Method), continuation.UserId, cancellationToken).ConfigureAwait(false);
    }

    public Task<AuthenticationResult> CompleteStepUpAsync(string refreshToken, string challengeId, string code, CancellationToken cancellationToken) =>
        WithRejectionFloorAsync(
            async () =>
            {
                SessionContinuation? continuation = await tokens.ReadRefreshTokenAsync(refreshToken).ConfigureAwait(false);
                if (continuation is null)
                {
                    return await FailAsync(Attempt.StepUp(method: null), AuthenticationFailureReason.TokenInvalid, userId: null).ConfigureAwait(false);
                }

                Attempt attempt = Attempt.StepUp(continuation.Authentication.Method);
                UserAccess? access = await users.FindByIdAsync(continuation.UserId, cancellationToken).ConfigureAwait(false);
                // As the refresh: a session that skipped a verification that now applies is not continued.
                AuthenticationFailureReason? refusal = access is not null && RequiresIdentityVerification(access)
                    ? AuthenticationFailureReason.IdentityVerificationMissing
                    : await VerifySecondFactorAsync(access, challengeId, code, cancellationToken).ConfigureAwait(false);
                return refusal is { } reason
                    ? await FailAsync(attempt, reason, continuation.UserId).ConfigureAwait(false)
                    : await SucceedAsync(
                            attempt, access!, new SessionAuthentication(continuation.Authentication.Method, MultiFactor: true, Now()), continuation)
                        .ConfigureAwait(false);
            },
            cancellationToken);

    public async Task<IdentityVerificationAuthorization> BeginIdentityVerificationAsync(string verificationToken, CancellationToken cancellationToken)
    {
        PendingIdentityVerification? pending = await tokens.ReadIdentityVerificationTokenAsync(verificationToken).ConfigureAwait(false);
        Attempt attempt = Attempt.IdentityVerification(pending?.Authentication.Method);
        (UserAccess? access, AuthenticationFailureReason? refusal) = await VerificationCandidateAsync(pending, cancellationToken).ConfigureAwait(false);
        if (refusal is { } reason)
        {
            await audit.RecordAsync(FailureEntry(attempt, reason, pending?.UserId)).ConfigureAwait(false);
            return IdentityVerificationAuthorization.Failed(FailureOf(reason));
        }

        IdentityVerificationAuthorization started = await identityVerification.BeginAsync(access!.UserId, cancellationToken).ConfigureAwait(false);
        if (started.Failure is { } failure)
        {
            await audit.RecordAsync(FailureEntry(attempt, ReasonOf(failure, AuthenticationFailureReason.IdentityNotVerified), access.UserId)).ConfigureAwait(false);
            return IdentityVerificationAuthorization.Failed(failure);
        }

        return started;
    }

    public async Task<AuthenticationResult> CompleteIdentityVerificationAsync(
        string verificationToken, string code, string state, string transaction, CancellationToken cancellationToken)
    {
        PendingIdentityVerification? pending = await tokens.ReadIdentityVerificationTokenAsync(verificationToken).ConfigureAwait(false);
        Attempt attempt = Attempt.IdentityVerification(pending?.Authentication.Method);
        (UserAccess? access, AuthenticationFailureReason? refusal) = await VerificationCandidateAsync(pending, cancellationToken).ConfigureAwait(false);
        if (refusal is { } reason)
        {
            return await FailAsync(attempt, reason, pending?.UserId).ConfigureAwait(false);
        }

        IdentityVerificationResult result = await identityVerification.CompleteAsync(access!.UserId, code, state, transaction, cancellationToken).ConfigureAwait(false);
        if (result.Reference is not { } reference)
        {
            LogIdentityNotVerified(logger, access.UserId, result.Failure);
            return await FailAsync(attempt, ReasonOf(result.Failure, AuthenticationFailureReason.IdentityNotVerified), access.UserId).ConfigureAwait(false);
        }

        // Staged, so the verification and its audit event are committed together. The reference is all that is kept (OQ-007).
        AuditEntry verified = Entry(attempt, IdentityAccessAuditEvents.IdentityVerified, AuditOutcome.Success, access.UserId, access.UserId);
        audit.Stage(verified with { Attributes = [.. verified.Attributes, AuditAttribute.Of(IdentityAccessAuditAttributes.VerificationReference, reference)] });
        await users.RecordIdentityVerificationAsync(access.UserId, reference, cancellationToken).ConfigureAwait(false);
        return await SucceedAsync(Attempt.SignIn(pending!.Authentication.Method), access, pending.Authentication, continuation: null).ConfigureAwait(false);
    }

    private async Task<AuthenticationResult> AuthenticateWithDirectoryAsync(Attempt attempt, string username, string password, CancellationToken cancellationToken)
    {
        DirectoryResult result = await directory.AuthenticateAsync(username, password, cancellationToken).ConfigureAwait(false);
        return result.Entry is null
            ? await FailAsync(attempt, ReasonOf(result.Failure, AuthenticationFailureReason.CredentialsRejected), userId: null).ConfigureAwait(false)
            : await StartSessionAsync(attempt, result.Entry.SubjectId, result.Entry, verifiedByIdentityProvider: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Admits the person the directory or identity provider vouched for. <paramref name="verifiedByIdentityProvider"/> is
    /// set when the identity provider's own second factor is accepted (<see cref="MultiFactorPolicy.IdentityProviderMethods"/>).
    /// </summary>
    private async Task<AuthenticationResult> StartSessionAsync(
        Attempt attempt, string subjectId, DirectoryEntry? knownEntry, SessionAuthentication? verifiedByIdentityProvider, CancellationToken cancellationToken)
    {
        AuthenticationMethod method = attempt.Method!.Value;
        UserAccess? access = await users.FindByDirectorySubjectAsync(subjectId, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.MaySignIn)
        {
            // The directory knows the person; the platform either does not, or does not let them in. Same answer.
            LogSignInRejected(logger, method, access?.UserId);
            return await FailAsync(
                    attempt, access is null ? AuthenticationFailureReason.NoPlatformAccount : AuthenticationFailureReason.AccountInactive, access?.UserId)
                .ConfigureAwait(false);
        }

        if (access.UserType == UserType.Internal)
        {
            await RefreshDirectoryAttributesAsync(access.UserId, subjectId, knownEntry, cancellationToken).ConfigureAwait(false);
        }

        if (verifiedByIdentityProvider is not null)
        {
            return await AdmitAsync(attempt, access, verifiedByIdentityProvider).ConfigureAwait(false);
        }

        if (!RequiresMultiFactor(access))
        {
            return await AdmitAsync(attempt, access, new SessionAuthentication(method, MultiFactor: false, Now())).ConfigureAwait(false);
        }

        // CTL-07: no session yet. The MFA token admits the person to the second factor and to nothing else.
        if (!multiFactor.IsConfigured)
        {
            LogMultiFactorNotConfigured(logger, access.UserId);
            return await FailAsync(attempt, AuthenticationFailureReason.NotConfigured, access.UserId).ConfigureAwait(false);
        }

        if (PurposeFor(access) is null)
        {
            LogEnrolmentNotAllowed(logger, access.UserId);
            return await FailAsync(attempt, AuthenticationFailureReason.EnrolmentNotAllowed, access.UserId).ConfigureAwait(false);
        }

        // The first factor passed and the session waits on the second: a password that works is worth a SIEM's attention
        // even if the second factor never follows.
        await audit.RecordAsync(Entry(attempt, IdentityAccessAuditEvents.FirstFactorPassed, AuditOutcome.Success, access.UserId, access.UserId)).ConfigureAwait(false);
        return AuthenticationResult.MultiFactorRequired(
            tokens.IssueMultiFactorToken(new PendingSignIn(access.UserId, method), enrolmentRequired: !access.MultiFactorEnrolled));
    }

    /// <summary>
    /// The last gate before a new session. An external user whose identity Nafath has not verified gets a verification
    /// token instead, which admits them to the verification and to nothing else (TASK-068); everyone else gets the session.
    /// </summary>
    private async Task<AuthenticationResult> AdmitAsync(Attempt attempt, UserAccess access, SessionAuthentication authentication)
    {
        if (!RequiresIdentityVerification(access))
        {
            return await SucceedAsync(attempt, access, authentication, continuation: null).ConfigureAwait(false);
        }

        await audit.RecordAsync(Entry(attempt with { Method = authentication.Method }, IdentityAccessAuditEvents.IdentityVerificationRequired, AuditOutcome.Success,
                access.UserId, access.UserId))
            .ConfigureAwait(false);
        return AuthenticationResult.IdentityVerificationRequired(tokens.IssueIdentityVerificationToken(new PendingIdentityVerification(access.UserId, authentication)));
    }

    /// <summary>
    /// The user a verification token admits, re-read: one disabled, or whose entity was suspended, since signing in is
    /// refused, and so is one who needs no verification — internal, verified already, or the feature since turned off.
    /// Nafath is never called for any of them.
    /// </summary>
    private async Task<(UserAccess? Access, AuthenticationFailureReason? Refusal)> VerificationCandidateAsync(
        PendingIdentityVerification? pending, CancellationToken cancellationToken)
    {
        if (pending is null)
        {
            return (null, AuthenticationFailureReason.TokenInvalid);
        }

        UserAccess? access = await users.FindByIdAsync(pending.UserId, cancellationToken).ConfigureAwait(false);
        AuthenticationFailureReason? refusal = access is null || !access.MaySignIn ? AuthenticationFailureReason.AccountInactive
            : !RequiresIdentityVerification(access) ? AuthenticationFailureReason.IdentityVerificationNotRequired
            : !identityVerification.IsConfigured ? AuthenticationFailureReason.NotConfigured
            : null;
        if (refusal == AuthenticationFailureReason.NotConfigured)
        {
            LogIdentityVerificationNotConfigured(logger, pending.UserId);
        }

        return (access, refusal);
    }

    private bool RequiresIdentityVerification(UserAccess access) => identityVerificationPolicy.Requires(access.UserType, access.IdentityVerified);

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

    /// <summary>Starts the second factor for <paramref name="userId"/>; a refusal is audited, a started challenge is not an outcome yet.</summary>
    private async Task<MultiFactorChallengeResult> BeginChallengeAsync(Attempt attempt, Guid userId, CancellationToken cancellationToken)
    {
        UserAccess? access = await users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        AuthenticationFailureReason? refusal = access is null || !access.MaySignIn ? AuthenticationFailureReason.AccountInactive
            : !multiFactor.IsConfigured ? AuthenticationFailureReason.NotConfigured
            : PurposeFor(access) is null ? AuthenticationFailureReason.EnrolmentNotAllowed
            : null;
        if (refusal is { } reason)
        {
            return await FailChallengeAsync(attempt, reason, userId).ConfigureAwait(false);
        }

        MultiFactorChallengeResult challenge = await multiFactor.StartAsync(userId, PurposeFor(access!)!.Value, cancellationToken).ConfigureAwait(false);
        return challenge.Failure is { } failure
            ? await FailChallengeAsync(attempt, ReasonOf(failure, AuthenticationFailureReason.SecondFactorRejected), userId).ConfigureAwait(false)
            : challenge;
    }

    /// <summary>Null when the second factor passed; otherwise why not. An enrolment that passes is recorded on the user, and audited with it.</summary>
    private async Task<AuthenticationFailureReason?> VerifySecondFactorAsync(UserAccess? access, string challengeId, string code, CancellationToken cancellationToken)
    {
        // Re-read, not taken from the token: a user disabled since the first factor gets no session.
        if (access is null || !access.MaySignIn)
        {
            return AuthenticationFailureReason.AccountInactive;
        }

        if (!multiFactor.IsConfigured)
        {
            return AuthenticationFailureReason.NotConfigured;
        }

        // The purpose comes from the user's stored enrolment, never from the client: an enrolled user cannot be talked
        // into enrolling a second, attacker-held factor.
        if (PurposeFor(access) is not { } purpose)
        {
            return AuthenticationFailureReason.EnrolmentNotAllowed;
        }

        MultiFactorVerification verification = await multiFactor.CompleteAsync(access.UserId, purpose, challengeId, code, cancellationToken).ConfigureAwait(false);
        if (!verification.Verified)
        {
            LogSecondFactorRejected(logger, access.UserId, purpose);
            return ReasonOf(verification.Failure, AuthenticationFailureReason.SecondFactorRejected);
        }

        if (purpose == MultiFactorPurpose.Enrolment)
        {
            // Staged, so the enrolment and its audit event are committed together.
            audit.Stage(new AuditEntry(AuditEventClass.Authentication, IdentityAccessAuditEvents.MultiFactorEnrolled, AuditOutcome.Success)
            {
                ActorUserId = access.UserId,
                Subject = UserSubject(access.UserId),
            });
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

    /// <summary>Audits the attempt's success, then issues the session: a session is never handed out unaudited.</summary>
    private async Task<AuthenticationResult> SucceedAsync(Attempt attempt, UserAccess access, SessionAuthentication authentication, SessionContinuation? continuation)
    {
        AuditEntry entry = Entry(attempt with { Method = authentication.Method }, attempt.SucceededEvent, AuditOutcome.Success, access.UserId, access.UserId);
        AuditAttribute multiFactor = AuditAttribute.Of(IdentityAccessAuditAttributes.MultiFactor, authentication.MultiFactor);
        await audit.RecordAsync(entry with { Attributes = [.. entry.Attributes, multiFactor] }).ConfigureAwait(false);
        return Issue(access, authentication, continuation);
    }

    /// <summary>Audits the failed attempt and answers with the failure the caller may see.</summary>
    private async Task<AuthenticationResult> FailAsync(Attempt attempt, AuthenticationFailureReason reason, Guid? userId)
    {
        await audit.RecordAsync(FailureEntry(attempt, reason, userId)).ConfigureAwait(false);
        return AuthenticationResult.Failed(FailureOf(reason));
    }

    private async Task<MultiFactorChallengeResult> FailChallengeAsync(Attempt attempt, AuthenticationFailureReason reason, Guid? userId)
    {
        await audit.RecordAsync(FailureEntry(attempt, reason, userId)).ConfigureAwait(false);
        return MultiFactorChallengeResult.Failed(FailureOf(reason));
    }

    /// <summary>
    /// A failed attempt names the account it was made against as its subject, not as its actor: whoever made it has not
    /// proved they are that user.
    /// </summary>
    private static AuditEntry FailureEntry(Attempt attempt, AuthenticationFailureReason reason, Guid? userId)
    {
        AuditEntry entry = Entry(attempt, attempt.FailedEvent, AuditOutcome.Failed, actorUserId: null, userId);
        return entry with { Attributes = [.. entry.Attributes, AuditAttribute.Of(IdentityAccessAuditAttributes.FailureReason, reason)] };
    }

    private static AuditEntry Entry(Attempt attempt, string eventType, AuditOutcome outcome, Guid? actorUserId, Guid? subjectUserId)
    {
        List<AuditAttribute> attributes = [];
        if (attempt.Method is { } method)
        {
            attributes.Add(AuditAttribute.Of(IdentityAccessAuditAttributes.AuthenticationMethod, method));
        }

        if (attempt.Username is { Length: > 0 } username)
        {
            attributes.Add(AuditAttribute.Of(IdentityAccessAuditAttributes.Username, username.Length > MaxAuditedUsernameLength ? username[..MaxAuditedUsernameLength] : username));
        }

        return new AuditEntry(AuditEventClass.Authentication, eventType, outcome)
        {
            ActorUserId = actorUserId,
            Subject = subjectUserId is { } id ? UserSubject(id) : null,
            Attributes = attributes,
        };
    }

    private static AuditSubject UserSubject(Guid userId) => new("IdentityAccess", nameof(User), userId);

    /// <summary>What the caller may be told: whether it concerned the person (one answer for all) or the platform.</summary>
    private static AuthenticationFailure FailureOf(AuthenticationFailureReason reason) =>
        reason == AuthenticationFailureReason.ProviderUnavailable ? AuthenticationFailure.ProviderUnavailable
        : reason == AuthenticationFailureReason.NotConfigured ? AuthenticationFailure.NotConfigured
        : reason == AuthenticationFailureReason.IdentityNotVerified ? AuthenticationFailure.IdentityNotVerified
        : AuthenticationFailure.Rejected;

    /// <summary>The audit reason of a provider's failure; <paramref name="whenRejected"/> when it concerned the person.</summary>
    private static AuthenticationFailureReason ReasonOf(AuthenticationFailure? failure, AuthenticationFailureReason whenRejected) =>
        failure == AuthenticationFailure.ProviderUnavailable ? AuthenticationFailureReason.ProviderUnavailable
        : failure == AuthenticationFailure.NotConfigured ? AuthenticationFailureReason.NotConfigured
        : whenRejected;

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

    /// <summary>
    /// The longest username copied into an audit event: the request allows 256 characters, a directory name is far
    /// shorter, and a longer one is more likely something typed into the wrong field.
    /// </summary>
    private const int MaxAuditedUsernameLength = 100;

    /// <summary>One kind of attempt: its success and failure events, and what is known of it before it is decided.</summary>
    private sealed record Attempt(string SucceededEvent, string FailedEvent, AuthenticationMethod? Method, string? Username = null)
    {
        public static Attempt SignIn(AuthenticationMethod? method, string? username = null) =>
            new(IdentityAccessAuditEvents.SignInSucceeded, IdentityAccessAuditEvents.SignInFailed, method, username);

        public static Attempt Refresh(AuthenticationMethod? method) =>
            new(IdentityAccessAuditEvents.SessionRefreshed, IdentityAccessAuditEvents.SessionRefreshFailed, method);

        public static Attempt StepUp(AuthenticationMethod? method) =>
            new(IdentityAccessAuditEvents.StepUpSucceeded, IdentityAccessAuditEvents.StepUpFailed, method);

        public static Attempt IdentityVerification(AuthenticationMethod? method) =>
            new(IdentityAccessAuditEvents.IdentityVerified, IdentityAccessAuditEvents.IdentityVerificationFailed, method);
    }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session refresh rejected for user {UserId}: the user's identity must be verified by Nafath and the session never was.")]
    private static partial void LogRefreshWithoutIdentityVerification(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "User {UserId} must have their identity verified by Nafath and Nafath is not configured; no session is issued.")]
    private static partial void LogIdentityVerificationNotConfigured(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nafath did not verify user {UserId}: {Failure}. No session is issued; the verification may be retried.")]
    private static partial void LogIdentityNotVerified(ILogger logger, Guid userId, AuthenticationFailure? failure);
}
