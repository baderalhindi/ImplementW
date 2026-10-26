namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// The runs of a source's record (M-4: a subject does not store its approval instance id), for a source's
/// <see cref="IApprovalOutcomeHandler"/>: unlike <see cref="IApprovalRequests"/> it does not depend on the handlers, so a handler
/// can read the run whose outcome it applies — the deciding task's reason, for one (TASK-050).
/// </summary>
public interface IApprovalRunReader
{
    /// <summary>Every run of the subject, oldest revision first.</summary>
    public Task<IReadOnlyList<ApprovalInstanceDetail>> FindBySubjectAsync(string subjectModule, string subjectType, Guid subjectId, CancellationToken cancellationToken);
}
