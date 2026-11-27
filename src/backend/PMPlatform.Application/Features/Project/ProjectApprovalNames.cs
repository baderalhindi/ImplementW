using PMPlatform.Application.Features.Approval.Contracts;

namespace PMPlatform.Application.Features.Project;

/// <summary>Names the projects approval runs are scoped to, for the approver's inbox (stage QA D1-01). Approval decides who may see them.</summary>
internal sealed class ProjectApprovalNames(IProjectRepository repository) : IApprovalProjectNames
{
    public async Task<IReadOnlyDictionary<Guid, ApprovalProjectName>> FindAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);

        Dictionary<Guid, ApprovalProjectName> names = [];
        foreach (Guid projectId in projectIds)
        {
            if (await repository.ReadAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project)
            {
                names[projectId] = new ApprovalProjectName(project.Id, project.FormalProjectId, project.Title);
            }
        }

        return names;
    }
}
