using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationHistoryQuery(IReadOnlyCollection<NotificationChannel> Channels, IReadOnlyCollection<NotificationDeliveryStatus> Statuses, PageRequest Page);
