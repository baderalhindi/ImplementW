using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary><paramref name="Unread"/>: only unread (true), only read (false), or both (null).</summary>
public sealed record NotificationInboxQuery(bool? Unread, string? EventFamilyCode, PageRequest Page);
