using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Projections;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Dashboards.Projections;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>
/// FG-01's runtime (TASK-069; FG-01 §24 Catalogue, Composition and Data Orchestrator). A person's roles select the PUBLISHED dashboards they
/// may open and the one they land on, and select nothing else. Each widget is then authorised on its own: the projects it may count are
/// those the caller reaches under the projection's source permission and the widget's classification, decided by the authorization engine
/// before any source is read (DSH-CC-04, -05; BR-DSH-025). One widget whose source fails answers SOURCE_UNAVAILABLE and the others still
/// render (BR-DSH-040). Nothing here reads another module's tables or computes a value a source owns.
/// </summary>
internal sealed partial class DashboardService(
    IDashboardRepository repository,
    IUserRoleDirectory userRoles,
    IRoleDirectory roles,
    IProjectFactsReader projects,
    IAuthorizationEngine engine,
    IEnumerable<IDashboardProjectionSource> sources,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<DashboardService> logger) : IDashboardService
{
    private readonly Dictionary<string, IDashboardProjectionSource> _sources = ProjectionSources.ByCode(sources);

    public async Task<DashboardCataloguePage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        UserRoles caller = await userRoles.FindAsync(callerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> callerRoles = caller.RoleCodes;
        IReadOnlyList<DashboardDefinition> published = [.. (await repository.ListPublishedAsync(cancellationToken).ConfigureAwait(false))
            .Where(d => DashboardCatalogue.MayOpen(d.Code, caller.IsExternal))];
        IReadOnlyDictionary<Guid, string> roleCodes = await RoleCodesAsync(cancellationToken).ConfigureAwait(false);
        ILookup<Guid, DashboardAudienceRole> audience = (await repository.ListAudienceAsync([.. published.Select(d => d.Id)], track: false, cancellationToken).ConfigureAwait(false))
            .Where(a => callerRoles.Contains(roleCodes[a.RoleId]))
            .ToLookup(a => a.DashboardDefinitionId);

        // TBC-DSH-003: with several roles, the landing of the caller's first role (in code order) that lands anywhere.
        Guid? landing = callerRoles
            .Select(role => audience.SelectMany(g => g).FirstOrDefault(a => a.IsDefaultLanding && roleCodes[a.RoleId] == role)?.DashboardDefinitionId)
            .FirstOrDefault(id => id is not null);
        List<DashboardCatalogueEntry> entries = [.. published.Where(d => audience[d.Id].Any()).Select(d => new DashboardCatalogueEntry(
            d.Code, d.Id, d.VersionNo, d.Name, d.Description, DashboardCatalogue.ContextOf(d.Code), d.AllowsPersonalization, d.Id == landing))];
        return new DashboardCataloguePage([.. entries.Skip(page.Skip).Take(page.PageSize)], page.Page, page.PageSize, entries.Count);
    }

    public async Task<AdministrationResult<DashboardView>> GetAsync(Guid callerId, DashboardCode code, DashboardContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (await OpenAsync(callerId, code, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        DashboardContextKind kind = DashboardCatalogue.ContextOf(code);
        if (ContextRefused(kind, context) is { } invalid)
        {
            return invalid;
        }

        IReadOnlyList<DashboardWidget> widgets = await repository.ListWidgetsAsync(definition.Id, track: false, cancellationToken).ConfigureAwait(false);
        Dictionary<string, RecordScope> scopes = [];
        foreach (string permission in widgets.Select(w => DashboardProjections.Find(w.SourceProjectionCode)?.PermissionCode).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            scopes[permission] = await engine.GetRecordScopeAsync(callerId, permission, cancellationToken).ConfigureAwait(false);
        }

        // The candidate projects: the one the Project Dashboard is bound to, or every project some widget's permission reaches.
        List<ProjectFacts> candidates;
        List<Guid> departments;
        if (kind == DashboardContextKind.Project)
        {
            ProjectFacts? project = await projects.FindAsync(context.ProjectId!.Value, cancellationToken).ConfigureAwait(false);
            if (project is null || !widgets.Any(w => Reaches(scopes, w, project)))
            {
                return AdministrationError.NotFound;
            }

            candidates = [project];
            departments = [];
        }
        else
        {
            RecordScope reach = new([.. widgets
                .Select(w => DashboardProjections.Find(w.SourceProjectionCode))
                .Where(p => p is { Eligibility: not ProjectionEligibility.NoProject })
                .Select(p => p!.PermissionCode).Distinct(StringComparer.Ordinal)
                .SelectMany(permission => scopes[permission].Clauses)]);
            candidates = [.. await projects.ListReachedAsync(reach, cancellationToken).ConfigureAwait(false)];
            departments = [.. candidates.Select(p => p.DepartmentId).Distinct().Order()];
            if (context.DepartmentId is { } departmentId)
            {
                if (!departments.Contains(departmentId))
                {
                    return AdministrationError.Rule(DashboardErrorCodes.FilterValueUnauthorized, new FieldIssue("departmentId", FieldIssue.NotAllowed));
                }

                candidates = [.. candidates.Where(p => p.DepartmentId == departmentId)];
            }
        }

        IReadOnlyDictionary<Guid, UserDashboardWidgetPreference> personal = await PersonalAsync(callerId, definition, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<DashboardWidgetResult> results = [];
        foreach (DashboardWidget widget in widgets)
        {
            ProjectionContract? projection = DashboardProjections.Find(widget.SourceProjectionCode);
            ProjectionReading reading = projection is null
                ? ProjectionReading.Unknown(WidgetUnknownReason.SourceUnavailable)
                : await ReadAsync(callerId, code, kind, widget, projection, scopes[projection.PermissionCode], candidates, now, cancellationToken).ConfigureAwait(false);
            personal.TryGetValue(widget.Id, out UserDashboardWidgetPreference? choice);
            results.Add(new DashboardWidgetResult(
                widget.Code,
                widget.Title,
                widget.WidgetType,
                widget.SourceProjectionCode,
                projection?.Version ?? string.Empty,
                projection?.SourceDomain ?? string.Empty,
                widget.LayoutRow,
                widget.LayoutColumn,
                widget.LayoutSpan,
                widget.IsOptionalVisibility,
                choice?.IsHidden ?? false,
                choice?.SortOrder,
                new ProjectionMeta(projection?.SemanticState ?? ProjectionSemanticState.CurrentLive, reading.Freshness, reading.AsOf, reading.Coverage),
                reading.UnknownReason,
                reading.Data,
                reading.CoverageDetail,
                reading.MaskedFields,
                reading.UnknownReason == WidgetUnknownReason.Restricted ? null : projection?.DrillTargetScreenId));
        }

        return new DashboardView(
            definition.Code, definition.Id, definition.VersionNo, definition.Name, definition.Description, kind, definition.AllowsPersonalization,
            context.ProjectId, context.DepartmentId, departments, now, results);
    }

    public async Task<AdministrationResult<DashboardPersonalizationDetail>> PersonalizeAsync(
        Guid callerId, DashboardCode code, DashboardPersonalizationInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (await OpenAsync(callerId, code, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (!definition.AllowsPersonalization)
        {
            return AdministrationError.Rule(DashboardErrorCodes.PersonalizationInvalid);
        }

        // DSH-CC-29: only the version's optional widgets, each once; a choice never reaches what a widget presents.
        Dictionary<string, DashboardWidget> optional = (await repository.ListWidgetsAsync(definition.Id, track: false, cancellationToken).ConfigureAwait(false))
            .Where(w => w.IsOptionalVisibility).ToDictionary(w => w.Code, StringComparer.Ordinal);
        HashSet<string> named = new(StringComparer.Ordinal);
        List<FieldIssue> issues = [];
        for (int i = 0; i < input.Widgets.Count; i++)
        {
            string widgetCode = input.Widgets[i].WidgetCode;
            if (!optional.ContainsKey(widgetCode))
            {
                issues.Add(new FieldIssue($"widgets[{i}].widgetCode", FieldIssue.NotAllowed));
            }
            else if (!named.Add(widgetCode))
            {
                issues.Add(new FieldIssue($"widgets[{i}].widgetCode", FieldIssue.Duplicate));
            }
        }

        if (issues.Count > 0)
        {
            return AdministrationError.Rule(DashboardErrorCodes.PersonalizationInvalid, [.. issues]);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        UserDashboardPreference? preference = await repository.FindPreferenceAsync(callerId, definition.Id, cancellationToken).ConfigureAwait(false);
        if (preference is null)
        {
            preference = new UserDashboardPreference
            {
                Id = Guid.CreateVersion7(now),
                UserId = callerId,
                DashboardDefinitionId = definition.Id,
                CreatedAt = now,
                CreatedBy = callerId,
                UpdatedAt = now,
                UpdatedBy = callerId,
            };
            repository.Add(preference);
        }
        else
        {
            foreach (UserDashboardWidgetPreference previous in await repository.ListWidgetPreferencesAsync(preference.Id, track: true, cancellationToken).ConfigureAwait(false))
            {
                repository.Remove(previous);
            }

            preference.UpdatedAt = now;
            preference.UpdatedBy = callerId;
        }

        foreach (WidgetPersonalizationInput choice in input.Widgets)
        {
            repository.Add(new UserDashboardWidgetPreference
            {
                Id = Guid.CreateVersion7(now),
                UserDashboardPreferenceId = preference.Id,
                DashboardWidgetId = optional[choice.WidgetCode].Id,
                IsHidden = choice.IsHidden,
                SortOrder = choice.SortOrder,
                CreatedAt = now,
                CreatedBy = callerId,
                UpdatedAt = now,
                UpdatedBy = callerId,
            });
        }

        audit.Stage(DashboardAudit.PersonalizationChanged(callerId, preference, definition, [.. input.Widgets.Where(w => w.IsHidden).Select(w => w.WidgetCode)]));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == DashboardSaveOutcome.Saved
            ? new DashboardPersonalizationDetail(code, definition.Id, [.. input.Widgets.Select(w => new WidgetPersonalizationDetail(w.WidgetCode, w.IsHidden, w.SortOrder))])
            : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<DashboardPersonalizationDetail>> ResetPersonalizationAsync(Guid callerId, DashboardCode code, CancellationToken cancellationToken)
    {
        if (await OpenAsync(callerId, code, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        DashboardPersonalizationDetail governed = new(code, definition.Id, []);
        if (await repository.FindPreferenceAsync(callerId, definition.Id, cancellationToken).ConfigureAwait(false) is not { } preference)
        {
            return governed;
        }

        repository.Remove(preference);
        audit.Stage(DashboardAudit.PersonalizationReset(callerId, preference, definition));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == DashboardSaveOutcome.Saved ? governed : AdministrationError.PreconditionFailed;
    }

    /// <summary>
    /// The dashboard's PUBLISHED version, if the caller's roles are its audience and ADR-019 lets them open it; otherwise, as if it did not
    /// exist (R-47).
    /// </summary>
    private async Task<DashboardDefinition?> OpenAsync(Guid callerId, DashboardCode code, CancellationToken cancellationToken)
    {
        if (await repository.FindPublishedAsync(code, track: false, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return null;
        }

        UserRoles caller = await userRoles.FindAsync(callerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, string> roleCodes = await RoleCodesAsync(cancellationToken).ConfigureAwait(false);
        return DashboardCatalogue.MayOpen(code, caller.IsExternal)
               && (await repository.ListAudienceAsync([definition.Id], track: false, cancellationToken).ConfigureAwait(false)).Any(a => caller.RoleCodes.Contains(roleCodes[a.RoleId]))
            ? definition
            : null;
    }

    /// <summary>
    /// One widget's reading. The projects it may count are the candidates its permission's scope reaches with its classification
    /// cleared, and of those, the ones the projection expects a value from. A caller who reaches none here learns nothing more.
    /// </summary>
    private async Task<ProjectionReading> ReadAsync(
        Guid callerId, DashboardCode code, DashboardContextKind kind, DashboardWidget widget, ProjectionContract projection, RecordScope scope,
        IReadOnlyList<ProjectFacts> candidates, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (scope.IsEmpty || !_sources.TryGetValue(projection.Code, out IDashboardProjectionSource? source))
        {
            return ProjectionReading.Unknown(scope.IsEmpty ? WidgetUnknownReason.Restricted : WidgetUnknownReason.SourceUnavailable);
        }

        List<ProjectFacts> reached = [];
        if (projection.Eligibility != ProjectionEligibility.NoProject)
        {
            reached = [.. candidates.Where(p => scope.Matches(ProjectSubjects.Of(p, widget.DataClassificationItemId)))];
            if (kind == DashboardContextKind.Project && reached.Count == 0)
            {
                return ProjectionReading.Unknown(WidgetUnknownReason.Restricted);
            }

            reached = [.. reached.Where(p => projection.IsEligible(p.Status))];
            if (reached.Count == 0)
            {
                return ProjectionReading.Unknown(WidgetUnknownReason.NotApplicable, kind == DashboardContextKind.Portfolio ? new WidgetCoverage(0, 0, 0, 0, []) : null);
            }
        }

        try
        {
            return await source.ReadAsync(new ProjectionRequest(callerId, kind, reached, now), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSourceUnavailable(logger, code, widget.Code, projection.Code, exception.GetType().Name);
            return ProjectionReading.Unknown(WidgetUnknownReason.SourceUnavailable);
        }
    }

    private static bool Reaches(Dictionary<string, RecordScope> scopes, DashboardWidget widget, ProjectFacts project) =>
        DashboardProjections.Find(widget.SourceProjectionCode) is { Eligibility: not ProjectionEligibility.NoProject } projection
        && scopes[projection.PermissionCode].Matches(ProjectSubjects.Of(project, widget.DataClassificationItemId));


    /// <summary>DSH-CC-18: the Project Dashboard is bound to a project and takes no department; the others take no project.</summary>
    private static AdministrationError? ContextRefused(DashboardContextKind kind, DashboardContext context) =>
        kind == DashboardContextKind.Project
            ? context.ProjectId is null ? AdministrationError.Rule(DashboardErrorCodes.ContextInvalid, new FieldIssue("projectId", FieldIssue.Required))
                : context.DepartmentId is not null ? AdministrationError.Rule(DashboardErrorCodes.ContextInvalid, new FieldIssue("departmentId", FieldIssue.NotAllowed))
                : null
            : context.ProjectId is not null ? AdministrationError.Rule(DashboardErrorCodes.ContextInvalid, new FieldIssue("projectId", FieldIssue.NotAllowed))
            : null;

    private async Task<IReadOnlyDictionary<Guid, UserDashboardWidgetPreference>> PersonalAsync(Guid callerId, DashboardDefinition definition, CancellationToken cancellationToken) =>
        definition.AllowsPersonalization && await repository.FindPreferenceAsync(callerId, definition.Id, cancellationToken).ConfigureAwait(false) is { } preference
            ? (await repository.ListWidgetPreferencesAsync(preference.Id, track: false, cancellationToken).ConfigureAwait(false)).ToDictionary(p => p.DashboardWidgetId)
            : [];

    private async Task<IReadOnlyDictionary<Guid, string>> RoleCodesAsync(CancellationToken cancellationToken) =>
        (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Id, r => r.Code);

    /// <summary>FG-01 §27.2 telemetry: which widget's source failed, never the data it was reading.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Dashboard {DashboardCode} widget {WidgetCode}: projection {ProjectionCode} is unavailable ({Reason}); the widget answers SOURCE_UNAVAILABLE.")]
    private static partial void LogSourceUnavailable(ILogger logger, DashboardCode dashboardCode, string widgetCode, string projectionCode, string reason);
}
