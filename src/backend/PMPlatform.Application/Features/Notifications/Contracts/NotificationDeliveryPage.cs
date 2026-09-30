namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationDeliveryPage(IReadOnlyList<NotificationDeliverySummary> Items, int Page, int PageSize, int TotalCount);
