using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <inheritdoc cref="IEntityCompletionRates"/>
internal sealed class EntityCompletionService(
    IExternalParticipationRepository repository,
    ExternalParticipationAccess access,
    IAuthorizationEngine engine,
    IProjectFactsReader projects,
    TimeProvider timeProvider) : IEntityCompletionRates
{
    public async Task<EntityCompletionPage> ListAsync(Guid callerId, CompletionPeriod period, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        IReadOnlyList<RequestCompletionFacts> all = await ReadAsync(period, cancellationToken).ConfigureAwait(false);
        HashSet<Guid> reached = [.. (await projects.ListReachedAsync(await access.ViewScopeAsync(callerId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false)).Select(p => p.Id)];
        Guid? ownEntity = (await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false))?.ExternalEntityId;
        List<EntityCompletionRow> rows = EntityCompletionRule.Rows([.. all.Where(r => reached.Contains(r.ProjectId) && (ownEntity is null || r.ExternalEntityId == ownEntity))], Today());
        return new EntityCompletionPage([.. rows.Skip(page.Skip).Take(page.PageSize)], page.Page, page.PageSize, rows.Count, EntityCompletionRule.Average(all, Today()));
    }

    public async Task<OwnEntityCompletion?> FindOwnAsync(Guid callerId, CompletionPeriod period, CancellationToken cancellationToken)
    {
        if (await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false) is not { IsActive: true, ExternalEntityId: { } entityId })
        {
            return null;
        }

        IReadOnlyList<RequestCompletionFacts> all = await ReadAsync(period, cancellationToken).ConfigureAwait(false);
        EntityCompletionRow? own = EntityCompletionRule.Rows(all, Today()).SingleOrDefault(r => r.ExternalEntityId == entityId);
        return new OwnEntityCompletion(own?.Issued ?? 0, own?.Accepted ?? 0, own?.CompletionPercent, EntityCompletionRule.Average(all, Today()));
    }

    private Task<IReadOnlyList<RequestCompletionFacts>> ReadAsync(CompletionPeriod period, CancellationToken cancellationToken) =>
        repository.ListCompletionFactsAsync(EntityCompletionRule.PeriodStart(period, Today()), cancellationToken);

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}

/// <summary>The calculation behind <see cref="IEntityCompletionRates"/>.</summary>
internal static class EntityCompletionRule
{
    private const int Places = 2;

    public static DateOnly PeriodStart(CompletionPeriod period, DateOnly today) => period switch
    {
        CompletionPeriod.ThisMonth => new DateOnly(today.Year, today.Month, 1),
        CompletionPeriod.LastThreeMonths => new DateOnly(today.Year, today.Month, 1).AddMonths(-2),
        CompletionPeriod.ThisYear => new DateOnly(today.Year, 1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown period."),
    };

    /// <summary>One row per entity, lowest rate first, then most overdue.</summary>
    public static List<EntityCompletionRow> Rows(IEnumerable<RequestCompletionFacts> facts, DateOnly today) =>
        [.. facts
            .GroupBy(r => r.ExternalEntityId)
            .Select(g => new EntityCompletionRow(
                g.Key,
                g.Count(),
                g.Count(r => r.HasSubmission),
                g.Count(IsAccepted),
                g.Count(r => r.DueDate < today && !r.HasSubmission),
                Percent(g.Count(IsAccepted), g.Count())))
            .OrderBy(r => r.CompletionPercent)
            .ThenByDescending(r => r.Overdue)
            .ThenBy(r => r.ExternalEntityId)];

    /// <summary>The simple average of each entity's rate; null when no entity has a request in the period.</summary>
    public static decimal? Average(IEnumerable<RequestCompletionFacts> facts, DateOnly today)
    {
        List<EntityCompletionRow> rows = Rows(facts, today);
        return rows.Count == 0 ? null : Math.Round(rows.Average(r => r.CompletionPercent), Places, MidpointRounding.AwayFromZero);
    }

    private static bool IsAccepted(RequestCompletionFacts r) =>
        r.LatestContribution is ExternalContributionStatus.AcceptedPendingApplication or ExternalContributionStatus.Applied or ExternalContributionStatus.ApplicationFailed;

    private static decimal Percent(int part, int whole) => Math.Round(part * 100m / whole, Places, MidpointRounding.AwayFromZero);
}
