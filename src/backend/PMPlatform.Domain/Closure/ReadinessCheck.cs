using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Closure;

/// <summary>
/// One readiness record of a completion or closure case (exactly one case is named), APPEND_ONLY (TASK-063). An evaluation appends one
/// PASS or FAIL row per criterion, all with the same <see cref="EvaluatedAt"/>; the evaluation a submission made is that revision's
/// immutable readiness snapshot (CLO-CC-07). A waiver appends a WAIVED row for a failed criterion: who accepted the exception, and why.
/// </summary>
public sealed class ReadinessCheck : AuditedEntity
{
    public Guid? CompletionCaseId { get; set; }

    public Guid? ClosureCaseId { get; set; }

    public ReadinessCheckCode CheckCode { get; set; }

    public ReadinessResult Result { get; set; }

    public DateTimeOffset EvaluatedAt { get; set; }

    /// <summary>How many of the project's records kept the criterion from passing when it was evaluated or waived; 0 for a pass.</summary>
    public int BlockingCount { get; set; }

    /// <summary>A waiver's reason, stored as entered (ADR-012); set exactly on a WAIVED row.</summary>
    public NarrativeText? Detail { get; set; }

    /// <summary>Who accepted the exception; set exactly on a WAIVED row.</summary>
    public Guid? WaivedByUserId { get; set; }
}
