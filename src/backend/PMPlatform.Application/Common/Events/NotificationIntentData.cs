namespace PMPlatform.Application.Common.Events;

/// <summary>
/// The data of every NOTIFICATION_INTENT (event-conventions EV-10), whatever module produces it: it crosses edge E-U4
/// through this type in <c>Application/Common</c>, so no producer references Notifications. It never names a recipient
/// (Notifications resolves them from the family and the envelope's scope) and never carries a sensitive value (EV-3).
/// </summary>
/// <param name="EventFamilyCode">An FG-04 <c>NotificationEventFamily</c> code: the routing key into the ADR-004 matrices.</param>
/// <param name="SourceReference">Equal to the envelope's <c>idempotencyKey</c>.</param>
/// <param name="ScheduledFor">Null to notify now; otherwise a reminder, sent then if its <paramref name="Condition"/> still holds.</param>
/// <param name="DeepLink">An SPA route, e.g. <c>/projects/{id}/risks/{id}</c>; never an absolute URL and never a token.</param>
/// <param name="Parameters">Template parameters: strings, none sensitive.</param>
/// <param name="Condition">Required for a reminder: what must still be true when it is sent.</param>
public sealed record NotificationIntentData(
    string EventFamilyCode,
    string SourceReference,
    DateTimeOffset? ScheduledFor,
    string DeepLink,
    IReadOnlyList<NotificationParameter> Parameters,
    NotificationCondition? Condition);
