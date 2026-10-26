using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>
/// How a provider call is reported to FG-05 (TASK-075; FG-05 §6.3): only an unreachable or failing provider is a technical failure; a
/// refused password, code or identity is the provider answering; a call that was not made is not reported; a fault is reported by its kind.
/// </summary>
public sealed class InvocationObservationTests
{
    [Theory]
    [InlineData(AuthenticationFailure.ProviderUnavailable, IntegrationFailureKind.Technical, "PROVIDER_UNAVAILABLE")]
    [InlineData(AuthenticationFailure.Rejected, IntegrationFailureKind.BusinessValidation, "REJECTED")]
    [InlineData(AuthenticationFailure.IdentityNotVerified, IntegrationFailureKind.BusinessValidation, "IDENTITY_NOT_VERIFIED")]
    public void AFailureIsClassifiedByWhoseItIs(AuthenticationFailure failure, IntegrationFailureKind kind, string responseCode) =>
        Assert.Equal(new InvocationObservation(IntegrationInvocationOutcome.Failed, kind, responseCode, null), InvocationObservation.Of(failure));

    [Fact]
    public void NoFailureIsASuccessAndAnUnconfiguredProviderIsNoCall()
    {
        Assert.Equal(InvocationObservation.Succeeded, InvocationObservation.Of((AuthenticationFailure?)null));
        Assert.Null(InvocationObservation.Of(AuthenticationFailure.NotConfigured));
        Assert.Null(InvocationObservation.Of(new ConnectionTestResult(ConnectionTestOutcome.NotConfigured, DateTimeOffset.UtcNow, null)));
    }

    [Fact]
    public void ARefusedServiceAccountIsASecurityFailureAndAnUnreachableOneTechnical()
    {
        Assert.Equal(IntegrationFailureKind.Security, InvocationObservation.Of(new ConnectionTestResult(ConnectionTestOutcome.Failed, DateTimeOffset.UtcNow, ConnectionTestResult.BindRejected))!.FailureKind);
        Assert.Equal(IntegrationFailureKind.Technical, InvocationObservation.Of(new ConnectionTestResult(ConnectionTestOutcome.Failed, DateTimeOffset.UtcNow, ConnectionTestResult.Unreachable))!.FailureKind);
    }

    [Fact]
    public void AFaultIsReportedByItsKindNeverItsMessage()
    {
        InvocationObservation observation = InvocationObservation.Faulted(new InvalidOperationException("ldap://admin:secret@directory refused"));

        Assert.Equal((IntegrationFailureKind.Technical, "InvalidOperationException"), (observation.FailureKind, observation.ErrorSummary));
    }
}
