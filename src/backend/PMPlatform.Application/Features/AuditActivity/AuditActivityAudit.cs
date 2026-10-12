using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Application.Features.AuditActivity.Contracts.Events;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>AuditActivity's own audit entries (event-conventions row 30): all PRIVILEGED_ACTION, each recorded on its own.</summary>
internal static class AuditActivityAudit
{
    private const string Module = "AuditActivity";

    public static AuditEntry SensitiveDetailViewed(Guid viewerId, AuditEvent viewed, AuditCategory category, bool requestContextRevealed) =>
        new(AuditEventClass.PrivilegedAction, AuditActivityAuditEvents.SensitiveDetailViewed, AuditOutcome.Success)
        {
            ActorUserId = viewerId,
            Subject = new AuditSubject(Module, nameof(AuditEvent), viewed.Id),
            ScopeProjectId = viewed.ScopeProjectId,
            ScopeExternalEntityId = viewed.ScopeExternalEntityId,
            Attributes =
            [
                AuditAttribute.Of(AuditActivityAuditAttributes.Category, category),
                AuditAttribute.Of(AuditActivityAuditAttributes.RequestContextRevealed, requestContextRevealed),
            ],
        };

    public static AuditEntry IntegrityVerified(Guid actorId, AuditIntegrityReport report) =>
        Operation(actorId, AuditActivityAuditEvents.IntegrityVerified, report.Intact,
        [
            AuditAttribute.Of(AuditActivityAuditAttributes.EventsVerified, report.EventsVerified),
            AuditAttribute.Of(AuditActivityAuditAttributes.BrokenLinks, report.BrokenLinks),
            AuditAttribute.Of(AuditActivityAuditAttributes.AttributeDigestMismatches, report.AttributeDigestMismatches),
        ]);

    public static AuditEntry ProjectionChecked(Guid actorId, ActivityProjectionReport report) =>
        Operation(actorId, AuditActivityAuditEvents.ActivityProjectionChecked, report.Matches, Counts(report));

    public static AuditEntry ProjectionRebuilt(Guid actorId, ActivityProjectionReport report) =>
        Operation(actorId, AuditActivityAuditEvents.ActivityProjectionRebuilt, succeeded: true, Counts(report));

    private static AuditEntry Operation(Guid actorId, string eventType, bool succeeded, IReadOnlyList<AuditAttribute> attributes) =>
        new(AuditEventClass.PrivilegedAction, eventType, succeeded ? AuditOutcome.Success : AuditOutcome.Failed)
        {
            ActorUserId = actorId,
            ActorType = actorId == AuditCapturePrincipal.Id ? AuditActorType.Service : AuditActorType.User,
            Attributes = attributes,
        };

    private static AuditAttribute[] Counts(ActivityProjectionReport report) =>
    [
        AuditAttribute.Of(AuditActivityAuditAttributes.EventsCompared, report.EventsCompared),
        AuditAttribute.Of(AuditActivityAuditAttributes.Missing, report.Missing),
        AuditAttribute.Of(AuditActivityAuditAttributes.Unexpected, report.Unexpected),
        AuditAttribute.Of(AuditActivityAuditAttributes.Changed, report.Changed),
    ];
}
