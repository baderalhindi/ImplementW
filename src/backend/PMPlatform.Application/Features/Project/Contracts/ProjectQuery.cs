using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// SCR-025 Project Register: the projects the caller may see, most recently changed first (indexing-strategy I-01 to
/// I-05). <see cref="Q"/> matches the title, the Formal Project ID or the legacy reference. The sector (classification), strategic portfolio and
/// initiative, priority, region and city filters are the dashboards' filter bar on the register (the 2 Nov 2026 review, R3-1). <see cref="ProjectManagerUserId"/> serves SCR-026 My
/// Projects (I-04); like every filter it narrows the caller's scope and never widens it.
/// </summary>
public sealed record ProjectQuery(
    IReadOnlyCollection<ProjectLifecycleState> Statuses, Guid? DepartmentId, Guid? ExternalEntityId, Guid? ProjectManagerUserId, string? Q, PageRequest Page)
{
    public Guid? ClassificationItemId { get; init; }

    public Guid? StrategicPortfolioItemId { get; init; }

    public Guid? StrategicInitiativeItemId { get; init; }

    public Guid? PriorityItemId { get; init; }

    public Guid? RegionItemId { get; init; }

    public Guid? CityItemId { get; init; }

    /// <summary>A year: the projects whose planned period touches it (the review's Period filter).</summary>
    public int? PeriodYear { get; init; }

    /// <summary>The register's order; recently changed first by default (indexing-strategy P-2).</summary>
    public ProjectSort Sort { get; init; } = ProjectSort.UpdatedDescending;
}

/// <summary>The register's sort options (review round 5, item 2).</summary>
public enum ProjectSort
{
    UpdatedDescending = 1,
    UpdatedAscending = 2,
    TitleAscending = 3,
    TitleDescending = 4,
    FormalIdAscending = 5,
    FormalIdDescending = 6,
}
