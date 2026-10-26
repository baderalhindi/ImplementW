using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// A periodic measurement of an assignment, pinned to the target version ACTIVE when it was recorded. The pin is never
/// rewritten, so a later target never changes what an earlier measurement was measured against, nor its RAG. A value is present
/// exactly when MEASURED. Delete policy: HARD_DRAFT.
/// </summary>
public sealed class KpiMeasurement : AuditedEntity
{
    public Guid KpiAssignmentId { get; set; }

    /// <summary>Pinned at record time; never rewritten (ERD).</summary>
    public Guid KpiTargetVersionId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    /// <summary>Null when <see cref="ValueStatus"/> is not MEASURED.</summary>
    public decimal? MeasuredValue { get; set; }

    public ValueStatus ValueStatus { get; set; }

    /// <summary>Rated against the pinned target; stored because pinned (ERD §7 row 17).</summary>
    public KpiRagStatus RagStatus { get; set; }

    public DateOnly AsOfDate { get; set; }

    public Guid RecordedByUserId { get; set; }

    public NarrativeText? Narrative { get; set; }

    public KpiMeasurementStatus Status { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}
