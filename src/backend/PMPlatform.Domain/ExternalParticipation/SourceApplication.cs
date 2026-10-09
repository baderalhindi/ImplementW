using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ExternalParticipation;

/// <summary>
/// One attempt to apply an accepted contribution revision to its source record through its typed adapter (WF-13 §7.2, SCR-166;
/// TASK-066). The attempt revalidates the source against the version the revision expects and either applies the allowlisted change in
/// its own transaction (APPLIED), finds the source changed and applies nothing (CONFLICT), or is refused by the source's own rules and
/// applies nothing (FAILED). A revision is applied at most once. Delete policy: RETAIN.
/// </summary>
public sealed class SourceApplication : AuditedEntity
{
    public Guid ExternalContributionId { get; set; }

    public int AttemptNo { get; set; }

    /// <summary>The caller and their <c>Idempotency-Key</c>: a retry of the same attempt finds it and applies nothing again (R-37).</summary>
    public required string IdempotencyKey { get; set; }

    public Guid CorrelationId { get; set; }

    public SourceApplicationStatus Status { get; set; }

    /// <summary>The source record's row version the attempt expected: the submitted revision's, or the one a revalidation confirmed.</summary>
    public long? ExpectedTargetRevisionNo { get; set; }

    /// <summary>The source record's row version a CONFLICT attempt found.</summary>
    public long? ActualTargetRevisionNo { get; set; }

    public Guid AttemptedByUserId { get; set; }

    public DateTimeOffset AttemptedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    /// <summary>The safe code of a FAILED attempt's refusal (WF-13 EXT-F-156).</summary>
    public string? FailureCode { get; set; }

    /// <summary>When an AHDA user confirmed that the accepted values still apply to the source as it now is (WF-13 US-EXT-REV-017).</summary>
    public DateTimeOffset? RevalidatedAt { get; set; }

    public Guid? RevalidatedByUserId { get; set; }

    /// <summary>The source record's row version the revalidation confirmed: what the next attempt expects.</summary>
    public long? RevalidatedTargetRevisionNo { get; set; }
}
