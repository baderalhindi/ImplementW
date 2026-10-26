using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>The governed lifecycle columns of a row (ERD D-12): its state, and who authored, validated and published it, and when.</summary>
public sealed record GovernedRecord(
    GovernedLifecycleState LifecycleState,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    Guid? ValidatedByUserId,
    DateTimeOffset? ValidatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? RetiredAt)
{
    public static GovernedRecord Of(GovernedEntity row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new GovernedRecord(
            row.LifecycleState, row.CreatedAt, row.CreatedBy, row.UpdatedAt, row.UpdatedBy,
            row.ValidatedByUserId, row.ValidatedAt, row.PublishedByUserId, row.PublishedAt, row.RetiredAt);
    }
}
