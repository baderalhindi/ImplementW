namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationPage(IReadOnlyList<NotificationSummary> Items, int Page, int PageSize, int TotalCount);
