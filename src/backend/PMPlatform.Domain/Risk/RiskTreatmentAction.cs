using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Risk;

/// <summary>A treatment or mitigation action of a risk (TASK-055). Delete policy: RETAIN — an action no longer wanted is cancelled.</summary>
public sealed class RiskTreatmentAction : AuditedEntity
{
    public Guid RiskId { get; set; }

    public required NarrativeText Title { get; set; }

    public NarrativeText? Description { get; set; }

    public RiskTreatmentActionType ActionType { get; set; }

    public Guid? OwnerUserId { get; set; }

    public DateOnly? DueDate { get; set; }

    public RiskTreatmentActionStatus Status { get; set; }

    /// <summary>Set exactly while the action is COMPLETED.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
