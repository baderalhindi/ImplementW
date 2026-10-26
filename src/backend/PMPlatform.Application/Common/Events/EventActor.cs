using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Common.Events;

/// <summary>Who caused the event. A service principal is a user (ERD D-2); <see cref="UserId"/> is null only for INTEGRATION.</summary>
public sealed record EventActor(AuditActorType ActorType, Guid? UserId);
