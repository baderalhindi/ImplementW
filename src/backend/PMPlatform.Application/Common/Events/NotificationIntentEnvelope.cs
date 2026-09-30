namespace PMPlatform.Application.Common.Events;

/// <summary>The envelope of a NOTIFICATION_INTENT as its consumer reads it; a producer declares its own type for the same shape (EV-8).</summary>
public sealed record NotificationIntentEnvelope : EventEnvelope<NotificationIntentData>;
