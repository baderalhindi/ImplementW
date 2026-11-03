using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Project;

/// <summary>
/// The project master aggregate (Blueprint Sections 5 and 9; ICD-02) and the root of the core-domain graph.
/// Delete policy: HARD_DRAFT — deletable only while DRAFT, by its owner, with an audit event.
/// </summary>
public sealed class Project : AuditedEntity
{
    /// <summary>
    /// The one authoritative identifier, unique, issued when AHDA approves the project. An entity may create a draft
    /// (ADR-013), so it is absent until approval (TASK-041).
    /// </summary>
    public string? FormalProjectId { get; set; }

    /// <summary>Free text in the language entered (ADR-012; ERD E-7).</summary>
    public required NarrativeText Title { get; set; }

    public NarrativeText? Description { get; set; }

    /// <summary>Master data item of catalogue PROJECT_CLASSIFICATION.</summary>
    public Guid ClassificationItemId { get; set; }

    /// <summary>The owning AHDA department.</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>The delivering entity, when the project is entity-delivered (ADR-013).</summary>
    public Guid? ExternalEntityId { get; set; }

    /// <summary>The R04 holder, employer-neutral (ADR-013); absent while DRAFT.</summary>
    public Guid? ProjectManagerUserId { get; set; }

    public ProjectLifecycleState LifecycleState { get; set; }

    /// <summary>A RETURNED request creates revision + 1 and a new approval instance (TASK-035).</summary>
    public int RevisionNo { get; set; } = 1;

    /// <summary>Assigned by rule on budget and duration, overridable by AHDA (TASK-105); changed only by a governed change.</summary>
    public Guid GovernanceProfileItemId { get; set; }

    public bool GovernanceProfileOverridden { get; set; }

    public NarrativeText? GovernanceProfileOverrideReason { get; set; }

    public ParticipationMode ParticipationMode { get; set; }

    /// <summary>
    /// The budget stated at registration, the input to profile assignment. The budget of record is WF-14's
    /// FinancialCommitment (ERD §7 row 19).
    /// </summary>
    public Money? RegistrationBudgetSar { get; set; }

    /// <summary>Registration-level dates; the schedule of record is WF-03's.</summary>
    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public Guid? RegionItemId { get; set; }

    public Guid? CityItemId { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    /// <summary>Master data item of catalogue STRATEGIC_PORTFOLIO: the strategy view of the project (the 2 Nov 2026 review, R3-3).</summary>
    public Guid? StrategicPortfolioItemId { get; set; }

    /// <summary>Master data item of catalogue STRATEGIC_INITIATIVE, an initiative of <see cref="StrategicPortfolioItemId"/>.</summary>
    public Guid? StrategicInitiativeItemId { get; set; }

    /// <summary>Master data item of catalogue PRIORITY.</summary>
    public Guid? PriorityItemId { get; set; }

    public FundingSource? FundingSource { get; set; }

    /// <summary>The project's number in RPMO's register before the platform, searchable; an import matches a row to it.</summary>
    public string? LegacyReference { get; set; }

    /// <summary>The RPMO staff member who follows the project up (the register's portfolio owner), an internal user.</summary>
    public Guid? FollowUpOwnerUserId { get; set; }

    /// <summary>The project import batch that created the project, which labels it Imported (R3-4).</summary>
    public Guid? ProjectImportBatchId { get; set; }

    /// <summary>The permanent intake marker: set once by the intake path, never cleared (ADR-014, TASK-104).</summary>
    public DateOnly? LegacyIntakeDate { get; set; }

    /// <summary>The Planned → Active command time, a business date (ERD §7 row 20).</summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    // The follow-up as RPMO keeps it between formal updates (the 2 Nov 2026 review, R3-3), changed by its own command at any state and
    // never by registration. Every value is optional: a blank is "not available", never zero.

    /// <summary>The obstacle holding the project up.</summary>
    public NarrativeText? CurrentObstacle { get; set; }

    public NarrativeText? NextAction { get; set; }

    /// <summary>Who is to take the next action, as RPMO names them: a person or an entity, free text.</summary>
    public string? NextActionOwner { get; set; }

    public DateOnly? NextActionDueDate { get; set; }

    /// <summary>Planned progress, 0 to 100, as declared where the project has no schedule to compute it from.</summary>
    public decimal? DeclaredPlannedPercent { get; set; }

    /// <summary>Actual progress, 0 to 100, as declared.</summary>
    public decimal? DeclaredActualPercent { get; set; }

    /// <summary>Planned spend as a share of the budget, 0 to 100, as declared where no amounts are recorded.</summary>
    public decimal? DeclaredPlannedSpendPercent { get; set; }

    public decimal? DeclaredActualSpendPercent { get; set; }

    /// <summary>The date the declared values hold for.</summary>
    public DateOnly? DeclaredAsOf { get; set; }

    /// <summary>Where the declared values came from; set whenever one of them is.</summary>
    public DeclaredValueSource? DeclaredSource { get; set; }

    /// <summary>Whether the delivering entity keeps to its periodic updates, as RPMO records it.</summary>
    public bool? EntityUpdateCompliant { get; set; }
}
