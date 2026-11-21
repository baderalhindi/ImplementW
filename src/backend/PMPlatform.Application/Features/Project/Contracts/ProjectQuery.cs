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

    /// <summary>Only projects past registration: the projects the portfolio dashboard counts (review round 8, point 01).</summary>
    public bool Registered { get; init; }

    /// <summary>A current stage's code, or <see cref="NoStage"/> for the projects the dashboard counts under "no stage yet".</summary>
    public string? CurrentStage { get; init; }

    /// <summary>The codes of the stage version in force, which <see cref="NoStage"/> needs: a code outside it counts as no stage, as on the dashboard.</summary>
    public IReadOnlyCollection<string> KnownStageCodes { get; init; } = [];

    /// <summary>The on-plan label, or not measured. Calculated, so the register filters it after reading.</summary>
    public ProjectOnPlanFilter? OnPlan { get; init; }

    /// <summary>Only projects with both a planned and an actual percentage, as the dashboard's averages count them. Calculated, like <see cref="OnPlan"/>.</summary>
    public bool Measured { get; init; }

    /// <summary>The stage filter's value for a project with no current stage.</summary>
    public const string NoStage = "NONE";

    /// <summary>Whether a filter needs each project's calculated indicators, so the register cannot page in the database.</summary>
    public bool FiltersByIndicators => OnPlan is not null || Measured;
}

/// <summary>The register's on-plan filter: the three labels and the projects that have none (review round 8, point 01).</summary>
public enum ProjectOnPlanFilter
{
    OnTrack = 1,
    Late = 2,
    Stalled = 3,
    NotMeasured = 4,
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

    /// <summary>Registration value, highest first; a project with none last (review round 8, point 01).</summary>
    ValueDescending = 7,
}
