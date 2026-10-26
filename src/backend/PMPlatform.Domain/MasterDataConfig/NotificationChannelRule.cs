using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>A cell of the event family → channel matrix (ADR-004). Delete policy: CASCADE.</summary>
public sealed class NotificationChannelRule : AuditedEntity
{
    public Guid NotificationEventFamilyId { get; set; }

    public NotificationChannel Channel { get; set; }

    public bool EnabledByDefault { get; set; }
}
