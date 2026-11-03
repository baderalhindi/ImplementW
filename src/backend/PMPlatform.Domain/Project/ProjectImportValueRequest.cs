using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Project;

/// <summary>
/// An importer's request to add a value the platform's lists lack — an entity, a department, a sector, a portfolio, a city … (the 2 Nov 2026
/// review §3: "an unknown name triggers a request to add it"). Administrators are notified; one marks it added or declined, and the importer is
/// notified in turn. Its requester is <see cref="AuditedEntity.CreatedBy"/>. Delete policy: RETAIN.
/// </summary>
public sealed class ProjectImportValueRequest : AuditedEntity
{
    /// <summary>What the name is of: ENTITY, DEPARTMENT, SECTOR, … (ProjectImportDimension, as text).</summary>
    public required string Dimension { get; set; }

    /// <summary>The name as the workbook writes it.</summary>
    public required string SourceText { get; set; }

    public ProjectImportValueRequestStatus Status { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public NarrativeText? ResolutionNote { get; set; }
}

public enum ProjectImportValueRequestStatus
{
    Open = 1,
    Added = 2,
    Declined = 3,
}
