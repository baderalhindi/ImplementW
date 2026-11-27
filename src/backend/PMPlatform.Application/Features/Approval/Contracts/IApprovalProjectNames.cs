namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// Names the projects runs are scoped to, so an inbox row says which project it decides (stage QA D1-01). Implemented by Project, which
/// already references Approval's Contracts (ADR-003 §8.2 edge 20), so Approval reads no project data itself (M-8). Approval asks only for the
/// scope projects of tasks the caller's authority has been proved over (APPROVAL_DECIDE covering the run's anchors, M-7).
/// </summary>
public interface IApprovalProjectNames
{
    /// <summary>The names of the projects that exist among <paramref name="projectIds"/>, by id.</summary>
    public Task<IReadOnlyDictionary<Guid, ApprovalProjectName>> FindAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);
}
