namespace PMPlatform.Domain.Common;

/// <summary>Who acted (ERD <c>audit_event.actor_type</c>). A service principal is a <c>User</c> row (ERD D-2).</summary>
public enum AuditActorType
{
    User = 1,
    Service = 2,
    Integration = 3,
}
