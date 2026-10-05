using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Risk;

/// <summary>
/// The impact level of one dimension in an assessment (ADR-011): one row per IMPACT_DIMENSION item the pinned matrix defines
/// levels for, so a new dimension needs no migration. Delete policy: APPEND_ONLY.
/// </summary>
public sealed class RiskAssessmentImpact : AuditedEntity
{
    public Guid RiskAssessmentVersionId { get; set; }

    public Guid ImpactDimensionItemId { get; set; }

    /// <summary>1 to 5.</summary>
    public short ImpactLevel { get; set; }

    public NarrativeText? Rationale { get; set; }
}
