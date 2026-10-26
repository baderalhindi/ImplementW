using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Notifications;

/// <summary>
/// One rendered delivery to one recipient on one channel, with its retry, suppression, dead-letter and read state
/// (SCR-150–154). What was rendered is kept as sent: templates are versioned and retired. Delete policy: RETAIN.
/// </summary>
public sealed class NotificationDelivery : AuditedEntity
{
    public Guid NotificationIntentId { get; set; }

    public Guid RecipientUserId { get; set; }

    public NotificationChannel Channel { get; set; }

    public Guid NotificationTemplateId { get; set; }

    /// <summary>The recipient's preferred language when it was rendered.</summary>
    public Language RenderedLanguage { get; set; }

    public string? RenderedSubject { get; set; }

    public required string RenderedBody { get; set; }

    public NotificationDeliveryStatus Status { get; set; }

    /// <summary>Why it was not sent, e.g. RECIPIENT_OPTED_OUT, MOBILE_UNVERIFIED.</summary>
    public string? SuppressionReason { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>From a delivery receipt (SMS, TASK-103).</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>In-app.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset? DeadLetteredAt { get; set; }

    public string? ProviderMessageId { get; set; }

    /// <summary>SMS segments (GSM-7 160/153, UCS-2 70/67).</summary>
    public short? SegmentCount { get; set; }

    /// <summary>The last attempt's failure, as the kind of fault only: never a provider response body or an address.</summary>
    public string? FailureReason { get; set; }
}
