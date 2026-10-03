using PMPlatform.Application.Features.Approval.Contracts;

namespace PMPlatform.Application.Features.Approval;

/// <summary>A subject's runs as WF-11 holds them, with every task.</summary>
internal sealed class ApprovalRunReader(IApprovalRepository repository) : IApprovalRunReader
{
    public async Task<IReadOnlyList<ApprovalInstanceDetail>> FindBySubjectAsync(string subjectModule, string subjectType, Guid subjectId, CancellationToken cancellationToken) =>
        [.. (await repository.FindBySubjectAsync(subjectModule, subjectType, subjectId, cancellationToken).ConfigureAwait(false))
            .Select(run => ApprovalMapping.ToDetail(run.Instance, run.Tasks))];
}
