namespace PMPlatform.Domain.IdentityAccess;

/// <summary>What a per-user override does to what the user's roles give them (client review, item 5).</summary>
public enum PermissionOverrideEffect
{
    Grant = 1,
    Remove = 2,
}
