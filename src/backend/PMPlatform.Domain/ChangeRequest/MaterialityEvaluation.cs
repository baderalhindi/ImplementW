using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ChangeRequest;

/// <summary>
/// The provenance of one materiality evaluation of a change request's revision (TASK-106, ADR-016): the band each dimension triggers,
/// the cumulative position against the active baseline, the MATERIALITY_BAND version that decided it, and the governed commitments it
/// was evaluated against — pinned, so the authorisations issued on approval name the exact versions the approvers reviewed.
/// Delete policy: APPEND_ONLY.
/// </summary>
public sealed class MaterialityEvaluation : AuditedEntity
{
    public Guid ChangeRequestId { get; set; }

    /// <summary>The revision evaluated.</summary>
    public int RevisionNo { get; set; }

    public DateTimeOffset EvaluatedAt { get; set; }

    /// <summary>The MATERIALITY_BAND version whose bands decided it.</summary>
    public Guid MaterialityConfigurationVersionId { get; set; }

    /// <summary>The ACTIVE Approved Baseline evaluated against; changes accumulate against it (ADR-016).</summary>
    public Guid? ProjectBaselineId { get; set; }

    public int? ProjectBaselineVersionNo { get; set; }

    /// <summary>The baseline's duration in calendar days: the base of a schedule percentage threshold.</summary>
    public int? BaselineDurationDays { get; set; }

    /// <summary>The ACTIVE Approved Budget evaluated against, when the request has a cost impact.</summary>
    public Guid? FinancialCommitmentId { get; set; }

    public int? FinancialCommitmentVersionNo { get; set; }

    /// <summary>The budget's amount: the base of a cost percentage threshold.</summary>
    public Money? BaselineBudgetSar { get; set; }

    /// <summary>Approved changes since the baseline plus this request.</summary>
    public Money CumulativeCostImpactSar { get; set; }

    public int CumulativeScheduleImpactDays { get; set; }

    /// <summary>Null when the request has no cost impact.</summary>
    public short? CostBandNo { get; set; }

    /// <summary>Null when the request has no schedule impact.</summary>
    public short? ScheduleBandNo { get; set; }

    /// <summary>Null when the request has no scope impact.</summary>
    public short? ScopeBandNo { get; set; }

    /// <summary>The highest band any dimension triggers; 3 for a contractual obligation; never below 1.</summary>
    public short ResultingBandNo { get; set; }
}
