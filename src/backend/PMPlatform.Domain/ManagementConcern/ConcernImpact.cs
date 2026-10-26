using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ManagementConcern;

/// <summary>
/// The impact level of one dimension of a concern (ADR-011): the dimension set risks and issues share, one row per dimension
/// assessed. A reassessment replaces the concern's rows. Delete policy: CASCADE.
/// </summary>
public sealed class ConcernImpact : AuditedEntity
{
    public Guid ManagementConcernId { get; set; }

    public Guid ImpactDimensionItemId { get; set; }

    /// <summary>1 to 5.</summary>
    public short ImpactLevel { get; set; }

    public NarrativeText? Rationale { get; set; }
}
