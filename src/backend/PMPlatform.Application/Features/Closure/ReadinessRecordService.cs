using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>A case's readiness history (CLO-CC-07, BR-CLO-031): every evaluation and waiver as recorded, newest first.</summary>
internal sealed class ReadinessRecordService(ICloseoutRepository repository, CloseoutGate gate) : IReadinessRecordService
{
    public async Task<ReadinessRecordPage> ListAsync(Guid callerId, ReadinessRecordQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        CloseoutCase? @case = query.CompletionCaseId is { } completionId
            ? await repository.FindCaseAsync<CompletionCase>(completionId, null, cancellationToken).ConfigureAwait(false)
            : await repository.FindCaseAsync<ClosureCase>(query.ClosureCaseId!.Value, null, cancellationToken).ConfigureAwait(false);
        if (@case is null || await gate.ViewableProjectAsync(callerId, @case.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new ReadinessRecordPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ReadinessCheck> items, int total) = await repository.PageReadinessAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new ReadinessRecordPage([.. items.Select(CloseoutViews.ToDetail)], page.Page, page.PageSize, total);
    }
}
