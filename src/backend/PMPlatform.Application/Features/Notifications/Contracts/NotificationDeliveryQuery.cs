using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationDeliveryQuery(IReadOnlyCollection<NotificationDeliveryStatus> Statuses, IReadOnlyCollection<NotificationChannel> Channels, PageRequest Page);
