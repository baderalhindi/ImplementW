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

    /// <summary>The permanent intake marker: set once by the intake path, never cleared (ADR-014, TASK-104).</summary>
    public DateOnly? LegacyIntakeDate { get; set; }

    /// <summary>The Planned → Active command time, a business date (ERD §7 row 20).</summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
}
