namespace PMPlatform.Application.Common.Events;

/// <summary>
/// A reminder's condition (EV-10): the subject's current status, as its source module reports it when the reminder is
/// due, must be one of <paramref name="SatisfiedWhenStatusIn"/>; otherwise the reminder is suppressed.
/// </summary>
public sealed record NotificationCondition(string SubjectType, Guid SubjectId, IReadOnlyList<string> SatisfiedWhenStatusIn);
