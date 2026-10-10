using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMPlatform.Application.Features.Dashboards;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Dashboards;

/// <summary>The <c>dashboards</c> schema (TASK-069).</summary>
internal sealed class DashboardRepository(PMPlatformDbContext context) : IDashboardRepository
{
    public async Task<IReadOnlyList<DashboardDefinition>> ListPublishedAsync(CancellationToken cancellationToken) =>
        await Definitions(track: false).Where(d => d.LifecycleState == GovernedLifecycleState.Published).OrderBy(d => d.Code).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<DashboardDefinition?> FindPublishedAsync(DashboardCode code, bool track, CancellationToken cancellationToken) =>
        Definitions(track).SingleOrDefaultAsync(d => d.Code == code && d.LifecycleState == GovernedLifecycleState.Published, cancellationToken);

    public async Task<(IReadOnlyList<DashboardDefinition> Items, int TotalCount)> PageAsync(DashboardDefinitionQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        IQueryable<DashboardDefinition> rows = Definitions(track: false);
        if (query.Code is { } code)
        {
            rows = rows.Where(d => d.Code == code);
        }

        if (query.LifecycleState is { } state)
        {
            rows = rows.Where(d => d.LifecycleState == state);
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<DashboardDefinition> items = await rows.OrderBy(d => d.Code).ThenByDescending(d => d.VersionNo)
            .Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<DashboardDefinition?> FindAsync(Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        DashboardDefinition? definition = await Definitions(track: true).SingleOrDefaultAsync(d => d.Id == definitionId, cancellationToken).ConfigureAwait(false);
        if (definition is not null)
        {
            AdministrationPersistence.ExpectVersion(context, definition, expectedVersion);
        }

        return definition;
    }

    public Task<DashboardDefinition?> FindOpenAsync(DashboardCode code, CancellationToken cancellationToken) =>
        Definitions(track: false).SingleOrDefaultAsync(
            d => d.Code == code && (d.LifecycleState == GovernedLifecycleState.Draft || d.LifecycleState == GovernedLifecycleState.Validated), cancellationToken);

    public async Task<int> MaxVersionNoAsync(DashboardCode code, CancellationToken cancellationToken) =>
        await Definitions(track: false).Where(d => d.Code == code).MaxAsync(d => (int?)d.VersionNo, cancellationToken).ConfigureAwait(false) ?? 0;

    public async Task<IReadOnlyList<DashboardWidget>> ListWidgetsAsync(Guid definitionId, bool track, CancellationToken cancellationToken) =>
        await Rows<DashboardWidget>(track).Where(w => w.DashboardDefinitionId == definitionId)
            .OrderBy(w => w.LayoutRow).ThenBy(w => w.LayoutColumn).ThenBy(w => w.Code).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<DashboardAudienceRole>> ListAudienceAsync(IReadOnlyCollection<Guid> definitionIds, bool track, CancellationToken cancellationToken) =>
        await Rows<DashboardAudienceRole>(track).Where(a => definitionIds.Contains(a.DashboardDefinitionId)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<UserDashboardPreference?> FindPreferenceAsync(Guid userId, Guid definitionId, CancellationToken cancellationToken) =>
        context.Set<UserDashboardPreference>().SingleOrDefaultAsync(p => p.UserId == userId && p.DashboardDefinitionId == definitionId, cancellationToken);

    public async Task<IReadOnlyList<UserDashboardWidgetPreference>> ListWidgetPreferencesAsync(Guid preferenceId, bool track, CancellationToken cancellationToken) =>
        await Rows<UserDashboardWidgetPreference>(track).Where(p => p.UserDashboardPreferenceId == preferenceId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<GovernedLifecycleState, int>> CountByStateAsync(CancellationToken cancellationToken) =>
        await Definitions(track: false).GroupBy(d => d.LifecycleState).Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.State, g => g.Count, cancellationToken).ConfigureAwait(false);

    public uint RowVersionOf(DashboardDefinition definition) =>
        context.Entry(definition).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public void Add(DashboardDefinition definition) => context.Add(definition);

    public void Add(DashboardWidget widget) => context.Add(widget);

    public void Add(DashboardAudienceRole audience) => context.Add(audience);

    public void Add(UserDashboardPreference preference) => context.Add(preference);

    public void Add(UserDashboardWidgetPreference widgetPreference) => context.Add(widgetPreference);

    public void Remove(DashboardWidget widget) => context.Remove(widget);

    public void Remove(DashboardAudienceRole audience) => context.Remove(audience);

    public void Remove(UserDashboardPreference preference) => context.Remove(preference);

    public void Remove(UserDashboardWidgetPreference widgetPreference) => context.Remove(widgetPreference);

    public async Task<DashboardSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return DashboardSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return DashboardSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A version number or the version on its way taken by another request first, or — at commit — a second PUBLISHED version of
            // a code or a second default landing of a role (TASK-069 migration 3).
            context.ChangeTracker.Clear();
            return DashboardSaveOutcome.Duplicate;
        }
    }

    private IQueryable<DashboardDefinition> Definitions(bool track) => Rows<DashboardDefinition>(track);

    private IQueryable<T> Rows<T>(bool track)
        where T : class =>
        track ? context.Set<T>() : context.Set<T>().AsNoTracking();
}
