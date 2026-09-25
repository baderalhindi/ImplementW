namespace PMPlatform.Domain.IdentityAccess;

/// <summary>The data scope a granted permission applies to (Blueprint Section 10.1).</summary>
public enum DataScope
{
    All = 1,
    Dept = 2,
    Own = 3,
    Assigned = 4,
    Entity = 5,
    ReadOnly = 6,
}
