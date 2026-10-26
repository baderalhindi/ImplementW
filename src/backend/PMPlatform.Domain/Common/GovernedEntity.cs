namespace PMPlatform.Domain.Common;

/// <summary>
/// An authored, published row under the governed lifecycle primitive (ERD D-12): its state and who validated and
/// published it. Author (<see cref="AuditedEntity.CreatedBy"/>), reviewer and publisher must differ (TASK-110).
/// </summary>
public abstract class GovernedEntity : AuditedEntity
{
    public GovernedLifecycleState LifecycleState { get; set; }

    public Guid? ValidatedByUserId { get; set; }

    public DateTimeOffset? ValidatedAt { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? RetiredAt { get; set; }
}
