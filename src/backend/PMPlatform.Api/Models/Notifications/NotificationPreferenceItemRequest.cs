using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Notifications;

/// <summary>One family on one channel, turned on or off.</summary>
public sealed record NotificationPreferenceItemRequest(string? EventFamilyCode, NotificationChannel? Channel, bool? IsEnabled);
