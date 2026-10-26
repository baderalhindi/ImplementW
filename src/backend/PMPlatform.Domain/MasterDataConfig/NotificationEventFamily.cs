using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>A notification event family and whether recipients may turn it off (ADR-004). Delete policy: CASCADE.</summary>
public sealed class NotificationEventFamily : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    /// <summary>The stable family code that intents, templates and preferences reference.</summary>
    public required string Code { get; set; }

    public required BilingualLabel Label { get; set; }

    /// <summary>Security and critical escalation families cannot be disabled by the recipient.</summary>
    public bool IsMandatory { get; set; }
}
