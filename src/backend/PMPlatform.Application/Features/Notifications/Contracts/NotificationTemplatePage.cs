namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationTemplatePage(IReadOnlyList<NotificationTemplateSummary> Items, int Page, int PageSize, int TotalCount);
