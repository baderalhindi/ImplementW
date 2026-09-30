namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationIntentPage(IReadOnlyList<NotificationIntentSummary> Items, int Page, int PageSize, int TotalCount);
