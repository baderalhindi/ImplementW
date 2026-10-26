using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Events;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// ADM-041: the directory and SSO settings in effect, and a test of each connection; Nafath's beside them (TASK-068) until
/// ADM-043 has a screen of its own (TASK-076). A test binds with the service account's credentials, so it is a privileged
/// action and is audited with its outcomes (TASK-033).
/// </summary>
internal sealed class IdentityIntegrationService(
    IDirectoryService directory,
    ISingleSignOnProvider singleSignOn,
    IIdentityVerificationProvider identityVerification,
    IAuditTrail audit,
    IAuditRequestContext request) : IIdentityIntegrationService
{
    public IdentityIntegrationStatus GetStatus() => new(directory.Describe(), singleSignOn.Describe(), identityVerification.Describe());

    public async Task<IdentityIntegrationTestResult> TestConnectionsAsync(CancellationToken cancellationToken)
    {
        Task<ConnectionTestResult> directoryTest = directory.TestConnectionAsync(cancellationToken);
        Task<ConnectionTestResult> singleSignOnTest = singleSignOn.TestConnectionAsync(cancellationToken);
        Task<ConnectionTestResult> identityVerificationTest = identityVerification.TestConnectionAsync(cancellationToken);
        IdentityIntegrationTestResult result = new(
            await directoryTest.ConfigureAwait(false),
            await singleSignOnTest.ConfigureAwait(false),
            await identityVerificationTest.ConfigureAwait(false));

        AuditEntry tested = new(AuditEventClass.PrivilegedAction, IdentityAccessAuditEvents.IdentityIntegrationTested, AuditOutcome.Success)
        {
            ActorUserId = request.UserId,
            Attributes =
            [
                AuditAttribute.Of("directory_outcome", result.Directory.Outcome),
                AuditAttribute.Of("directory_failure_code", result.Directory.FailureCode),
                AuditAttribute.Of("single_sign_on_outcome", result.SingleSignOn.Outcome),
                AuditAttribute.Of("single_sign_on_failure_code", result.SingleSignOn.FailureCode),
                AuditAttribute.Of("identity_verification_outcome", result.IdentityVerification.Outcome),
                AuditAttribute.Of("identity_verification_failure_code", result.IdentityVerification.FailureCode),
            ],
        };
        await audit.RecordAsync(tested).ConfigureAwait(false);
        return result;
    }
}
