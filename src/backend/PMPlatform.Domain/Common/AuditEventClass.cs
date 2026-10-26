namespace PMPlatform.Domain.Common;

/// <summary>
/// The mandatory audit classes (PTBC-011; ERD <c>audit_event.event_class</c>; event-conventions EV-9). Every module
/// records its audit events in one of these; which classes are forwarded to the SIEM is configuration (PTBC-029).
/// </summary>
public enum AuditEventClass
{
    Authentication = 1,
    AuthorizationDenial = 2,
    PrivilegedAction = 3,
    PermissionChange = 4,
    LifecycleTransition = 5,
    DataChange = 6,
    ApprovalDecision = 7,
    Integration = 8,
    ConfigurationChange = 9,
}
