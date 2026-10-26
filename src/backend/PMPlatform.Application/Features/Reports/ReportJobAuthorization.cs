using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>A job's request planned for its requester now, or the safe failure code it ends with.</summary>
internal sealed record JobPlan(ReportPlan? Plan, ReportDefinition? Definition, bool IsExternal, string? FailureCode)
{
    public static JobPlan Fail(string code) => new(null, null, false, code);
}

/// <summary>
/// Every check a job's request passed when it was accepted, made again for its requester now (US-RPT-SYS-026, -034; RPT-CC-21): an active account,
/// the export permission, the report's audience and its version still the one in force — or, for a composition, <c>REPORT_COMPOSE</c> and the
/// allowlist in force — and the request still valid against them. The worker runs it before reading any data and the download before serving
/// any byte, so neither relies on what was true when the job was asked for.
/// </summary>
internal sealed class ReportJobAuthorization(
    IReportRepository repository,
    ReportAccess access,
    ReportFields fields,
    ReportExecutor executor,
    IReportAllowlistReader allowlists,
    TimeProvider timeProvider)
{
    public async Task<JobPlan> PlanAsync(ReportJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        Guid requester = job.RequestedByUserId;
        if (!await access.IsActiveAsync(requester, cancellationToken).ConfigureAwait(false)
            || !await access.HoldsAsync(requester, PermissionCatalogue.ReportExport, cancellationToken).ConfigureAwait(false))
        {
            return JobPlan.Fail(ReportErrorCodes.ExportNotPermitted);
        }

        UserRoles caller = await access.CallerAsync(requester, cancellationToken).ConfigureAwait(false);
        ReportRequestSnapshot snapshot = ReportRequestSnapshot.Parse(job.RequestSnapshot);
        if (job.Kind == ReportJobKind.Report)
        {
            ReportDefinition? definition = job.ReportDefinitionId is { } id ? await repository.ReadDefinitionAsync(id, cancellationToken).ConfigureAwait(false) : null;
            if (definition is not { LifecycleState: GovernedLifecycleState.Published })
            {
                return JobPlan.Fail(ReportErrorCodes.NotActive);
            }

            if (!await access.MayRunAsync(requester, definition, cancellationToken).ConfigureAwait(false))
            {
                return JobPlan.Fail(ReportErrorCodes.AccessDenied);
            }

            ReportParts parts = await access.PartsAsync(definition.Id, cancellationToken).ConfigureAwait(false);
            AdministrationResult<ReportPlan> plan = ReportQueryBuilder.ForReport(fields, definition.Code, definition.PrimaryProjectionCode, parts.Columns, parts.Parameters, snapshot.ToReportInput());
            return plan.Succeeded ? new JobPlan(plan.Value, definition, caller.IsExternal, null) : JobPlan.Fail(plan.Error.Code ?? ReportErrorCodes.DefinitionInvalid);
        }

        if (caller.IsExternal || !await access.HoldsAsync(requester, PermissionCatalogue.ReportCompose, cancellationToken).ConfigureAwait(false))
        {
            return JobPlan.Fail(ReportErrorCodes.AccessDenied);
        }

        ReportAllowlist allowlist;
        try
        {
            allowlist = await allowlists.ResolveAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }
        catch (ConfigurationMissingException)
        {
            return JobPlan.Fail(ReportErrorCodes.ColumnNotSupported);
        }

        AdministrationResult<ReportPlan> composition = ReportQueryBuilder.ForExplorer(fields, allowlist.Entries, snapshot.ToExplorerInput());
        return composition.Succeeded ? new JobPlan(composition.Value, null, IsExternal: false, null) : JobPlan.Fail(composition.Error.Code ?? ReportErrorCodes.ColumnNotSupported);
    }

    /// <summary>
    /// Whether the output's requester may still see everything it holds: the job planned again for them now and run over the output's projects
    /// only, with its filters and narrowing set aside — they select rows, they grant nothing — and every value it revealed still not withheld.
    /// </summary>
    public async Task<bool> MayStillSeeAsync(ReportJob job, GeneratedOutput output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        JobPlan planned = await PlanAsync(job, cancellationToken).ConfigureAwait(false);
        if (planned.Plan is not { } plan)
        {
            return false;
        }

        ReportFootprint footprint = ReportFootprint.Parse(output.AuthorizationFootprint);
        ReportExecution now = await executor.ExecuteAsync(
            job.RequestedByUserId, planned.IsExternal, plan with { Filters = [], ProjectId = null, DepartmentId = null }, [PermissionCatalogue.ReportExport],
            footprint.ProjectIds, cancellationToken, includeIneligible: true).ConfigureAwait(false);
        return footprint.IsCoveredBy(now);
    }
}
