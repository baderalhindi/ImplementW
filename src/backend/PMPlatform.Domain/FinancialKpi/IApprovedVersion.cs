namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// What a commitment version and a KPI target version share as WF-11 subjects (TASK-052): an identity, a revision, the
/// approved-version lifecycle, and the version that superseded it.
/// </summary>
public interface IApprovedVersion
{
    public Guid Id { get; }

    public int RevisionNo { get; set; }

    public ApprovedVersionStatus Status { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public Guid? SupersededById { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid UpdatedBy { get; set; }
}
