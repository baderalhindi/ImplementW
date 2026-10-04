using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// A reporting period's CURRENT/LIVE actual expenditure and forecast, one row per revision, aligned to WF-02's reporting cycle
/// (ADR-003 §8.2 edge 13), with provenance (ERD D-10). A figure that is not known is null, never zero. Reviewed and published by
/// AHDA; publication writes a <see cref="PublishedFinancialSnapshot"/>. Delete policy: HARD_DRAFT.
/// </summary>
public sealed class FinancialProgressUpdate : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public Guid ReportingCycleId { get; set; }

    public int RevisionNo { get; set; } = 1;

    public FinancialUpdateStatus Status { get; set; }

    /// <summary>Null = Unknown, never 0: present exactly when <see cref="ValueStatus"/> is MEASURED.</summary>
    public Money? ActualExpenditureToDateSar { get; set; }

    /// <summary>Null = Unknown; only with a MEASURED actual.</summary>
    public Money? ForecastAtCompletionSar { get; set; }

    public ValueStatus ValueStatus { get; set; }

    public NarrativeText? Narrative { get; set; }

    /// <summary>Set on the opening spend-to-date record of a legacy intake (ADR-014).</summary>
    public Guid? ProjectIntakeId { get; set; }

    public FinancialSourceType SourceType { get; set; }

    public string? SourceReference { get; set; }

    public DateOnly AsOfDate { get; set; }

    public Guid EnteredByUserId { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public NarrativeText? ReturnReason { get; set; }
}
