namespace PMPlatform.Domain.IdentityAccess;

/// <summary>The scopes a role can be assigned with (the 5 Nov 2026 review, R4-6).</summary>
public static class RoleScopes
{
    public const string All = "ALL";
    public const string Department = "DEPARTMENT";
    public const string Project = "PROJECT";
    public const string Entity = "ENTITY";

    public static IReadOnlyList<string> Known { get; } = [All, Department, Project, Entity];
}
