using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Risk;

/// <summary>
/// One assessment of a risk (TASK-055, ADR-011): its probability, its impact per dimension (<see cref="RiskAssessmentImpact"/>),
/// and the rating the RISK_MATRIX version in force at assessment time gave them. The version id, the overall impact and the
/// rating are stored with it (ERD §7 row 12), so a later publication of the matrix never changes a recorded rating.
/// Delete policy: APPEND_ONLY — a reassessment is a new version.
/// </summary>
public sealed class RiskAssessmentVersion : AuditedEntity
{
    public Guid RiskId { get; set; }

    /// <summary>1 for the first assessment of the risk, one more for each after it.</summary>
    public int VersionNo { get; set; }

    public DateTimeOffset AssessedAt { get; set; }

    public Guid AssessedByUserId { get; set; }

    /// <summary>The PUBLISHED RISK_MATRIX configuration version the rating was computed from.</summary>
    public Guid MatrixConfigurationVersionId { get; set; }

    /// <summary>1 to 5.</summary>
    public short ProbabilityLevel { get; set; }

    /// <summary>1 to 5: the highest level of any dimension.</summary>
    public short OverallImpactLevel { get; set; }

    /// <summary>The rating of the pinned matrix's cell for the probability and the overall impact.</summary>
    public Guid RiskRatingDefinitionId { get; set; }

    public NarrativeText? Rationale { get; set; }
}
