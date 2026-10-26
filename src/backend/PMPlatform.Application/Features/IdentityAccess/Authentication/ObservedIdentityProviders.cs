using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>The directory, observed: every call to it is FG-05 telemetry (TASK-075). Behaviour is the adapter's, unchanged.</summary>
internal sealed class ObservedDirectoryService(IDirectoryService inner, IdentityIntegrationTelemetry telemetry) : IDirectoryService
{
    public bool IsConfigured => inner.IsConfigured;

    public Task<DirectoryResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.Directory, "AUTHENTICATE", () => inner.AuthenticateAsync(username, password, cancellationToken), r => InvocationObservation.Of(r.Failure));

    public Task<DirectoryResult> FindBySubjectAsync(string subjectId, CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.Directory, "READ_PERSON", () => inner.FindBySubjectAsync(subjectId, cancellationToken), r => InvocationObservation.Of(r.Failure));

    public DirectoryIntegrationStatus Describe() => inner.Describe();

    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.Directory, "CONNECTION_TEST", () => inner.TestConnectionAsync(cancellationToken), InvocationObservation.Of);
}

/// <summary>The identity provider, observed (TASK-075).</summary>
internal sealed class ObservedSingleSignOnProvider(ISingleSignOnProvider inner, IdentityIntegrationTelemetry telemetry) : ISingleSignOnProvider
{
    public bool IsConfigured => inner.IsConfigured;

    public Task<SsoAuthorizationResult> BeginAsync(CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.SingleSignOn, "AUTHORIZE", () => inner.BeginAsync(cancellationToken), r => InvocationObservation.Of(r.Failure));

    public Task<SingleSignOnResult> CompleteAsync(string code, string state, string transaction, CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.SingleSignOn, "TOKEN_REDEEM", () => inner.CompleteAsync(code, state, transaction, cancellationToken), r => InvocationObservation.Of(r.Failure));

    public SingleSignOnIntegrationStatus Describe() => inner.Describe();

    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.SingleSignOn, "CONNECTION_TEST", () => inner.TestConnectionAsync(cancellationToken), InvocationObservation.Of);
}

/// <summary>The MFA provider, observed (TASK-075). A wrong code is the provider answering, not an outage.</summary>
internal sealed class ObservedMultiFactorProvider(IMultiFactorProvider inner, IdentityIntegrationTelemetry telemetry) : IMultiFactorProvider
{
    public bool IsConfigured => inner.IsConfigured;

    public Task<MultiFactorChallengeResult> StartAsync(Guid userId, MultiFactorPurpose purpose, CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.MultiFactor, "START_CHALLENGE", () => inner.StartAsync(userId, purpose, cancellationToken), r => InvocationObservation.Of(r.Failure));

    public Task<MultiFactorVerification> CompleteAsync(Guid userId, MultiFactorPurpose purpose, string challengeId, string code, CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.MultiFactor, "VERIFY_CHALLENGE", () => inner.CompleteAsync(userId, purpose, challengeId, code, cancellationToken), r => InvocationObservation.Of(r.Failure));
}

/// <summary>Nafath, observed (TASK-075; ADR-003 §8.2 edge 33). The verification's reference is never part of the telemetry.</summary>
internal sealed class ObservedIdentityVerificationProvider(IIdentityVerificationProvider inner, IdentityIntegrationTelemetry telemetry) : IIdentityVerificationProvider
{
    public bool IsConfigured => inner.IsConfigured;

    public Task<IdentityVerificationAuthorization> BeginAsync(Guid userId, CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.Nafath, "AUTHORIZE", () => inner.BeginAsync(userId, cancellationToken), r => InvocationObservation.Of(r.Failure));

    public Task<IdentityVerificationResult> CompleteAsync(Guid userId, string code, string state, string transaction, CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.Nafath, "TOKEN_REDEEM", () => inner.CompleteAsync(userId, code, state, transaction, cancellationToken), r => InvocationObservation.Of(r.Failure));

    public IdentityVerificationIntegrationStatus Describe() => inner.Describe();

    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken) =>
        telemetry.ObserveAsync(IntegrationCodes.Nafath, "CONNECTION_TEST", () => inner.TestConnectionAsync(cancellationToken), InvocationObservation.Of);
}
