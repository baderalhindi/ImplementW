using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Events;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

internal sealed class AccessAudit(IAuditTrail audit) : IAccessAudit
{
    public Task AccessTokenRejectedAsync(AuthenticationFailureReason reason, string requestPath) =>
        audit.RecordAsync(new AuditEntry(AuditEventClass.Authentication, IdentityAccessAuditEvents.AccessTokenRejected, AuditOutcome.Failed)
        {
            Attributes =
            [
                AuditAttribute.Of(IdentityAccessAuditAttributes.FailureReason, reason),
                AuditAttribute.Of(IdentityAccessAuditAttributes.RequestPath, requestPath),
            ],
        });

    public Task StepUpRequiredAsync(Guid userId, string operation) =>
        audit.RecordAsync(new AuditEntry(AuditEventClass.AuthorizationDenial, IdentityAccessAuditEvents.StepUpRequired, AuditOutcome.Denied)
        {
            ActorUserId = userId,
            Attributes = [AuditAttribute.Of(IdentityAccessAuditAttributes.Operation, operation)],
        });

    public Task MultiFactorGateDeniedAsync(Guid userId, string permissionCode) =>
        audit.RecordAsync(new AuditEntry(AuditEventClass.AuthorizationDenial, IdentityAccessAuditEvents.AccessDenied, AuditOutcome.Denied)
        {
            ActorUserId = userId,
            Attributes =
            [
                AuditAttribute.Of(IdentityAccessAuditAttributes.PermissionCode, permissionCode),
                AuditAttribute.Of(IdentityAccessAuditAttributes.DenialReason, AuthenticationFailureReason.MultiFactorMissing),
            ],
        });
}
