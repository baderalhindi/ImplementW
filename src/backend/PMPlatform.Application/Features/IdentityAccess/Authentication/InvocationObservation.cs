using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// How one provider call ended, as FG-05 records it (FG-05 §6.2, §6.3). Only an unreachable or failing provider is a technical failure:
/// a refused password, code or identity is the provider answering. A call that was not made — the provider is not configured — is no
/// observation at all.
/// </summary>
internal sealed record InvocationObservation(IntegrationInvocationOutcome Outcome, IntegrationFailureKind? FailureKind, string? ResponseCode, string? ErrorSummary)
{
    public static InvocationObservation Succeeded { get; } = new(IntegrationInvocationOutcome.Succeeded, null, null, null);

    /// <summary>A sign-in, MFA or verification step's failure; null when the provider was not called.</summary>
    public static InvocationObservation? Of(AuthenticationFailure? failure) => failure switch
    {
        null => Succeeded,
        AuthenticationFailure.ProviderUnavailable => Failed(IntegrationFailureKind.Technical, "PROVIDER_UNAVAILABLE"),
        AuthenticationFailure.Rejected => Failed(IntegrationFailureKind.BusinessValidation, "REJECTED"),
        AuthenticationFailure.IdentityNotVerified => Failed(IntegrationFailureKind.BusinessValidation, "IDENTITY_NOT_VERIFIED"),
        AuthenticationFailure.NotConfigured => null,
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "Unknown authentication failure."),
    };

    /// <summary>A connection test: a refused service account is a security failure, anything else that failed a technical one.</summary>
    public static InvocationObservation? Of(ConnectionTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Outcome switch
        {
            ConnectionTestOutcome.Succeeded => Succeeded,
            ConnectionTestOutcome.Failed when result.FailureCode == ConnectionTestResult.BindRejected => Failed(IntegrationFailureKind.Security, result.FailureCode),
            ConnectionTestOutcome.Failed => Failed(IntegrationFailureKind.Technical, result.FailureCode),
            ConnectionTestOutcome.NotConfigured => null,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown connection test outcome."),
        };
    }

    /// <summary>The adapter threw: the kind of fault only, never its message (EV-3).</summary>
    public static InvocationObservation Faulted(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new InvocationObservation(IntegrationInvocationOutcome.Failed, IntegrationFailureKind.Technical, "FAULTED", exception.GetType().Name);
    }

    private static InvocationObservation Failed(IntegrationFailureKind kind, string? responseCode) =>
        new(IntegrationInvocationOutcome.Failed, kind, responseCode, null);
}
