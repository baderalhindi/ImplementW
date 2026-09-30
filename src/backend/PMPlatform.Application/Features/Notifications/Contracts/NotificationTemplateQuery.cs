using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationTemplateQuery(
    string? EventType, string? EventFamilyCode, IReadOnlyCollection<NotificationChannel> Channels, IReadOnlyCollection<GovernedLifecycleState> LifecycleStates, PageRequest Page);
