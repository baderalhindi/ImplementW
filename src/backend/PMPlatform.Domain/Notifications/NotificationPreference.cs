using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Notifications;

/// <summary>
/// A recipient's choice for one non-mandatory family on email or SMS (ADR-004). In-app is always on and a mandatory
/// family cannot be turned off, so neither has a row. Delete policy: HARD_OWNER.
/// </summary>
public sealed class NotificationPreference : AuditedEntity
{
    public Guid UserId { get; set; }

    public required string EventFamilyCode { get; set; }

    public NotificationChannel Channel { get; set; }

    public bool IsEnabled { get; set; }
}
