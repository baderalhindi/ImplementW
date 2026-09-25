namespace PMPlatform.Domain.IdentityAccess;

/// <summary>TASK-031: access ends on project closure or role change.</summary>
public enum AccessRelationshipStatus
{
    Active = 1,
    Ended = 2,
}
