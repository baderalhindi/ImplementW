using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// The command edge a source module starts an approval through (ADR-003 §8.2 edges 20–27) and the query it finds the
/// runs of its record by (M-4: a subject does not store its approval instance id).
/// </summary>
public interface IApprovalRequests
{
    /// <summary>
    /// Routes the subject's revision through the APPROVAL_AUTHORITY version in force now, pins that version and creates
    /// every stage's tasks, the first stage due at once. The run is staged, not saved: the source's own save commits it
    /// with the source's transition (M-11). A retried start of the same PENDING revision returns that run. A revision
    /// after a decided run starts a new run linked to it; the decided run is not touched.
    /// </summary>
    /// <exception cref="MasterDataConfig.Contracts.Resolution.ConfigurationMissingException">
    /// No APPROVAL_AUTHORITY row routes the subject, the rows are ambiguous, or WORKFLOW_POLICY lacks APPROVAL_TASK_DUE_DAYS.
    /// </exception>
    public Task<AdministrationResult<ApprovalInstanceDetail>> StartAsync(ApprovalStart start, CancellationToken cancellationToken);

    /// <summary>Every run of the subject, oldest revision first.</summary>
    public Task<IReadOnlyList<ApprovalInstanceDetail>> FindBySubjectAsync(string subjectModule, string subjectType, Guid subjectId, CancellationToken cancellationToken);
}
