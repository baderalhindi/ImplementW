using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Project;

/// <summary>
/// The one-time declaration for a project already under way: the Declared Baseline inputs and the opening position
/// (ADR-014, TASK-104). Each owning module writes its own fact from the ProjectIntakeRecorded event (ERD §7 row 18).
/// Delete policy: APPEND_ONLY.
/// </summary>
public sealed class ProjectIntake : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public DateOnly IntakeDate { get; set; }

    public required NarrativeText DeclaredScope { get; set; }

    /// <summary>Written to WF-14 as a DECLARED commitment.</summary>
    public Money DeclaredBudgetSar { get; set; }

    /// <summary>Written to WF-03 as the DECLARED baseline end date.</summary>
    public DateOnly DeclaredEndDate { get; set; }

    /// <summary>Written to WF-02 as the opening progress submission.</summary>
    public decimal OpeningPercentComplete { get; set; }

    /// <summary>Written to WF-14 as the opening financial progress update.</summary>
    public Money OpeningSpendToDateSar { get; set; }

    public Guid RecordedByUserId { get; set; }
}
