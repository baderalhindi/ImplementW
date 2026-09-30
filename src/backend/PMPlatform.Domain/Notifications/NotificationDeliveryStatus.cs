namespace PMPlatform.Domain.Notifications;

/// <summary>One delivery's state (ERD §6 row 44).</summary>
public enum NotificationDeliveryStatus
{
    /// <summary>Rendered and waiting to be sent.</summary>
    Pending = 1,

    /// <summary>Handed to the channel: stored in the inbox, or accepted by the mail relay or SMS gateway.</summary>
    Sent = 2,

    /// <summary>The provider's delivery receipt confirmed it (SMS, TASK-103).</summary>
    Delivered = 3,

    /// <summary>The last attempt failed; it is tried again at <c>next_attempt_at</c>.</summary>
    Failed = 4,

    /// <summary>Every attempt failed; kept with its last failure until an operator redrives it.</summary>
    DeadLetter = 5,

    /// <summary>Not sent, by rule: the recipient opted out, is no longer eligible, or the channel cannot reach them.</summary>
    Suppressed = 6,

    /// <summary>An in-app notification its recipient has read.</summary>
    Read = 7,
}
