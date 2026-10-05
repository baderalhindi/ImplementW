using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Risk;

/// <summary>
/// A decision to tolerate a risk until <see cref="ExpiresOn"/> (TASK-055 gate decision: no permanent acceptance). On that day
/// it EXPIRES and the risk returns for review; it can be REVOKED before. At most one is ACTIVE per risk. Delete policy: RETAIN.
/// </summary>
public sealed class RiskAcceptance : AuditedEntity
{
    public Guid RiskId { get; set; }

    /// <summary>An internal user holding the acceptance permission: acceptance authority is unchanged for entity users (ADR-013).</summary>
    public Guid AcceptedByUserId { get; set; }

    public DateTimeOffset AcceptedAt { get; set; }

    /// <summary>The first day the acceptance no longer holds; always after the day it was given.</summary>
    public DateOnly ExpiresOn { get; set; }

    public required NarrativeText Rationale { get; set; }

    public RiskAcceptanceStatus Status { get; set; }

    /// <summary>Set exactly while the acceptance is REVOKED.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
