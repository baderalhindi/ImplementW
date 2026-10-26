namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>The caller's channels for every family of the routing configuration in force (SCR-154).</summary>
public sealed record NotificationPreferenceSet(IReadOnlyList<NotificationFamilyPreference> Families);
