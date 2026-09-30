using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Notifications;

/// <summary>A template parameter supplied by the source; never a sensitive value (EV-3). Delete policy: CASCADE.</summary>
public sealed class NotificationIntentParameter : AuditedEntity
{
    public Guid NotificationIntentId { get; set; }

    public required string ParameterKey { get; set; }

    public required string ParameterValue { get; set; }
}
