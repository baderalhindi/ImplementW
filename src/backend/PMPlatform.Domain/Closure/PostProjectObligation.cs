using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Closure;

/// <summary>
/// Work or responsibility that survives a project's completion — a warranty, a final payment, a residual risk's follow-up — with its
/// owner and due date (WF-10 §7.2, TASK-063). It stays open while the project is COMPLETED, so the project need not stay ACTIVE for it,
/// until the closure policy is satisfied: every obligation SATISFIED, WAIVED or CANCELLED before the project closes. Recorded against
/// the completion case, or the closure case on the terminal path (exactly one is named). Delete policy: RETAIN.
/// </summary>
public sealed class PostProjectObligation : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public Guid? CompletionCaseId { get; set; }

    public Guid? ClosureCaseId { get; set; }

    public required NarrativeText Title { get; set; }

    public NarrativeText? Description { get; set; }

    /// <summary>Who carries the obligation; an open obligation needs one before the completion is submitted (BR-CLO-023).</summary>
    public Guid? OwnerUserId { get; set; }

    public DateOnly? DueDate { get; set; }

    public PostProjectObligationStatus Status { get; set; }

    /// <summary>When it was settled as SATISFIED; set exactly then.</summary>
    public DateTimeOffset? SatisfiedAt { get; set; }
}
