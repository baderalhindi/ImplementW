namespace PMPlatform.Domain.IdentityAccess;

/// <summary>TASK-031: activate and disable; disabling never rewrites historical attribution.</summary>
public enum UserStatus
{
    Active = 1,
    Disabled = 2,
}
