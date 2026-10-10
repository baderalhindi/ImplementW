using System.Globalization;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// What a pass may do (TBC-RPT-10 to -12 are AHDA's to set): how many jobs of each kind it takes, how long an output stays downloadable, how many
/// rows an export may hold, and how long a job may sit VALIDATING or RUNNING before its worker is taken to have stopped.
/// </summary>
public sealed record ReportMaintenancePolicy(int BatchSize, TimeSpan OutputLifetime, int MaxExportRows, TimeSpan AbandonAfter);

/// <summary>FG-02's job runner (§10.1, §21 Report Job Service): one pass of validation, generation, abandonment and expiry.</summary>
public interface IReportMaintenance
{
    /// <summary>Runs one pass; returns how many jobs and outputs it moved.</summary>
    public Task<int> RunAsync(ReportMaintenancePolicy policy, CancellationToken cancellationToken);
}

/// <summary>
/// A pass over report jobs, each in its own unit of work so one failure touches no other (FG-02 §26). REQUESTED → VALIDATING → QUEUED, or FAILED with
/// a safe code, after the requester's access, the report version and the request are checked again; QUEUED → RUNNING → COMPLETED, the output read,
/// rendered, hashed and stored privately in one save with the job — or FAILED, with no partial file kept (BR-RPT-043). Every step claims the job
/// by saving it unchanged-since-read, so two passes never run one job twice, and a cancellation made meanwhile wins: the output is not saved.
/// An output past its expiry is purged and its job EXPIRED; the records stay (RPT-CC-22).
/// </summary>
internal sealed partial class ReportMaintenance(
    IReportRepository repository,
    ReportJobAuthorization authorization,
    ReportExecutor executor,
    IEnumerable<IReportRenderer> renderers,
    IOrganizationDirectory organizations,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<ReportMaintenance> logger) : IReportMaintenance
{
    public async Task<int> RunAsync(ReportMaintenancePolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        int moved = 0;
        foreach (Guid jobId in await repository.ListJobIdsAsync(ReportJobStatus.Requested, policy.BatchSize, cancellationToken).ConfigureAwait(false))
        {
            moved += await SafelyAsync(jobId, () => ValidateAsync(jobId, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        foreach (Guid jobId in await repository.ListJobIdsAsync(ReportJobStatus.Queued, policy.BatchSize, cancellationToken).ConfigureAwait(false))
        {
            moved += await SafelyAsync(jobId, () => GenerateAsync(jobId, policy, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (Guid jobId in await repository.ListAbandonedJobIdsAsync(now - policy.AbandonAfter, policy.BatchSize, cancellationToken).ConfigureAwait(false))
        {
            moved += await SafelyAsync(jobId, () => FailAsync(jobId, ReportErrorCodes.OutputGenerationFailed, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        foreach (Guid jobId in await repository.ListExpiredJobIdsAsync(now, policy.BatchSize, cancellationToken).ConfigureAwait(false))
        {
            moved += await SafelyAsync(jobId, () => ExpireAsync(jobId, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        return moved;
    }

    private async Task<int> ValidateAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (await ClaimAsync(jobId, ReportJobStatus.Requested, ReportJobStatus.Validating, cancellationToken).ConfigureAwait(false) is not { } job)
        {
            return 0;
        }

        JobPlan planned = await authorization.PlanAsync(job, cancellationToken).ConfigureAwait(false);
        if (planned.FailureCode is { } failure)
        {
            return await FailAsync(job, ReportJobStatus.Validating, failure, cancellationToken).ConfigureAwait(false);
        }

        Touch(job, ReportJobStatus.Queued);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ReportSaveOutcome.Saved ? 1 : 0;
    }

    private async Task<int> GenerateAsync(Guid jobId, ReportMaintenancePolicy policy, CancellationToken cancellationToken)
    {
        if (await ClaimAsync(jobId, ReportJobStatus.Queued, ReportJobStatus.Running, cancellationToken).ConfigureAwait(false) is not { } job)
        {
            return 0;
        }

        // Checked again now: the requester may have lost the report since the job was queued (US-RPT-SYS-026).
        JobPlan planned = await authorization.PlanAsync(job, cancellationToken).ConfigureAwait(false);
        if (planned.Plan is not { } plan)
        {
            return await FailAsync(job, ReportJobStatus.Running, planned.FailureCode!, cancellationToken).ConfigureAwait(false);
        }

        ReportExecution execution = await executor.ExecuteAsync(
            job.RequestedByUserId, planned.IsExternal, plan, [PermissionCatalogue.ReportExport], null, cancellationToken).ConfigureAwait(false);
        if (!execution.IsNarrowingAuthorized)
        {
            return await FailAsync(job, ReportJobStatus.Running, ReportErrorCodes.ParameterUnauthorized, cancellationToken).ConfigureAwait(false);
        }

        if (execution.Rows.Count > policy.MaxExportRows)
        {
            return await FailAsync(job, ReportJobStatus.Running, ReportErrorCodes.ExportSizeLimitExceeded, cancellationToken).ConfigureAwait(false);
        }

        OutputSensitivity sensitivity = execution.RevealsSensitive ? OutputSensitivity.Sensitive : OutputSensitivity.Standard;
        string title = planned.Definition is { } definition ? ReportText.In(definition.Name, job.ReportLanguage) : ReportText.In(ReportText.Explorer, job.ReportLanguage);
        IReadOnlyList<ReportDocumentLine> context = await ContextAsync(job, planned, execution, cancellationToken).ConfigureAwait(false);
        RenderedReport rendered = renderers.Single(r => r.Format == job.ExportFormat)
            .Render(ReportDocumentBuilder.Build(title, job.ReportLanguage, execution, context, sensitivity));

        DateTimeOffset now = timeProvider.GetUtcNow();
        string key = Guid.NewGuid().ToString("N");
        GeneratedOutput output = new()
        {
            Id = Guid.CreateVersion7(now),
            ReportJobId = job.Id,
            StorageObjectKey = key,
            FileName = FileName(planned.Definition?.Code, execution.GeneratedAt, rendered.FileExtension),
            ContentType = rendered.ContentType,
            SizeBytes = rendered.Content.LongLength,
            ChecksumSha256 = ReportChecksums.Sha256(rendered.Content),
            Sensitivity = sensitivity,
            RowCount = execution.Rows.Count,
            SourceAsOf = execution.SourceAsOf,
            AuthorizationFootprint = ReportFootprint.Of(execution).ToJson(),
            ExpiresAt = now + policy.OutputLifetime,
            Status = GeneratedOutputStatus.Available,
            CreatedAt = now,
            CreatedBy = ReportServicePrincipal.Id,
            UpdatedAt = now,
            UpdatedBy = ReportServicePrincipal.Id,
        };
        repository.Add(new ReportOutputContent
        {
            Id = Guid.CreateVersion7(now),
            StorageObjectKey = key,
            Content = rendered.Content,
            CreatedAt = now,
            CreatedBy = ReportServicePrincipal.Id,
            UpdatedAt = now,
            UpdatedBy = ReportServicePrincipal.Id,
        });
        repository.Add(output);
        Touch(job, ReportJobStatus.Completed);
        job.CompletedAt = now;
        audit.Stage(ReportAudit.ExportCompleted(job, output, execution.Rows.Select(r => r.ProjectId).Distinct().Count(), rendered.NeutralizedCellCount));

        // A cancellation saved since the claim changed the job: this save is refused and no file is kept (US-RPT-SYS-036).
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != ReportSaveOutcome.Saved)
        {
            repository.Clear();
            return 0;
        }

        return 1;
    }

    private async Task<int> ExpireAsync(Guid jobId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        ReportJob? job = await repository.FindJobAsync(jobId, null, cancellationToken).ConfigureAwait(false);
        GeneratedOutput? output = await repository.FindOutputAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || output is not { Status: GeneratedOutputStatus.Available or GeneratedOutputStatus.Expired } || output.ExpiresAt > now)
        {
            return 0;
        }

        await repository.RemoveContentAsync(output.StorageObjectKey, cancellationToken).ConfigureAwait(false);
        output.Status = GeneratedOutputStatus.Purged;
        output.PurgedAt = now;
        output.UpdatedAt = now;
        output.UpdatedBy = ReportServicePrincipal.Id;
        if (job.Status == ReportJobStatus.Completed)
        {
            Touch(job, ReportJobStatus.Expired);
            audit.Stage(ReportAudit.OutputExpired(job));
        }

        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ReportSaveOutcome.Saved ? 1 : 0;
    }

    /// <summary>A job left VALIDATING or RUNNING by a worker that stopped, or one whose generation failed: FAILED, with no file kept.</summary>
    private async Task<int> FailAsync(Guid jobId, string failureCode, CancellationToken cancellationToken)
    {
        repository.Clear();
        return await repository.FindJobAsync(jobId, null, cancellationToken).ConfigureAwait(false) is { Status: ReportJobStatus.Validating or ReportJobStatus.Running } job
            ? await FailAsync(job, job.Status, failureCode, cancellationToken).ConfigureAwait(false)
            : 0;
    }

    private async Task<int> FailAsync(ReportJob job, ReportJobStatus from, string failureCode, CancellationToken cancellationToken)
    {
        Touch(job, ReportJobStatus.Failed);
        job.FailureCode = failureCode;
        job.CompletedAt = timeProvider.GetUtcNow();
        audit.Stage(ReportAudit.ExportFailed(job, from));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ReportSaveOutcome.Saved ? 1 : 0;
    }

    /// <summary>The job, if it is still in <paramref name="from"/>, saved in <paramref name="to"/>; null if another pass or its requester changed it first.</summary>
    private async Task<ReportJob?> ClaimAsync(Guid jobId, ReportJobStatus from, ReportJobStatus to, CancellationToken cancellationToken)
    {
        repository.Clear();
        if (await repository.FindJobAsync(jobId, null, cancellationToken).ConfigureAwait(false) is not { } job || job.Status != from)
        {
            return null;
        }

        Touch(job, to);
        if (to == ReportJobStatus.Running)
        {
            job.StartedAt = job.UpdatedAt;
        }

        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ReportSaveOutcome.Saved ? job : null;
    }

    /// <summary>One job's step; a failure is logged with the job's id and the exception's type, never its data, and the job is FAILED (FG-02 §26).</summary>
    private async Task<int> SafelyAsync(Guid jobId, Func<Task<int>> step, CancellationToken cancellationToken)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStepFailed(logger, jobId, exception.GetType().Name);
            return await FailAsync(jobId, ReportErrorCodes.OutputGenerationFailed, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The output's context: the parameters or filters as the reader of the file needs them, in its language.</summary>
    private async Task<IReadOnlyList<ReportDocumentLine>> ContextAsync(ReportJob job, JobPlan planned, ReportExecution execution, CancellationToken cancellationToken)
    {
        Language language = job.ReportLanguage;
        List<ReportDocumentLine> lines = [];
        if (planned.Definition is { } definition)
        {
            lines.Add(new(ReportText.In(ReportText.Version, language), definition.VersionNo.ToString(CultureInfo.InvariantCulture)));
            ReportRequestSnapshot snapshot = ReportRequestSnapshot.Parse(job.RequestSnapshot);
            IReadOnlyList<ReportParameter> parameters = await repository.ListParametersAsync(definition.Id, track: false, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ReportParameterOption> options = await repository.ListOptionsAsync([.. parameters.Select(p => p.Id)], track: false, cancellationToken).ConfigureAwait(false);
            OrganizationNames names = await organizations.ListNamesAsync([.. planned.Plan!.DepartmentId is { } d ? [d] : Array.Empty<Guid>()], [], cancellationToken).ConfigureAwait(false);
            foreach (ReportParameterInput given in snapshot.Parameters)
            {
                if (parameters.FirstOrDefault(p => p.Code == given.Code) is not { } parameter)
                {
                    continue;
                }

                string value = parameter.DataType switch
                {
                    ReportParameterDataType.Option => options.FirstOrDefault(o => o.ReportParameterId == parameter.Id && o.ValueCode == given.Value) is { } option
                        ? ReportText.In(option.Label, language) : given.Value,
                    ReportParameterDataType.Department => Guid.TryParse(given.Value, out Guid departmentId) && names.Departments.TryGetValue(departmentId, out BilingualLabel? name)
                        ? ReportText.In(name, language) : given.Value,
                    ReportParameterDataType.Project => execution.Rows.FirstOrDefault(r => r.ProjectId.ToString() == given.Value) is { } row
                        ? $"{row.FormalProjectId} {row.Title}".Trim() : given.Value,
                    _ => given.Value,
                };
                lines.Add(new($"{ReportText.In(ReportText.Parameters, language)}: {ReportText.In(parameter.Label, language)}", value));
            }
        }
        else
        {
            lines.AddRange(execution.Plan.Filters.Select(f => new ReportDocumentLine(
                $"{ReportText.In(ReportText.Filters, language)}: {ReportText.In(execution.Plan.Columns[f.Column].Label, language)}",
                $"{ReportFilterWords.Of(f.Operator)} {string.Join(", ", f.Values.Select(v => ReportText.Code(v, language)))}")));
        }

        return lines;
    }

    private void Touch(ReportJob job, ReportJobStatus status)
    {
        job.Status = status;
        job.UpdatedAt = timeProvider.GetUtcNow();
        job.UpdatedBy = ReportServicePrincipal.Id;
    }

    /// <summary>A safe download name (US-RPT-SYS-058): the report's code or the explorer, and the generation time — ASCII letters, digits, '_' and '-' only.</summary>
    private static string FileName(ReportCode? code, DateTimeOffset generatedAt, string extension) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{(code is { } c ? System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(c.ToString()) : "REPORT_EXPLORER")}_{generatedAt.UtcDateTime:yyyyMMdd'T'HHmm'Z'}.{extension}");

    [LoggerMessage(Level = LogLevel.Error, Message = "Report job {JobId}: a step failed ({Reason}); the job is FAILED with no output kept.")]
    private static partial void LogStepFailed(ILogger logger, Guid jobId, string reason);
}

/// <summary>A filter's operator as the context of an output states it.</summary>
internal static class ReportFilterWords
{
    public static string Of(ReportFilterOperator op) => op switch
    {
        ReportFilterOperator.Eq => "=",
        ReportFilterOperator.Neq => "≠",
        ReportFilterOperator.In => "∈",
        ReportFilterOperator.Gt => ">",
        ReportFilterOperator.Gte => "≥",
        ReportFilterOperator.Lt => "<",
        ReportFilterOperator.Lte => "≤",
        ReportFilterOperator.Between => "↔",
        ReportFilterOperator.Contains => "∋",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown operator."),
    };
}
