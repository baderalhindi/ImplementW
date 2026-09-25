using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>An AHDA organisation structure node (ADM-011/012). The directory is authoritative for membership (ADR-007). Delete policy: RETAIN.</summary>
public sealed class Department : AuditedEntity
{
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    public Guid? ParentDepartmentId { get; set; }

    /// <summary>The directory object identifier.</summary>
    public string? DirectoryReference { get; set; }

    public bool IsActive { get; set; } = true;
}
