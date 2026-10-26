using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// SCR-140 Export History, cancellation and the secure download (FG-02 §9.3, §10.1, §13.5). A job is its requester's alone: another person's job
/// is 404, as if it did not exist (R-47). A download is authorised at download time — the job's request is run again as its requester now, through
/// the same authorization as the data, and the file is served only if that run still shows every project and every value the file holds — and
/// never on its opaque key; a refusal is 403 and audited (BR-RPT-042; TASK-071 acceptance criterion 2). The bytes are checked against their
/// SHA-256 before they are served.
/// </summary>
internal sealed class ReportJobService(
    IReportRepository repository,
    ReportJobAuthorization authorization,
    IAuditTrail audit,
    TimeProvider timeProvider) : IReportJobService
{
    public async Task<ReportJobPage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        (IReadOnlyList<ReportJob> items, int total) = await repository.PageJobsAsync(callerId, page, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, GeneratedOutput> outputs = await repository.ListOutputsAsync([.. items.Select(j => j.Id)], cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, ReportCode> codes = await CodesAsync(items, cancellationToken).ConfigureAwait(false);
        return new ReportJobPage(
            [.. items.Select(j => ReportMapping.ToSummary(j, CodeOf(j, codes), outputs.GetValueOrDefault(j.Id)))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ReportJobDetail>>> GetAsync(Guid callerId, Guid jobId, CancellationToken cancellationToken) =>
        await OwnAsync(callerId, jobId, null, cancellationToken).ConfigureAwait(false) is { } job
            ? await VersionedAsync(job, cancellationToken).ConfigureAwait(false)
            : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<ReportJobDetail>>> CancelAsync(Guid callerId, Guid jobId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (await OwnAsync(callerId, jobId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } job)
        {
            return AdministrationError.NotFound;
        }

        if (job.Status is not (ReportJobStatus.Requested or ReportJobStatus.Validating or ReportJobStatus.Queued or ReportJobStatus.Running))
        {
            return AdministrationError.Conflict(ReportErrorCodes.JobNotCancellable);
        }

        // A RUNNING job's worker saves its output only if the job is unchanged since it claimed it: after this, it cannot (US-RPT-SYS-036).
        ReportJobStatus from = job.Status;
        DateTimeOffset now = timeProvider.GetUtcNow();
        job.Status = ReportJobStatus.Cancelled;
        job.CompletedAt = now;
        job.UpdatedAt = now;
        job.UpdatedBy = callerId;
        audit.Stage(ReportAudit.ExportCancelled(callerId, job, from));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ReportSaveOutcome.Saved
            ? await VersionedAsync(job, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;
    }

    public async Task<AdministrationResult<ReportOutputDownload>> OpenContentAsync(Guid callerId, Guid jobId, CancellationToken cancellationToken)
    {
        if (await OwnAsync(callerId, jobId, null, cancellationToken).ConfigureAwait(false) is not { } job)
        {
            return AdministrationError.NotFound;
        }

        GeneratedOutput? output = await repository.FindOutputAsync(job.Id, cancellationToken).ConfigureAwait(false);
        if (job.Status == ReportJobStatus.Expired || (output is not null && (output.Status != GeneratedOutputStatus.Available || output.ExpiresAt <= timeProvider.GetUtcNow())))
        {
            return AdministrationError.Conflict(ReportErrorCodes.OutputExpired);
        }

        if (job.Status != ReportJobStatus.Completed || output is null)
        {
            return AdministrationError.Conflict(ReportErrorCodes.OutputNotAvailable);
        }

        if (!await authorization.MayStillSeeAsync(job, output, cancellationToken).ConfigureAwait(false))
        {
            await audit.RecordAsync(ReportAudit.OutputAccessDenied(callerId, job)).ConfigureAwait(false);
            return AdministrationError.Forbidden;
        }

        byte[] content = await repository.ReadContentAsync(output.StorageObjectKey, cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException($"Output of job {job.Id} has no stored content.");
        if (!string.Equals(ReportChecksums.Sha256(content), output.ChecksumSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Output of job {job.Id} does not match its recorded SHA-256.");
        }

        audit.Stage(ReportAudit.OutputDownloaded(callerId, job, output));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ReportSaveOutcome.Saved
            ? new ReportOutputDownload(new MemoryStream(content, writable: false), output.FileName, output.ContentType)
            : AdministrationError.PreconditionFailed;
    }

    /// <summary>The caller's own job; anyone else's does not exist for them (R-47).</summary>
    private async Task<ReportJob?> OwnAsync(Guid callerId, Guid jobId, uint? expectedVersion, CancellationToken cancellationToken) =>
        await repository.FindJobAsync(jobId, expectedVersion, cancellationToken).ConfigureAwait(false) is { } job && job.RequestedByUserId == callerId ? job : null;

    private async Task<Versioned<ReportJobDetail>> VersionedAsync(ReportJob job, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, GeneratedOutput> outputs = await repository.ListOutputsAsync([job.Id], cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, ReportCode> codes = await CodesAsync([job], cancellationToken).ConfigureAwait(false);
        return new(ReportMapping.ToDetail(job, CodeOf(job, codes), outputs.GetValueOrDefault(job.Id)), repository.RowVersionOf(job));
    }

    private async Task<IReadOnlyDictionary<Guid, ReportCode>> CodesAsync(IEnumerable<ReportJob> jobs, CancellationToken cancellationToken)
    {
        Dictionary<Guid, ReportCode> codes = [];
        foreach (Guid definitionId in jobs.Select(j => j.ReportDefinitionId).OfType<Guid>().Distinct())
        {
            if (await repository.ReadDefinitionAsync(definitionId, cancellationToken).ConfigureAwait(false) is { } definition)
            {
                codes[definitionId] = definition.Code;
            }
        }

        return codes;
    }

    private static ReportCode? CodeOf(ReportJob job, IReadOnlyDictionary<Guid, ReportCode> codes) =>
        job.ReportDefinitionId is { } id && codes.TryGetValue(id, out ReportCode code) ? code : null;
}
