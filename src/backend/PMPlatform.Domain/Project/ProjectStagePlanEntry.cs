using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Project;

/// <summary>
/// One stage of a project's stage plan (review round 7, item 1): whether the stage applies to the project, when it is planned, and, for a project
/// already under way when it entered the platform, how far the stage had come. The stage's name and weight are not copied: they come from the
/// PROJECT_STAGE version in force, so a period always weighs by the version of its own day. Delete policy: NONE — a stage a later version drops keeps its row.
/// </summary>
public sealed class ProjectStagePlanEntry : AuditedEntity
{
    public Guid ProjectId { get; set; }

    /// <summary>The PROJECT_STAGE code, unique per project.</summary>
    public required string StageCode { get; set; }

    public bool Applies { get; set; }

    /// <summary>Required while <see cref="Applies"/>; within the project's planned dates.</summary>
    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    /// <summary>The stage's % complete when the project entered the platform, 0 to 100; only a project already under way has one.</summary>
    public decimal? StartingPercent { get; set; }
}
