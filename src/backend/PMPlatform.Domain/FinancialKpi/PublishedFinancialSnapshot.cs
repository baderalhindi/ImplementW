using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// The PUBLISHED/OFFICIAL financial position of a reporting period. Self-contained: every figure is a copy taken at publication,
/// with the status rated under the thresholds pinned here, so a later correction anywhere changes nothing in it. Delete policy:
/// APPEND_ONLY — never updated, never deleted.
/// </summary>
public sealed class PublishedFinancialSnapshot : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public Guid ReportingCycleId { get; set; }

    public Guid FinancialProgressUpdateId { get; set; }

    /// <summary>The ACTIVE APPROVED_BUDGET at publication; null when there was none.</summary>
    public Guid? FinancialCommitmentId { get; set; }

    public DateTimeOffset PublishedAt { get; set; }

    public Guid PublishedByUserId { get; set; }

    public Money? ApprovedBudgetSar { get; set; }

    public Money? ActualExpenditureToDateSar { get; set; }

    public Money? ForecastAtCompletionSar { get; set; }

    public ValueStatus ValueStatus { get; set; }

    public FinancialStatus FinancialStatus { get; set; }

    /// <summary>The WORKFLOW_POLICY version whose thresholds rated <see cref="FinancialStatus"/> (ERD D-13).</summary>
    public Guid ThresholdConfigurationVersionId { get; set; }

    public FinancialSourceType SourceType { get; set; }

    public string? SourceReference { get; set; }

    public DateOnly AsOfDate { get; set; }

    public Guid EnteredByUserId { get; set; }
}
