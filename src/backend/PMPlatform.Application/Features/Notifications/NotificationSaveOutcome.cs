namespace PMPlatform.Application.Features.Notifications;

public enum NotificationSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key another request took first: a template version number, a delivery, a preference.</summary>
    Duplicate = 3,
}
