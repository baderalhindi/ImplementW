using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Notifications.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>The audit events of WF-15 (TASK-033 <c>IAuditTrail</c>). Template text is configuration and is recorded; rendered content is not.</summary>
internal static class NotificationAudit
{
    public const string Module = "Notifications";

    public static AuditEntry Template(string eventType, Guid actorId, NotificationTemplate template, IEnumerable<AuditAttribute?> attributes) =>
        new(AuditEventClass.ConfigurationChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(NotificationTemplate), template.Id),
            Attributes = [.. attributes.OfType<AuditAttribute>()],
        };

    public static AuditEntry PreferenceChanged(Guid actorId, NotificationPreference preference, bool? before) =>
        new(AuditEventClass.DataChange, NotificationAuditEvents.PreferenceChanged, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(NotificationPreference), preference.Id),
            Attributes =
            [
                AuditAttribute.Of("event_family_code", preference.EventFamilyCode),
                AuditAttribute.Of("channel", preference.Channel),
                .. new[] { AuditAttribute.Change("is_enabled", before, preference.IsEnabled) }.OfType<AuditAttribute>(),
            ],
        };

    public static AuditEntry IntentRedriven(Guid actorId, NotificationIntent intent, string? reason) =>
        new(AuditEventClass.PrivilegedAction, NotificationAuditEvents.IntentRedriven, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(NotificationIntent), intent.Id),
            ScopeProjectId = intent.ScopeProjectId,
            Attributes =
            [
                AuditAttribute.Change("status", NotificationIntentStatus.Failed, intent.Status)!,
                AuditAttribute.Of("suppression_reason", reason),
            ],
        };

    public static AuditEntry DeliveryRedriven(Guid actorId, NotificationDelivery delivery, int attempts) =>
        new(AuditEventClass.PrivilegedAction, NotificationAuditEvents.DeliveryRedriven, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(NotificationDelivery), delivery.Id),
            Attributes =
            [
                AuditAttribute.Of("channel", delivery.Channel),
                AuditAttribute.Change("status", NotificationDeliveryStatus.DeadLetter, delivery.Status)!,
                AuditAttribute.Of("attempt_count", attempts),
            ],
        };
}
