using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>A cell of the recipient role → event family matrix (ADR-004). Delete policy: CASCADE.</summary>
public sealed class NotificationRecipientRule : AuditedEntity
{
    public Guid NotificationEventFamilyId { get; set; }

    public Guid RoleId { get; set; }
}
