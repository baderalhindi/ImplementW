using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ExternalParticipation.Contracts;

/// <summary>
/// How entities answer their update requests (review round 5, items 9 and 10). One definition, so an entity and AHDA read the same number:
/// requests the entity submitted and AHDA accepted, divided by those issued to it in the period; cancelled and withdrawn requests are left out.
/// The average of all entities is the simple average of each entity's rate, over every entity issued a request in the period.
/// </summary>
public interface IEntityCompletionRates
{
    /// <summary>One row per entity with requests in the projects the caller's EXTERNAL_REQUEST_VIEW reaches, lowest rate first.</summary>
    public Task<EntityCompletionPage> ListAsync(Guid callerId, CompletionPeriod period, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The caller's own entity beside the average, and no other entity's name or rate. Null when the caller belongs to no entity.</summary>
    public Task<OwnEntityCompletion?> FindOwnAsync(Guid callerId, CompletionPeriod period, CancellationToken cancellationToken);
}

public enum CompletionPeriod
{
    ThisMonth = 1,
    LastThreeMonths = 2,
    ThisYear = 3,
}

public sealed record EntityCompletionRow(Guid ExternalEntityId, int Issued, int Submitted, int Accepted, int Overdue, decimal CompletionPercent);

public sealed record EntityCompletionPage(IReadOnlyList<EntityCompletionRow> Items, int Page, int PageSize, int TotalCount, decimal? AveragePercent);

/// <summary>A null rate: nothing was issued to the entity in the period, which is not 0%.</summary>
public sealed record OwnEntityCompletion(int Issued, int Accepted, decimal? CompletionPercent, decimal? AveragePercent);
