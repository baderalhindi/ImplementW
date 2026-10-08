using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>The representations of WF-10's rows: a case with its readiness roll-up, read for a page at once.</summary>
internal sealed class CloseoutViews(ICloseoutRepository repository)
{
    public async Task<IReadOnlyList<CompletionCaseDetail>> CompletionDetailsAsync(IReadOnlyList<CompletionCase> cases, CancellationToken cancellationToken)
    {
        ILookup<Guid, ReadinessCheck> readiness = await ReadinessOfAsync(cases, cancellationToken).ConfigureAwait(false);
        return [.. cases.Select(c => new CompletionCaseDetail(
            c.Id, c.ProjectId, c.Status, c.RevisionNo, c.RequestedByUserId, c.SubmittedAt, c.ActualProjectCompletionDate, c.CompletionNarrative, c.EffectedAt,
            ReadinessRollUp.Of(readiness[c.Id]), c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy))];
    }

    public async Task<IReadOnlyList<ClosureCaseDetail>> ClosureDetailsAsync(IReadOnlyList<ClosureCase> cases, CancellationToken cancellationToken)
    {
        ILookup<Guid, ReadinessCheck> readiness = await ReadinessOfAsync(cases, cancellationToken).ConfigureAwait(false);
        return [.. cases.Select(c => new ClosureCaseDetail(
            c.Id, c.ProjectId, c.CompletionCaseId, c.Outcome, c.Status, c.RevisionNo, c.RequestedByUserId, c.SubmittedAt, c.ClosureNarrative, c.EffectedAt,
            ReadinessRollUp.Of(readiness[c.Id]), c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy))];
    }

    public static PostProjectObligationDetail ToDetail(PostProjectObligation o)
    {
        ArgumentNullException.ThrowIfNull(o);
        return new PostProjectObligationDetail(
            o.Id, o.ProjectId, o.CompletionCaseId, o.ClosureCaseId, o.Title, o.Description, o.OwnerUserId, o.DueDate, o.Status, o.SatisfiedAt,
            o.CreatedAt, o.CreatedBy, o.UpdatedAt, o.UpdatedBy);
    }

    public static ReadinessRecordDetail ToDetail(ReadinessCheck r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new ReadinessRecordDetail(r.Id, r.CompletionCaseId, r.ClosureCaseId, r.CheckCode, r.Result, r.EvaluatedAt, r.BlockingCount, r.WaivedByUserId, r.Detail, r.CreatedBy);
    }

    private async Task<ILookup<Guid, ReadinessCheck>> ReadinessOfAsync(IEnumerable<CloseoutCase> cases, CancellationToken cancellationToken) =>
        (await repository.ListReadinessAsync([.. cases.Select(c => c.Id)], cancellationToken).ConfigureAwait(false))
        .ToLookup(r => (r.CompletionCaseId ?? r.ClosureCaseId)!.Value);
}
