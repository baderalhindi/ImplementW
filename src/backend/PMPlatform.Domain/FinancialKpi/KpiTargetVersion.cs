using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// One version of an assignment's target, in the KPI's unit, with its RAG thresholds (OQ-006). Approved through WF-11 and
/// immutable once ACTIVE: a new target is a new version, and every measurement keeps the version it was recorded against.
/// Delete policy: HARD_DRAFT.
/// </summary>
public sealed class KpiTargetVersion : AuditedEntity, IApprovedVersion
{
    public Guid KpiAssignmentId { get; set; }

    public int VersionNo { get; set; }

    public int RevisionNo { get; set; } = 1;

    public ApprovedVersionStatus Status { get; set; }

    public decimal TargetValue { get; set; }

    public decimal? GreenThreshold { get; set; }

    public decimal? AmberThreshold { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public Guid? SupersededByTargetVersionId { get; set; }

    Guid? IApprovedVersion.SupersededById
    {
        get => SupersededByTargetVersionId;
        set => SupersededByTargetVersionId = value;
    }
}
