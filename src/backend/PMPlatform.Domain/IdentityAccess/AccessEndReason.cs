namespace PMPlatform.Domain.IdentityAccess;

public enum AccessEndReason
{
    ProjectClosed = 1,
    RoleChange = 2,
    Migrated = 3,
    Manual = 4,
    Expired = 5,
}
