using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// SCR-139 (FG-02 §10.2; ADR-019): saved views. A view holds configuration only: parameter values, or allowlist entries with their sorts and
/// filters, never a row (BR-RPT-031). It is its owner's, and private unless the owner shares it with ADR-019's saved-view roles (TBC-RPT-20, enabled
/// by TASK-072): a holder of such a role may read and run it, never change it; anyone else learns nothing of it (R-47). The configuration transfers
/// and the authorisation does not — every read says whether the view still fits the report version and the allowlist in force for the reader,
/// naming what no longer does (SAV-012), and every run plans it again and runs it as the reader may see now (BR-RPT-033). Saving is
/// <c>REPORT_COMPOSE</c>'s, which ADR-019 gives R02, R03 and R07; an external entity's person saves no view and is shared none.
/// </summary>
internal sealed class SavedViewService(
    IReportRepository repository,
    ReportAccess access,
    ReportFields fields,
    ReportExecutor executor,
    ReportExplorerService explorer,
    IReportAllowlistReader allowlists,
    IAuditTrail audit,
    TimeProvider timeProvider) : ISavedViewService
{
    public async Task<SavedViewPage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        IReadOnlyDictionary<Guid, string> roleCodes = await access.RoleCodesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<Guid> recipientRoles = RecipientRoleIds(await access.CallerAsync(callerId, cancellationToken).ConfigureAwait(false), roleCodes);
        (IReadOnlyList<SavedView> items, int total) = await repository.PageViewsAsync(callerId, recipientRoles, page, cancellationToken).ConfigureAwait(false);
        ILookup<Guid, SavedViewShare> shares = (await repository.ListViewSharesAsync([.. items.Select(v => v.Id)], track: false, cancellationToken).ConfigureAwait(false))
            .ToLookup(s => s.SavedViewId);
        List<SavedViewSummary> summaries = [];
        foreach (SavedView view in items)
        {
            ReportDefinition? definition = view.ReportDefinitionId is { } id ? await repository.ReadDefinitionAsync(id, cancellationToken).ConfigureAwait(false) : null;
            (SavedViewCompatibility compatibility, _) = await CompatibilityAsync(callerId, view, cancellationToken).ConfigureAwait(false);
            summaries.Add(new SavedViewSummary(
                view.Id, view.OwnerUserId, view.ViewType, view.Name, definition?.Code, SharedWith(callerId, view, shares[view.Id], roleCodes), compatibility, view.UpdatedAt));
        }

        return new SavedViewPage(summaries, page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<SavedViewDetail>>> GetAsync(Guid callerId, Guid savedViewId, CancellationToken cancellationToken) =>
        await VisibleAsync(callerId, savedViewId, cancellationToken).ConfigureAwait(false) is { } view
            ? await VersionedAsync(callerId, view, cancellationToken).ConfigureAwait(false)
            : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<SavedViewDetail>>> CreateAsync(Guid callerId, SavedViewInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        AdministrationResult<Content> content = await ContentAsync(callerId, input, cancellationToken).ConfigureAwait(false);
        if (!content.Succeeded)
        {
            return content.Error;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        SavedView view = new()
        {
            Id = Guid.CreateVersion7(now),
            OwnerUserId = callerId,
            ViewType = input.ViewType,
            ReportDefinitionId = content.Value.Definition?.Id,
            Name = input.Name,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(view);
        AddChildren(view, content.Value, callerId, now);
        audit.Stage(ReportAudit.SavedViewCreated(callerId, view, input.SharedWithRoleCodes));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ReportSaveOutcome.Saved
            ? await VersionedAsync(callerId, view, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<Versioned<SavedViewDetail>>> UpdateAsync(
        Guid callerId, Guid savedViewId, SavedViewInput input, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (await OwnAsync(callerId, savedViewId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } view)
        {
            return AdministrationError.NotFound;
        }

        if (input.ViewType != view.ViewType)
        {
            return AdministrationError.Rule(ReportErrorCodes.SavedViewNotAllowed, new FieldIssue("viewType", FieldIssue.NotAllowed));
        }

        AdministrationResult<Content> content = await ContentAsync(callerId, input, cancellationToken).ConfigureAwait(false);
        if (!content.Succeeded)
        {
            return content.Error;
        }

        foreach (SavedViewColumn column in await repository.ListViewColumnsAsync(view.Id, track: true, cancellationToken).ConfigureAwait(false))
        {
            repository.Remove(column);
        }

        foreach (SavedViewFilter filter in await repository.ListViewFiltersAsync(view.Id, track: true, cancellationToken).ConfigureAwait(false))
        {
            repository.Remove(filter);
        }

        foreach (ReportParameterValue value in await repository.ListViewParametersAsync(view.Id, track: true, cancellationToken).ConfigureAwait(false))
        {
            repository.Remove(value);
        }

        foreach (SavedViewShare share in await repository.ListViewSharesAsync([view.Id], track: true, cancellationToken).ConfigureAwait(false))
        {
            repository.Remove(share);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        view.Name = input.Name;
        view.ReportDefinitionId = content.Value.Definition?.Id;
        view.UpdatedAt = now;
        view.UpdatedBy = callerId;
        AddChildren(view, content.Value, callerId, now);
        audit.Stage(ReportAudit.SavedViewChanged(callerId, view, input.SharedWithRoleCodes));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ReportSaveOutcome.Saved
            ? await VersionedAsync(callerId, view, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationError?> DeleteAsync(Guid callerId, Guid savedViewId, CancellationToken cancellationToken)
    {
        if (await OwnAsync(callerId, savedViewId, null, cancellationToken).ConfigureAwait(false) is not { } view)
        {
            return AdministrationError.NotFound;
        }

        repository.Remove(view);
        audit.Stage(ReportAudit.SavedViewChanged(callerId, view, []));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ReportSaveOutcome.Saved ? null : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<ReportResultPage>> RunAsync(Guid callerId, Guid savedViewId, PageRequest page, CancellationToken cancellationToken)
    {
        if (await VisibleAsync(callerId, savedViewId, cancellationToken).ConfigureAwait(false) is not { } view)
        {
            return AdministrationError.NotFound;
        }

        if (await CompatibilityAsync(callerId, view, cancellationToken).ConfigureAwait(false) is { Issues.Count: > 0 } incompatible)
        {
            return AdministrationError.Rule(ReportErrorCodes.SavedViewIncompatible, [.. incompatible.Issues.Select(i => new FieldIssue(i.Field, i.Code))]);
        }

        Saved saved = await SavedAsync(view, cancellationToken).ConfigureAwait(false);
        if (view.ViewType == SavedViewType.ExplorerComposition)
        {
            return await explorer.RunAsync(callerId, new ExplorerRunInput(saved.Columns, saved.Filters, saved.Sort), page, cancellationToken).ConfigureAwait(false);
        }

        ReportDefinition definition = (await repository.ReadDefinitionAsync(view.ReportDefinitionId!.Value, cancellationToken).ConfigureAwait(false))!;
        ReportDefinition current = (await access.OpenAsync(callerId, definition.Code, cancellationToken).ConfigureAwait(false))!;
        ReportParts parts = await access.PartsAsync(current.Id, cancellationToken).ConfigureAwait(false);
        ReportRunInput input = new(saved.Parameters, null, []);
        AdministrationResult<ReportPlan> plan = ReportQueryBuilder.ForReport(fields, current.Code, current.PrimaryProjectionCode, parts.Columns, parts.Parameters, input);
        if (!plan.Succeeded)
        {
            return plan.Error;
        }

        UserRoles caller = await access.CallerAsync(callerId, cancellationToken).ConfigureAwait(false);
        ReportExecution execution = await executor.ExecuteAsync(callerId, caller.IsExternal, plan.Value, [], null, cancellationToken).ConfigureAwait(false);
        return execution.IsNarrowingAuthorized
            ? ReportExecutor.Page(execution, [.. plan.Value.ShownColumns.Select(i => ReportExecutor.View(plan.Value.Columns[i]))], current.Code, current.VersionNo, page)
            : ReportService.Unauthorized(parts, input);
    }

    /// <summary>
    /// What a view holds once validated: for a report's parameters, the report version in force the caller may run, which allows saved views, and
    /// parameters that plan; for a composition, the allowlist entries that plan now; and the roles it is shared with, each one of ADR-019's
    /// saved-view roles, once.
    /// </summary>
    private async Task<AdministrationResult<Content>> ContentAsync(Guid callerId, SavedViewInput input, CancellationToken cancellationToken)
    {
        if ((await access.CallerAsync(callerId, cancellationToken).ConfigureAwait(false)).IsExternal)
        {
            return AdministrationError.Rule(ReportErrorCodes.SavedViewNotAllowed);
        }

        FieldIssue[] shareIssues = [.. input.SharedWithRoleCodes
            .Select((code, i) => (code, i))
            .Where(x => !ReportCatalogue.SavedViewRoles.Contains(x.code) || input.SharedWithRoleCodes.Take(x.i).Contains(x.code, StringComparer.Ordinal))
            .Select(x => new FieldIssue($"sharedWithRoleCodes[{x.i}]", FieldIssue.NotAllowed))];
        if (shareIssues.Length > 0)
        {
            return AdministrationError.Rule(ReportErrorCodes.SavedViewNotAllowed, shareIssues);
        }

        IReadOnlyList<Guid> shareRoleIds = [.. (await access.RoleCodesAsync(cancellationToken).ConfigureAwait(false))
            .Where(r => input.SharedWithRoleCodes.Contains(r.Value, StringComparer.Ordinal)).Select(r => r.Key)];

        if (input.ViewType == SavedViewType.ExplorerComposition)
        {
            if (input.ReportCode is not null || input.Parameters.Count > 0)
            {
                return AdministrationError.Rule(ReportErrorCodes.SavedViewNotAllowed, new FieldIssue(input.ReportCode is not null ? "reportCode" : "parameters", FieldIssue.NotAllowed));
            }

            ExplorerRunInput composition = new(
                [.. input.Columns.Select(c => new ReportFieldReference(c.SourceEntityCode, c.FieldCode))],
                input.Filters,
                [.. input.Columns.Where(c => c.SortDirection is not null).Select(c => new ReportSortInput(c.SourceEntityCode, c.FieldCode, c.SortDirection!.Value))]);
            AdministrationResult<ReportPlan> plan = await explorer.PlanAsync(callerId, composition, cancellationToken).ConfigureAwait(false);
            if (!plan.Succeeded)
            {
                return plan.Error;
            }

            ReportAllowlist allowlist = await allowlists.ResolveAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            Guid EntryOf(string entity, string field) => allowlist.Entries.Single(e => e.SourceEntityCode == entity && e.FieldCode == field).Id;
            return new Content(
                null,
                [.. input.Columns.Select(c => (EntryOf(c.SourceEntityCode, c.FieldCode), c.SortDirection))],
                [.. input.Filters.Select(f => (EntryOf(f.SourceEntityCode, f.FieldCode), f.Operator, f.Value))],
                [],
                shareRoleIds);
        }

        if (input.ReportCode is not { } code || input.Columns.Count > 0 || input.Filters.Count > 0)
        {
            return AdministrationError.Rule(ReportErrorCodes.SavedViewNotAllowed,
                new FieldIssue(input.ReportCode is null ? "reportCode" : input.Columns.Count > 0 ? "columns" : "filters", input.ReportCode is null ? FieldIssue.Required : FieldIssue.NotAllowed));
        }

        if (await access.OpenAsync(callerId, code, cancellationToken).ConfigureAwait(false) is not { AllowsSavedViews: true } definition)
        {
            return AdministrationError.Rule(ReportErrorCodes.SavedViewNotAllowed, new FieldIssue("reportCode", FieldIssue.NotAllowed));
        }

        ReportParts parts = await access.PartsAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        AdministrationResult<ReportPlan> planned = ReportQueryBuilder.ForReport(fields, code, definition.PrimaryProjectionCode, parts.Columns, parts.Parameters, new ReportRunInput(input.Parameters, null, []));
        return planned.Succeeded ? new Content(definition, [], [], [.. input.Parameters.Select(p => (p.Code, p.Value))], shareRoleIds) : planned.Error;
    }

    /// <summary>
    /// Whether the view still fits (SAV-012): a report's view, the report's version in force — one the owner may still run — and its parameters;
    /// a composition, entries still named by the allowlist in force, planning still. Each misfit is named, never silently reinterpreted.
    /// </summary>
    private async Task<(SavedViewCompatibility Compatibility, IReadOnlyList<SavedViewIssue> Issues)> CompatibilityAsync(Guid callerId, SavedView view, CancellationToken cancellationToken)
    {
        Saved saved = await SavedAsync(view, cancellationToken).ConfigureAwait(false);
        List<SavedViewIssue> issues = [];
        if (view.ViewType == SavedViewType.ExplorerComposition)
        {
            try
            {
                AdministrationResult<ReportPlan> plan = await explorer.PlanAsync(callerId, new ExplorerRunInput(saved.Columns, saved.Filters, saved.Sort), cancellationToken).ConfigureAwait(false);
                issues.AddRange(plan.Succeeded ? [] : plan.Error.Fields.Select(f => new SavedViewIssue(f.Field, f.Code)).DefaultIfEmpty(new SavedViewIssue("view", FieldIssue.NotAllowed)));
            }
            catch (ConfigurationMissingException)
            {
                issues.Add(new SavedViewIssue("view", ReportIssueCodes.FieldNotAllowlisted));
            }
        }
        else
        {
            ReportDefinition? savedAgainst = await repository.ReadDefinitionAsync(view.ReportDefinitionId!.Value, cancellationToken).ConfigureAwait(false);
            if (savedAgainst is null || await access.OpenAsync(callerId, savedAgainst.Code, cancellationToken).ConfigureAwait(false) is not { } current)
            {
                issues.Add(new SavedViewIssue("reportCode", FieldIssue.NotFound));
            }
            else
            {
                ReportParts parts = await access.PartsAsync(current.Id, cancellationToken).ConfigureAwait(false);
                AdministrationResult<ReportPlan> plan = ReportQueryBuilder.ForReport(
                    fields, current.Code, current.PrimaryProjectionCode, parts.Columns, parts.Parameters, new ReportRunInput(saved.Parameters, null, []));
                issues.AddRange(plan.Succeeded ? [] : plan.Error.Fields.Select(f => new SavedViewIssue(f.Field, f.Code)));
            }
        }

        return (issues.Count == 0 ? SavedViewCompatibility.Valid : SavedViewCompatibility.Incompatible, issues);
    }

    /// <summary>
    /// The view's children as a request, its entries named by their entity and field: the REPORT_RULES version they were saved against may since
    /// have been superseded, and planning against the version in force decides whether each is still allowlisted.
    /// </summary>
    private async Task<Saved> SavedAsync(SavedView view, CancellationToken cancellationToken)
    {
        IReadOnlyList<SavedViewColumn> columns = [.. (await repository.ListViewColumnsAsync(view.Id, track: false, cancellationToken).ConfigureAwait(false)).OrderBy(c => c.SortOrder)];
        IReadOnlyList<SavedViewFilter> filters = await repository.ListViewFiltersAsync(view.Id, track: false, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ReportParameterValue> parameters = await repository.ListViewParametersAsync(view.Id, track: false, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, ReportAllowlistEntryReference> entries = (await allowlists.FindAsync(
            [.. columns.Select(c => c.ReportAllowlistEntryId).Concat(filters.Select(f => f.ReportAllowlistEntryId)).Distinct()], cancellationToken).ConfigureAwait(false))
            .ToDictionary(e => e.Id);
        return new Saved(
            [.. parameters.Select(p => new ReportParameterInput(p.ParameterCode, p.ValueText))],
            [.. columns.Select(c => new ReportFieldReference(entries[c.ReportAllowlistEntryId].SourceEntityCode, entries[c.ReportAllowlistEntryId].FieldCode))],
            [.. filters.Select(f => new ReportFilterInput(entries[f.ReportAllowlistEntryId].SourceEntityCode, entries[f.ReportAllowlistEntryId].FieldCode, f.Operator, f.ValueText))],
            [.. columns.Where(c => c.SortDirection is not null)
                .Select(c => new ReportSortInput(entries[c.ReportAllowlistEntryId].SourceEntityCode, entries[c.ReportAllowlistEntryId].FieldCode, c.SortDirection!.Value))]);
    }

    /// <summary>The view, if the caller owns it: the only one who may change or delete it.</summary>
    private async Task<SavedView?> OwnAsync(Guid callerId, Guid savedViewId, uint? expectedVersion, CancellationToken cancellationToken) =>
        await repository.FindViewAsync(savedViewId, expectedVersion, cancellationToken).ConfigureAwait(false) is { } view && view.OwnerUserId == callerId ? view : null;

    /// <summary>The view, if the caller owns it or holds a role it is shared with: the readers who may open and run it.</summary>
    private async Task<SavedView?> VisibleAsync(Guid callerId, Guid savedViewId, CancellationToken cancellationToken)
    {
        if (await repository.FindViewAsync(savedViewId, null, cancellationToken).ConfigureAwait(false) is not { } view)
        {
            return null;
        }

        if (view.OwnerUserId == callerId)
        {
            return view;
        }

        IReadOnlyCollection<Guid> recipientRoles = RecipientRoleIds(
            await access.CallerAsync(callerId, cancellationToken).ConfigureAwait(false), await access.RoleCodesAsync(cancellationToken).ConfigureAwait(false));
        return (await repository.ListViewSharesAsync([view.Id], track: false, cancellationToken).ConfigureAwait(false)).Any(s => recipientRoles.Contains(s.SharedWithRoleId))
            ? view
            : null;
    }

    /// <summary>The roles through which views are shared with the caller: those they hold, none for an external entity's person (ADR-013).</summary>
    private static IReadOnlyCollection<Guid> RecipientRoleIds(UserRoles caller, IReadOnlyDictionary<Guid, string> roleCodes) =>
        caller.IsExternal ? [] : [.. roleCodes.Where(r => caller.RoleCodes.Contains(r.Value, StringComparer.Ordinal)).Select(r => r.Key)];

    /// <summary>The roles a view is shared with, for its owner; a person it is shared with is not told who else it is shared with.</summary>
    private static IReadOnlyList<string> SharedWith(Guid callerId, SavedView view, IEnumerable<SavedViewShare> shares, IReadOnlyDictionary<Guid, string> roleCodes) =>
        view.OwnerUserId == callerId ? [.. shares.Select(s => roleCodes[s.SharedWithRoleId]).Order(StringComparer.Ordinal)] : [];

    private async Task<Versioned<SavedViewDetail>> VersionedAsync(Guid callerId, SavedView view, CancellationToken cancellationToken)
    {
        Saved saved = await SavedAsync(view, cancellationToken).ConfigureAwait(false);
        (SavedViewCompatibility compatibility, IReadOnlyList<SavedViewIssue> issues) = await CompatibilityAsync(callerId, view, cancellationToken).ConfigureAwait(false);
        ReportDefinition? definition = view.ReportDefinitionId is { } id ? await repository.ReadDefinitionAsync(id, cancellationToken).ConfigureAwait(false) : null;
        IReadOnlyList<SavedViewColumn> columns = [.. (await repository.ListViewColumnsAsync(view.Id, track: false, cancellationToken).ConfigureAwait(false)).OrderBy(c => c.SortOrder)];
        IReadOnlyList<SavedViewShare> shares = await repository.ListViewSharesAsync([view.Id], track: false, cancellationToken).ConfigureAwait(false);
        return new(
            new SavedViewDetail(
                view.Id, view.OwnerUserId, view.ViewType, view.Name, definition?.Code, definition?.Id, definition?.VersionNo, saved.Parameters,
                [.. saved.Columns.Zip(columns, (reference, column) => new SavedViewColumnInput(reference.SourceEntityCode, reference.FieldCode, column.SortDirection))],
                saved.Filters, SharedWith(callerId, view, shares, await access.RoleCodesAsync(cancellationToken).ConfigureAwait(false)),
                compatibility, issues, view.CreatedAt, view.UpdatedAt),
            repository.RowVersionOf(view));
    }

    private void AddChildren(SavedView view, Content content, Guid actorId, DateTimeOffset now)
    {
        short order = 0;
        foreach ((Guid entryId, ReportSortDirection? direction) in content.Columns)
        {
            repository.Add(new SavedViewColumn
            {
                Id = Guid.CreateVersion7(now),
                SavedViewId = view.Id,
                ReportAllowlistEntryId = entryId,
                SortOrder = ++order,
                SortDirection = direction,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        foreach ((Guid entryId, ReportFilterOperator op, string value) in content.Filters)
        {
            repository.Add(new SavedViewFilter
            {
                Id = Guid.CreateVersion7(now),
                SavedViewId = view.Id,
                ReportAllowlistEntryId = entryId,
                Operator = op,
                ValueText = value,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        foreach (Guid roleId in content.ShareRoleIds)
        {
            repository.Add(new SavedViewShare
            {
                Id = Guid.CreateVersion7(now),
                SavedViewId = view.Id,
                SharedWithRoleId = roleId,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        foreach ((string code, string value) in content.Parameters)
        {
            repository.Add(new ReportParameterValue
            {
                Id = Guid.CreateVersion7(now),
                SavedViewId = view.Id,
                ParameterCode = code,
                ValueText = value,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }
    }

    private sealed record Content(
        ReportDefinition? Definition,
        IReadOnlyList<(Guid EntryId, ReportSortDirection? Direction)> Columns,
        IReadOnlyList<(Guid EntryId, ReportFilterOperator Operator, string Value)> Filters,
        IReadOnlyList<(string Code, string Value)> Parameters,
        IReadOnlyList<Guid> ShareRoleIds);

    private sealed record Saved(
        IReadOnlyList<ReportParameterInput> Parameters,
        IReadOnlyList<ReportFieldReference> Columns,
        IReadOnlyList<ReportFilterInput> Filters,
        IReadOnlyList<ReportSortInput> Sort);
}
