using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationTemplateSummary(
    Guid Id, string EventFamilyCode, string EventType, NotificationChannel Channel, int VersionNo, GovernedLifecycleState LifecycleState, DateTimeOffset UpdatedAt);
