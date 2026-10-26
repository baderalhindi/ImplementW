namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>An intent with every delivery it produced. Rendered content is its recipient's to read, so it is not shown here.</summary>
public sealed record NotificationIntentDetail(NotificationIntentSummary Intent, string? SubjectType, Guid? SubjectId, Guid? ScopeProjectId, IReadOnlyList<NotificationDeliverySummary> Deliveries);
