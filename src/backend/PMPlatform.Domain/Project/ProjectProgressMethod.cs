namespace PMPlatform.Domain.Project;

/// <summary>
/// How a project's overall progress is calculated (review round 8, point 02). A project with none — imported, or registered before the setting —
/// shows its declared figure, labelled as declared, until one is set.
/// </summary>
public enum ProjectProgressMethod
{
    /// <summary>The schedule's weighted progress against the approved baseline.</summary>
    Schedule = 1,

    /// <summary>Σ stage weight × its completion; Execution from the schedule once there is an approved baseline (round 7).</summary>
    Stages = 2,
}
