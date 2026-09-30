using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary><paramref name="IsConfigurable"/> is false for in-app and for every channel of a mandatory family.</summary>
public sealed record NotificationChannelPreference(NotificationChannel Channel, bool IsEnabled, bool IsConfigurable);
