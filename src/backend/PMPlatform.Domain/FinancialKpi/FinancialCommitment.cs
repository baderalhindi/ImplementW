using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// One version of a project's commitment in SAR (ADR-008): each row is a version, and the ACTIVE APPROVED_BUDGET row is the
/// budget of record. Project total only at launch; the Etimad category lines exist and stay empty (ADR-008 gate). Carries the
/// four provenance fields (ERD D-10). Delete policy: HARD_DRAFT.
/// </summary>
public sealed class FinancialCommitment : AuditedEntity, IApprovedVersion
{
    public Guid ProjectId { get; set; }

    public CommitmentType CommitmentType { get; set; }

    /// <summary>1, 2, … per project and type.</summary>
    public int VersionNo { get; set; }

    /// <summary>A RETURNED version is resubmitted as revision + 1 under a new WF-11 run.</summary>
    public int RevisionNo { get; set; } = 1;

    public ApprovedVersionStatus Status { get; set; }

    public Money AmountSar { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public Guid? SupersededByCommitmentId { get; set; }

    /// <summary>WF-08's authorisation of the change (TASK-060); ChangeRequest is not built, so nothing sets it yet (financial-kpi.md F-3).</summary>
    public Guid? ChangeAuthorizationId { get; set; }

    /// <summary>Set on the DECLARED_BUDGET written from a legacy intake (ADR-014).</summary>
    public Guid? ProjectIntakeId { get; set; }

    public FinancialSourceType SourceType { get; set; }

    public string? SourceReference { get; set; }

    public DateOnly AsOfDate { get; set; }

    public Guid EnteredByUserId { get; set; }

    Guid? IApprovedVersion.SupersededById
    {
        get => SupersededByCommitmentId;
        set => SupersededByCommitmentId = value;
    }
}
