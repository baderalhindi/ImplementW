namespace PMPlatform.Domain.Notifications;

/// <summary>Where a received intent is on its way to its recipients (ERD §6 row 43).</summary>
public enum NotificationIntentStatus
{
    /// <summary>Received after its source committed; to be routed now.</summary>
    Received = 1,

    /// <summary>A reminder, routed at its <c>scheduled_for</c> once its condition is revalidated.</summary>
    Scheduled = 2,

    /// <summary>In the ERD's value set; never stored, because resolution and routing commit together (record F-9).</summary>
    Resolving = 3,

    /// <summary>Deliveries exist and at least one is still being sent.</summary>
    Routed = 4,

    /// <summary>Nothing was sent, by rule: the condition resolved, or no one is eligible. The reason is recorded.</summary>
    Suppressed = 5,

    /// <summary>Every delivery reached a final state.</summary>
    Completed = 6,

    /// <summary>Routing was refused: configuration or a mandatory template is missing. The reason is recorded; it can be redriven.</summary>
    Failed = 7,
}
