using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationFamilyPreference(string EventFamilyCode, BilingualLabel Label, bool IsMandatory, IReadOnlyList<NotificationChannelPreference> Channels);
