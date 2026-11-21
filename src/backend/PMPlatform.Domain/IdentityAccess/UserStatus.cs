namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// TASK-031: activate and disable; disabling never rewrites historical attribution. Deleted (client review, item 5) has the same
/// effect on access and also takes the user out of administration; the row stays, so history keeps naming them, and it has no way back.
/// </summary>
public enum UserStatus
{
    Active = 1,
    Disabled = 2,
    Deleted = 3,
}
