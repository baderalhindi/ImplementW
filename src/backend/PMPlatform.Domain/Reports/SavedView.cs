using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// A person's private report configuration (FG-02 §10.2, SAV-001–014): parameter values for one report, or an SCR-138 composition of
/// allowlisted fields. Configuration only — no report row is ever stored with it (BR-RPT-031) — and every use revalidates it against the report
/// version and the allowlist in force and against its owner's access then (BR-RPT-033). Private: sharing is Conditional (TASK-112). Delete
/// policy: HARD_OWNER.
/// </summary>
public sealed class SavedView : AuditedEntity
{
    public Guid OwnerUserId { get; set; }

    public SavedViewType ViewType { get; set; }

    /// <summary>The report version the parameters were saved against (REPORT_PARAMETERS only); compatibility with the version in force is checked at use.</summary>
    public Guid? ReportDefinitionId { get; set; }

    /// <summary>Free text in the language entered (ADR-012).</summary>
    public required NarrativeText Name { get; set; }
}
