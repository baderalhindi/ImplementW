using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationPreferenceChange(string EventFamilyCode, NotificationChannel Channel, bool IsEnabled);
