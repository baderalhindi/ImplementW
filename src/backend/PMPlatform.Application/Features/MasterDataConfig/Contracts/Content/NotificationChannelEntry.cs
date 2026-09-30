using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>A channel of an event family; <see cref="EnabledByDefault"/> is what a recipient starts with.</summary>
public sealed record NotificationChannelEntry(NotificationChannel Channel, bool EnabledByDefault);
