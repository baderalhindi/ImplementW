using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// FG-02's published reports at runtime (TASK-071; FG-02 §21 Report Catalogue, Query and Execution Services). A role selects a report and grants
/// nothing: the rows are the projects the caller reaches under the report's primary projection, each cell authorised on its own projection,
/// before any source is read (FG-02 §8). Parameters only narrow (BR-RPT-007); a project or a department the caller may not know is refused the
/// same as one that does not exist (BR-RPT-010). An export is a job; nothing is read when it is requested.
/// </summary>
internal sealed class ReportService(
    IReportRepository repository,
    ReportAccess access,
    ReportFields fields,
    ReportExecutor executor,
    ReportJobRequests jobs,
    IProjectionRowReader rows,
    IOrganizationDirectory organizations,
    IAuditTrail audit,
    TimeProvider timeProvider) : IReportService
{
    public async Task<ReportCataloguePage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        List<ReportCatalogueEntry> entries = [];
        foreach (ReportDefinition definition in await repository.ListPublishedAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await access.MayRunAsync(callerId, definition, cancellationToken).ConfigureAwait(false))
            {
                entries.Add(new ReportCatalogueEntry(
                    definition.Code, definition.Id, definition.VersionNo, definition.Name, definition.Description, definition.AudienceFamily, definition.AllowsSavedViews));
            }
        }

        return new ReportCataloguePage([.. entries.Skip(page.Skip).Take(page.PageSize)], page.Page, page.PageSize, entries.Count);
    }

    public async Task<AdministrationResult<ReportView>> GetAsync(Guid callerId, ReportCode code, CancellationToken cancellationToken)
    {
        if (await access.OpenAsync(callerId, code, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        ReportParts parts = await access.PartsAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        UserRoles caller = await access.CallerAsync(callerId, cancellationToken).ConfigureAwait(false);

        // The project and department options are those of the population the caller reaches here: never a value they may not know.
        ProjectionRowSet population = parts.Parameters.Any(p => p.Parameter.DataType is ReportParameterDataType.Project or ReportParameterDataType.Department)
            ? await rows.ReadAsync(new ProjectionRowQuery(callerId, definition.PrimaryProjectionCode, [], [], null, null, caller.IsExternal, timeProvider.GetUtcNow()), cancellationToken)
                .ConfigureAwait(false)
            : new ProjectionRowSet(true, [], [], []);
        OrganizationNames names = await organizations.ListNamesAsync(population.DepartmentOptions, [], cancellationToken).ConfigureAwait(false);
        List<ReportParameterView> parameters = [.. parts.Parameters.Select(p => new ReportParameterView(p.Parameter.Code, p.Parameter.Label, p.Parameter.DataType, p.Parameter.IsRequired,
            p.Parameter.DataType switch
            {
                ReportParameterDataType.Option => [.. p.Options.Select(o => new ReportParameterOptionView(o.ValueCode, o.Label, o.CatalogueEntryReference))],
                ReportParameterDataType.Department => [.. population.DepartmentOptions.Select(d =>
                    new ReportParameterOptionView(d.ToString(), names.Departments.GetValueOrDefault(d) ?? new BilingualLabel(d.ToString(), d.ToString()), null))],
                ReportParameterDataType.Project => [.. population.Rows.DistinctBy(r => r.ProjectId).Select(r =>
                    new ReportParameterOptionView(r.ProjectId.ToString(), ProjectLabel(r.FormalProjectId, r.Title), null))],
                _ => throw new InvalidOperationException($"{code}: parameter {p.Parameter.Code} has an unknown type."),
            }))];

        bool mayExport = await access.HoldsAsync(callerId, PermissionCatalogue.ReportExport, cancellationToken).ConfigureAwait(false);
        bool maySave = definition.AllowsSavedViews && !caller.IsExternal && await access.HoldsAsync(callerId, PermissionCatalogue.ReportCompose, cancellationToken).ConfigureAwait(false);
        return new ReportView(
            definition.Code, definition.Id, definition.VersionNo, definition.Name, definition.Description, definition.AudienceFamily, definition.AllowsSavedViews,
            [.. Columns(parts)], parameters, ReportCatalogue.ExportFormats, mayExport, maySave);
    }

    public async Task<AdministrationResult<ReportResultPage>> RunAsync(Guid callerId, ReportCode code, ReportRunInput input, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (await access.OpenAsync(callerId, code, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        ReportParts parts = await access.PartsAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        AdministrationResult<ReportPlan> plan = ReportQueryBuilder.ForReport(fields, definition.Code, definition.PrimaryProjectionCode, parts.Columns, parts.Parameters, input);
        if (!plan.Succeeded)
        {
            return plan.Error;
        }

        UserRoles caller = await access.CallerAsync(callerId, cancellationToken).ConfigureAwait(false);
        ReportExecution execution = await executor.ExecuteAsync(callerId, caller.IsExternal, plan.Value, [], null, cancellationToken).ConfigureAwait(false);
        if (!execution.IsNarrowingAuthorized)
        {
            return Unauthorized(parts, input);
        }

        if (execution.RevealsSensitive)
        {
            await audit.RecordAsync(ReportAudit.SensitiveReportExecuted(
                callerId, definition.Code, definition.Id, execution.Rows.Count, execution.Rows.Select(r => r.ProjectId).Distinct().Count())).ConfigureAwait(false);
        }

        return ReportExecutor.Page(execution, [.. plan.Value.ShownColumns.Select(i => ReportExecutor.View(plan.Value.Columns[i]))], definition.Code, definition.VersionNo, page);
    }

    public async Task<AdministrationResult<ReportExportOutcome>> ExportAsync(
        Guid callerId, ReportCode code, ReportRunInput input, ReportExportInput output, Guid idempotencyKey, Guid correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (await access.OpenAsync(callerId, code, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        ReportParts parts = await access.PartsAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        AdministrationResult<ReportPlan> plan = ReportQueryBuilder.ForReport(fields, definition.Code, definition.PrimaryProjectionCode, parts.Columns, parts.Parameters, input);
        if (!plan.Succeeded)
        {
            return plan.Error;
        }

        // The narrowing is checked now against the population alone; the rows are read when the job runs, as its requester may then.
        UserRoles caller = await access.CallerAsync(callerId, cancellationToken).ConfigureAwait(false);
        if (plan.Value.ProjectId is not null || plan.Value.DepartmentId is not null)
        {
            ProjectionRowSet population = await rows.ReadAsync(
                new ProjectionRowQuery(callerId, plan.Value.Basis.Code, [], [], plan.Value.ProjectId is { } p ? [p] : null, plan.Value.DepartmentId, caller.IsExternal,
                    timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
            if (!population.DepartmentOffered || (plan.Value.ProjectId is not null && population.DepartmentOptions.Count == 0))
            {
                return Unauthorized(parts, input);
            }
        }

        return await jobs.RequestAsync(
            callerId, ReportJobKind.Report, definition, ReportRequestSnapshot.Of(plan.Value, input.Parameters), output, idempotencyKey, correlationId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A report version's columns as the caller sees them offered.</summary>
    public IEnumerable<ReportColumnView> Columns(ReportParts parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return parts.Columns.OrderBy(c => c.SortOrder).Select(c => ReportExecutor.View(new PlannedColumn(
            fields.Find(c.SourceEntityCode, c.FieldCode) ?? throw new InvalidOperationException($"Column {c.SourceEntityCode}.{c.FieldCode} is not registered."),
            c.Label, c.DataClassificationItemId, IsShown: true, c.IsDefaultVisible)));
    }

    /// <summary>The project or department given is not one the caller may know (BR-RPT-010): named by its parameter, never echoed.</summary>
    public static AdministrationError Unauthorized(ReportParts parts, ReportRunInput input)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(input);
        HashSet<string> narrowing = [.. parts.Parameters.Where(p => p.Parameter.DataType is ReportParameterDataType.Project or ReportParameterDataType.Department).Select(p => p.Parameter.Code)];
        return AdministrationError.Rule(ReportErrorCodes.ParameterUnauthorized,
        [
            .. input.Parameters.Select((p, i) => (p, i)).Where(x => narrowing.Contains(x.p.Code)).Select(x => new FieldIssue($"parameters[{x.i}].value", FieldIssue.NotAllowed)),
        ]);
    }

    /// <summary>A project's option label: its Formal Project ID and its title as entered, the same in both languages (ADR-012).</summary>
    private static BilingualLabel ProjectLabel(string? formalProjectId, NarrativeText title)
    {
        string label = formalProjectId is null ? title.Text : $"{formalProjectId} — {title.Text}";
        return new BilingualLabel(label, label);
    }
}
