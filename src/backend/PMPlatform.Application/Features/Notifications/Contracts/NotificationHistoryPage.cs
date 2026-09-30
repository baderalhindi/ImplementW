namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationHistoryPage(IReadOnlyList<NotificationHistoryItem> Items, int Page, int PageSize, int TotalCount);
