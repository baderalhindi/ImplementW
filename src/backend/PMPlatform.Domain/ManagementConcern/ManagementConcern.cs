using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ManagementConcern;

/// <summary>
/// An issue or a challenge of a project (WF-07, TASK-057). Its severity is computed by the server from its impacts and pinned to
/// the configuration version whose rule computed it; its priority is chosen by people. The two never set each other. An issue
/// raised from a risk names it in <see cref="OriginatingRiskId"/> (edge 15). Delete policy: RETAIN — a concern is closed, never removed.
/// </summary>
public sealed class ManagementConcern : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public ConcernType ConcernType { get; set; }

    public required NarrativeText Title { get; set; }

    public required NarrativeText Description { get; set; }

    /// <summary>A CONCERN_CATEGORY master data item.</summary>
    public Guid CategoryItemId { get; set; }

    /// <summary>A PRIORITY master data item, chosen by people.</summary>
    public Guid PriorityItemId { get; set; }

    /// <summary>The highest impact level of the concern's dimensions; null until its impacts are assessed.</summary>
    public short? OverallImpactLevel { get; set; }

    /// <summary>A CONCERN_SEVERITY master data item, computed from <see cref="OverallImpactLevel"/>; never input.</summary>
    public Guid? SeverityItemId { get; set; }

    /// <summary>The RISK_MATRIX version whose impact scale and severity rule computed <see cref="SeverityItemId"/>.</summary>
    public Guid? SeverityConfigurationVersionId { get; set; }

    public ConcernStatus Status { get; set; }

    /// <summary>The business revision a WF-11 validation run is for; a returned validation starts the next one.</summary>
    public int RevisionNo { get; set; } = 1;

    public Guid RaisedByUserId { get; set; }

    public DateTimeOffset RaisedAt { get; set; }

    public Guid? AssigneeUserId { get; set; }

    /// <summary>The risk this issue was raised from, if any: the one foreign key both sides query.</summary>
    public Guid? OriginatingRiskId { get; set; }

    public DateOnly? TargetResolutionDate { get; set; }

    /// <summary>When the concern is next due for review, at the cadence of the project's governance profile (ADR-015).</summary>
    public DateOnly NextReviewDate { get; set; }

    public DateTimeOffset? LastReviewedAt { get; set; }

    /// <summary>The resolution submitted for validation; kept when a validation returns it, for correction.</summary>
    public NarrativeText? Resolution { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
}
