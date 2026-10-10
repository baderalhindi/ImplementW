using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>The <c>dashboards</c> schema (TASK-069). Finds that return rows to change track them; the others do not.</summary>
public interface IDashboardRepository
{
    /// <summary>Each dashboard's PUBLISHED version. Not tracked.</summary>
    public Task<IReadOnlyList<DashboardDefinition>> ListPublishedAsync(CancellationToken cancellationToken);

    /// <summary>The dashboard's PUBLISHED version; tracked when <paramref name="track"/>.</summary>
    public Task<DashboardDefinition?> FindPublishedAsync(DashboardCode code, bool track, CancellationToken cancellationToken);

    /// <summary>One page of versions, the newest of each dashboard first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<DashboardDefinition> Items, int TotalCount)> PageAsync(DashboardDefinitionQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<DashboardDefinition?> FindAsync(Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The dashboard's version on its way, DRAFT or VALIDATED, if any. Not tracked.</summary>
    public Task<DashboardDefinition?> FindOpenAsync(DashboardCode code, CancellationToken cancellationToken);

    /// <summary>The dashboard's highest version number; 0 before its first.</summary>
    public Task<int> MaxVersionNoAsync(DashboardCode code, CancellationToken cancellationToken);

    /// <summary>A version's widgets in layout order; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<DashboardWidget>> ListWidgetsAsync(Guid definitionId, bool track, CancellationToken cancellationToken);

    /// <summary>The audience of the versions named; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<DashboardAudienceRole>> ListAudienceAsync(IReadOnlyCollection<Guid> definitionIds, bool track, CancellationToken cancellationToken);

    /// <summary>A person's preference on a version. Tracked.</summary>
    public Task<UserDashboardPreference?> FindPreferenceAsync(Guid userId, Guid definitionId, CancellationToken cancellationToken);

    /// <summary>A preference's widget choices; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<UserDashboardWidgetPreference>> ListWidgetPreferencesAsync(Guid preferenceId, bool track, CancellationToken cancellationToken);

    /// <summary>How many versions are in each lifecycle state, for DSH-001's configuration backlog.</summary>
    public Task<IReadOnlyDictionary<GovernedLifecycleState, int>> CountByStateAsync(CancellationToken cancellationToken);

    /// <summary>The row version of a tracked definition, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(DashboardDefinition definition);

    public void Add(DashboardDefinition definition);

    public void Add(DashboardWidget widget);

    public void Add(DashboardAudienceRole audience);

    public void Add(UserDashboardPreference preference);

    public void Add(UserDashboardWidgetPreference widgetPreference);

    /// <summary>A DRAFT's widget, replaced as part of its content.</summary>
    public void Remove(DashboardWidget widget);

    /// <summary>A DRAFT's audience row, replaced as part of its content.</summary>
    public void Remove(DashboardAudienceRole audience);

    /// <summary>HARD_OWNER: a reset; its widget choices go with it.</summary>
    public void Remove(UserDashboardPreference preference);

    public void Remove(UserDashboardWidgetPreference widgetPreference);

    /// <summary>
    /// Saves the tracked changes and the audit events this unit of work staged. A row changed since it was read, and a key another
    /// request took first — a version number, the version on its way, the one PUBLISHED version — are answers, not faults; after either
    /// nothing stays tracked.
    /// </summary>
    public Task<DashboardSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

public enum DashboardSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key, or the at-commit one-PUBLISHED-version and one-default-landing checks, refused the save.</summary>
    Duplicate = 3,
}
