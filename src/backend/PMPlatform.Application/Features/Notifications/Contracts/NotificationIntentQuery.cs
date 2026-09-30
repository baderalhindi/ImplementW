using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationIntentQuery(IReadOnlyCollection<NotificationIntentStatus> Statuses, string? EventFamilyCode, string? SourceModule, PageRequest Page);
