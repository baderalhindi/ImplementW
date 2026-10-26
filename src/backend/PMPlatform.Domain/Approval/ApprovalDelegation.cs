using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Approval;

/// <summary>
/// A standing delegation from one internal user to another for a period (SCR-114). The delegate may act on what the
/// delegator could decide themselves, never more: a delegation conveys only the delegator's own authority, so a
/// delegation made by a delegate conveys nothing it received. Delete policy: RETAIN.
/// </summary>
public sealed class ApprovalDelegation : AuditedEntity
{
    public Guid DelegatorUserId { get; set; }

    /// <summary>Internal users only (ADR-013).</summary>
    public Guid DelegateUserId { get; set; }

    /// <summary>Null: every routing key.</summary>
    public string? RoutingKey { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset ValidTo { get; set; }

    public ApprovalDelegationStatus Status { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
