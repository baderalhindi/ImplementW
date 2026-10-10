using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// SCR-138, the controlled report explorer (ADR-019; FG-02 §11.3, §13.3). Its query surface is the REPORT_RULES allowlist in force, resolved on
/// every request (a publication applies from its effective moment; resolution fails closed): a composition names allowlisted fields only, and the
/// projections they belong to are the only joins to the project row — the allowlist's own entities. Its rows are the projects the caller may see
/// under WF-01's view permission; every cell is then authorised on its own projection. ADR-019 gives it to R02, R03 and R07 through
/// <c>REPORT_COMPOSE</c>; an external entity's person never composes (ADR-013).
/// </summary>
internal sealed class ReportExplorerService(
    IReportAllowlistReader allowlists,
    ReportAccess access,
    ReportFields fields,
    ReportExecutor executor,
    ReportJobRequests jobs,
    IAuditTrail audit,
    TimeProvider timeProvider) : IReportExplorerService
{
    public async Task<ReportAllowlistEntryPage> ListFieldsAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ReportAllowlist allowlist = await allowlists.ResolveAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        List<ReportAllowlistEntryView> entries = [.. allowlist.Entries.Select(e => (Entry: e, Field: fields.Find(e.SourceEntityCode, e.FieldCode)))
            .Where(x => x.Field is not null)
            .Select(x => new ReportAllowlistEntryView(
                x.Entry.Id, x.Entry.SourceEntityCode, x.Entry.FieldCode, x.Entry.Label, x.Field!.Type, x.Entry.IsFilterable, x.Entry.IsSortable && x.Field.IsSortableType,
                x.Entry.IsFilterable ? x.Field.Operators : [], x.Field.Values, x.Field.Projection?.Code, x.Field.Projection?.SemanticState, x.Field.IsSensitive))];
        return new ReportAllowlistEntryPage(
            [.. entries.Skip(page.Skip).Take(page.PageSize)], page.Page, page.PageSize, entries.Count, allowlist.ConfigurationVersionId, allowlist.VersionNo);
    }

    public async Task<AdministrationResult<ReportResultPage>> RunAsync(Guid callerId, ExplorerRunInput input, PageRequest page, CancellationToken cancellationToken)
    {
        AdministrationResult<ReportPlan> plan = await PlanAsync(callerId, input, cancellationToken).ConfigureAwait(false);
        if (!plan.Succeeded)
        {
            return plan.Error;
        }

        ReportExecution execution = await executor.ExecuteAsync(callerId, isExternal: false, plan.Value, [], null, cancellationToken).ConfigureAwait(false);
        if (execution.RevealsSensitive)
        {
            await audit.RecordAsync(ReportAudit.SensitiveReportExecuted(
                callerId, null, null, execution.Rows.Count, execution.Rows.Select(r => r.ProjectId).Distinct().Count())).ConfigureAwait(false);
        }

        return ReportExecutor.Page(execution, [.. plan.Value.ShownColumns.Select(i => ReportExecutor.View(plan.Value.Columns[i]))], null, null, page);
    }

    public async Task<AdministrationResult<ReportExportOutcome>> ExportAsync(
        Guid callerId, ExplorerRunInput input, ReportExportInput output, Guid idempotencyKey, Guid correlationId, CancellationToken cancellationToken)
    {
        AdministrationResult<ReportPlan> plan = await PlanAsync(callerId, input, cancellationToken).ConfigureAwait(false);
        return plan.Succeeded
            ? await jobs.RequestAsync(callerId, ReportJobKind.Explorer, null, ReportRequestSnapshot.Of(plan.Value, []), output, idempotencyKey, correlationId, cancellationToken)
                .ConfigureAwait(false)
            : plan.Error;
    }

    /// <summary>The composition planned against the allowlist in force, for a caller who may compose: 404 for anyone else (R-47).</summary>
    public async Task<AdministrationResult<ReportPlan>> PlanAsync(Guid callerId, ExplorerRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await MayComposeAsync(callerId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.NotFound;
        }

        ReportAllowlist allowlist = await allowlists.ResolveAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return ReportQueryBuilder.ForExplorer(fields, allowlist.Entries, input);
    }

    /// <summary>ADR-019 and ADR-013: <c>REPORT_COMPOSE</c>, held by an internal person.</summary>
    public async Task<bool> MayComposeAsync(Guid callerId, CancellationToken cancellationToken) =>
        !(await access.CallerAsync(callerId, cancellationToken).ConfigureAwait(false)).IsExternal
        && await access.HoldsAsync(callerId, PermissionCatalogue.ReportCompose, cancellationToken).ConfigureAwait(false);
}
